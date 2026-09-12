using DoorSim.Validation;

namespace DoorSim.Endpoints;

public static class SimulationEndpoints
{
    public static IEndpointRouteBuilder MapSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/simulate").WithTags("Simulation");

        // Library-backed events (card looked up by ID)
        group.MapPost("/event", async (DoorEventRequest request, SimulationOrchestrator orchestrator, IReaderBank bank, CardLibraryService cardService) =>
            {
                var validationError = SimulationValidation.Validate(request);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                if (!bank.ContainsReader(request.ReaderId))
                    return Results.NotFound($"Reader {request.ReaderId} not found in the active bank.");

                if (request.EventType != DoorEventType.EgressCycle)
                {
                    var card = await cardService.GetAsync(request.CardEntryId!.Value);
                    if (card is null)
                        return Results.NotFound($"Card {request.CardEntryId.Value} not found in library.");
                }

                await orchestrator.RunEventAsync(request);

                return Results.Ok();
            })
            .WithName("RunEvent")
            .WithSummary("Simulate a card-read or door event using a card from the library.")
            .WithDescription(
                "Looks up the card by CardEntryId (required unless EventType is EgressCycle) and behaves " +
                "according to EventType: CardReadOnly sends just a card read; AccessCycle sends a card read " +
                "followed by a door open/close cycle using the configured timing settings; EgressCycle ignores " +
                "the card entirely and simulates a REX-triggered egress cycle. Use /raw-event for the same " +
                "EventType-driven behavior with a raw card value instead of a saved library card.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);

        // Raw value send (no library entry needed)
        group.MapPost("/send-card", async (RawCardRequest request, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                var validationError = SimulationValidation.Validate(request);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                if (!bank.ContainsReader(request.ReaderId))
                    return Results.NotFound($"Reader {request.ReaderId} not found in the active bank.");

                await orchestrator.SendCardAsync(request);

                return Results.Ok();
            })
            .WithName("SendRawCard")
            .WithSummary("Send a single raw card read — no library entry, no door/REX cycle.")
            .WithDescription(
                "Always behaves like /event's CardReadOnly case, but takes an inline CardNumber/FacilityCode/" +
                "Format instead of a CardEntryId — use it to test a card value that hasn't been saved to the " +
                "card library. It never opens the door or trips REX; for that, use /raw-event with " +
                "EventType = AccessCycle or EgressCycle.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);

        // Raw bit-stream send (no library entry or format calculation needed)
        group.MapPost("/send-bits", async (RawBitsRequest request, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                var validationError = SimulationValidation.Validate(request);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                if (!bank.ContainsReader(request.ReaderId))
                    return Results.NotFound($"Reader {request.ReaderId} not found in the active bank.");

                await orchestrator.SendBitsAsync(request);

                return Results.Ok();
            })
            .WithName("SendRawBits")
            .WithSummary("Send a raw bit-stream card read — literal bits without format calculation or parity wrapping.")
            .WithDescription(
                "Transmits an arbitrary binary bit string (e.g. \"10000000000010100011100100\") to the reader " +
                "without requiring facility code, card number, or standard format rules. For Wiegand, bits are pulsed " +
                "across D0/D1 in left-to-right order. For OSDP, bits are queued as an osdp_RAW reply.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);

        group.MapPost("/raw-event", async (RawDoorEventRequest request, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                var validationError = SimulationValidation.Validate(request);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                if (!bank.ContainsReader(request.ReaderId))
                    return Results.NotFound($"Reader {request.ReaderId} not found in the active bank.");

                await orchestrator.RunRawEventAsync(request);

                return Results.Ok();
            })
            .WithName("RunRawEvent")
            .WithSummary("Simulate a card-read or door event using a raw card value instead of a library entry.")
            .WithDescription(
                "The general-purpose version of /send-card: same EventType-driven behavior as /event " +
                "(CardReadOnly / AccessCycle / EgressCycle), but takes CardNumber/FacilityCode/Format directly " +
                "instead of looking up a CardEntryId. Use /send-card as a shorthand when you only need " +
                "EventType = CardReadOnly.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces<string>(StatusCodes.Status404NotFound);

        // Direct DPS / REX primitives (no status tracking, instant)
        group.MapPost("/door/{readerId:int}/open", async (int readerId, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                if (!bank.ContainsReader(readerId))
                    return Results.NotFound($"Reader {readerId} not found in the active bank.");

                await orchestrator.OpenDoorAsync(readerId);
                return Results.Ok();
            })
            .WithName("OpenDoor")
            .WithSummary("Force the door position switch (DPS) to the open state.")
            .WithDescription("Direct hardware primitive — takes effect immediately, bypassing the status " +
                             "tracking and timing that /event and /raw-event use.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound);

        group.MapPost("/door/{readerId:int}/close", async (int readerId, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                if (!bank.ContainsReader(readerId))
                    return Results.NotFound($"Reader {readerId} not found in the active bank.");

                await orchestrator.CloseDoorAsync(readerId);
                return Results.Ok();
            })
            .WithName("CloseDoor")
            .WithSummary("Force the door position switch (DPS) to the closed state.")
            .WithDescription("Direct hardware primitive — takes effect immediately, no status tracking.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound);

        group.MapPost("/rex/{readerId:int}/quick", async (int readerId, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                if (!bank.ContainsReader(readerId))
                    return Results.NotFound($"Reader {readerId} not found in the active bank.");

                await orchestrator.QuickRexAsync(readerId);
                return Results.Ok();
            })
            .WithName("QuickRex")
            .WithSummary("Trip request-to-exit (REX), hold for the configured QuickRexMs, then reset.")
            .WithDescription("Direct hardware primitive — no status tracking. Does not open the door itself; " +
                             "a panel watching REX is expected to react to it.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound);

        // Reader status polling
        group.MapGet("/status/{readerId:int}", (int readerId, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                if (!bank.ContainsReader(readerId))
                    return Results.NotFound($"Reader {readerId} not found in the active bank.");

                return Results.Ok(orchestrator.GetStatus(readerId));
            })
            .WithName("GetReaderStatus")
            .WithSummary("Get a reader's current simulation status.")
            .WithDescription("One of Idle, Running, Success, or Error — reflects the most recent /event, " +
                             "/send-card, or /raw-event call for this reader.")
            .Produces<SimulationStatus>()
            .Produces<string>(StatusCodes.Status404NotFound);

        // OSDP connectivity (always true for Wiegand)
        group.MapGet("/connectivity/{readerId:int}", (int readerId, SimulationOrchestrator orchestrator, IReaderBank bank) =>
            {
                if (!bank.ContainsReader(readerId))
                    return Results.NotFound($"Reader {readerId} not found in the active bank.");

                return Results.Ok(orchestrator.GetConnectivity(readerId));
            })
            .WithName("GetReaderConnectivity")
            .WithSummary("Get whether the reader's transport is currently connected.")
            .WithDescription("Always true for Wiegand readers (no handshake). For OSDP readers, reflects " +
                             "whether the panel has polled within the connection timeout window.")
            .Produces<bool>()
            .Produces<string>(StatusCodes.Status404NotFound);

        // SSE stream — pushes DoorStatusUpdate[] for all active doors every 500 ms.
        // Replaces per-reader polling; a single persistent connection covers all doors.
        group.MapGet("/stream", (SimulationOrchestrator orchestrator, CancellationToken ct) =>
                TypedResults.ServerSentEvents(orchestrator.StreamStatusAsync(ct: ct)))
            .WithName("StreamStatus")
            .WithSummary("Server-Sent Events stream of every active door's status.")
            .WithDescription("Pushes a DoorStatusUpdate[] covering all active doors roughly every 500ms over a " +
                             "single persistent connection — use this instead of polling /status and /connectivity per " +
                             "reader.");

        return app;
    }
}
