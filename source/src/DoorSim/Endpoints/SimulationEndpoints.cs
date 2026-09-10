namespace DoorSim.Endpoints;

public static class SimulationEndpoints
{
    public static IEndpointRouteBuilder MapSimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/simulate");

        // Library-backed events (card looked up by ID)
        group.MapPost("/event", async (DoorEventRequest request, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.RunEventAsync(request);

            return Results.Ok();
        });

        // Raw value send (no library entry needed)
        group.MapPost("/send-card", async (RawCardRequest request, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.SendCardAsync(request);

            return Results.Ok();
        });

        group.MapPost("/raw-event", async (RawDoorEventRequest request, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.RunRawEventAsync(request);

            return Results.Ok();
        });

        // Direct DPS / REX primitives (no status tracking, instant)
        group.MapPost("/door/{readerId:int}/open", async (int readerId, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.OpenDoorAsync(readerId);
            return Results.Ok();
        });

        group.MapPost("/door/{readerId:int}/close", async (int readerId, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.CloseDoorAsync(readerId);
            return Results.Ok();
        });

        group.MapPost("/rex/{readerId:int}/quick", async (int readerId, SimulationOrchestrator orchestrator) =>
        {
            await orchestrator.QuickRexAsync(readerId);
            return Results.Ok();
        });

        // Reader status polling
        group.MapGet("/status/{readerId:int}", (int readerId, SimulationOrchestrator orchestrator) =>
            Results.Ok(orchestrator.GetStatus(readerId)));

        // OSDP connectivity (always true for Wiegand)
        group.MapGet("/connectivity/{readerId:int}", (int readerId, SimulationOrchestrator orchestrator) =>
            Results.Ok(orchestrator.GetConnectivity(readerId)));

        // SSE stream — pushes DoorStatusUpdate[] for all active doors every 500 ms.
        // Replaces per-reader polling; a single persistent connection covers all doors.
        group.MapGet("/stream", (SimulationOrchestrator orchestrator, CancellationToken ct) =>
            TypedResults.ServerSentEvents(orchestrator.StreamStatusAsync(ct: ct)));

        return app;
    }
}
