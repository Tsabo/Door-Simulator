using System.ComponentModel;
using DoorSim.Validation;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>
/// MCP tools wrapping <see cref="SimulationOrchestrator" /> — the "drive a test scenario" surface.
/// Mirrors <c>DoorSim.Endpoints.SimulationEndpoints</c> logic exactly so behavior stays identical
/// between the REST API and MCP.
/// </summary>
[McpServerToolType]
internal static class SimulationTools
{
    private const string CannotVerifyPacs =
        " DoorSim only transmits the configured signal and reports its own last action or contact state " +
        "— it cannot see the panel's (PACS's) access decision or verify the physical lock/door state " +
        "independently.";

    [McpServerTool(Name = "simulate_card_event")]
    [Description(
        "Simulate a card-read or door event using a card already saved in the card library, looked up " +
        "by CardEntryId (required unless EventType is EgressCycle). CardReadOnly sends just a card read; " +
        "AccessCycle sends a card read followed by a door open/close cycle using the configured timing " +
        "settings; EgressCycle ignores the card and simulates a REX-triggered egress cycle." + CannotVerifyPacs)]
    public static async Task<string> SimulateCardEvent(DoorEventRequest request,
        SimulationOrchestrator orchestrator,
        IReaderBank bank,
        CardLibraryService cardService,
        CardFormatService formatService)
    {
        var validationError = SimulationValidation.Validate(request);
        if (validationError is not null)
            throw new McpException(validationError);

        if (!bank.ContainsReader(request.ReaderId))
            throw new McpException($"Reader {request.ReaderId} not found in the active bank.");

        if (request.EventType != DoorEventType.EgressCycle)
        {
            var card = await cardService.GetAsync(request.CardEntryId!.Value);
            if (card is null)
                throw new McpException($"Card {request.CardEntryId.Value} not found in library.");

            if (card.Format == WiegandFormat.Custom)
            {
                var format = await formatService.GetAsync(card.CustomFormatId!.Value);
                if (format is null)
                    throw new McpException($"Custom format {card.CustomFormatId} not found.");

                var boundsError = CardFormatValidation.ValidateCredential(card.CardNumber, card.FacilityCode, format);
                if (boundsError is not null)
                    throw new McpException(boundsError);
            }
        }

        await orchestrator.RunEventAsync(request);

        return $"Queued {request.EventType} on reader {request.ReaderId}.";
    }

    [McpServerTool(Name = "simulate_raw_card_read")]
    [Description(
        "Send a single raw card read to a reader — no library entry, no door/REX cycle. Takes an inline " +
        "CardNumber/FacilityCode/Format instead of a CardEntryId, for testing a card value that hasn't been " +
        "saved to the library. Never opens the door or trips REX; use simulate_raw_card_event with " +
        "EventType AccessCycle or EgressCycle for that." + CannotVerifyPacs)]
    public static async Task<string> SimulateRawCardRead(RawCardRequest request,
        SimulationOrchestrator orchestrator,
        IReaderBank bank,
        CardFormatService formatService)
    {
        var validationError = SimulationValidation.Validate(request);
        if (validationError is not null)
            throw new McpException(validationError);

        if (!bank.ContainsReader(request.ReaderId))
            throw new McpException($"Reader {request.ReaderId} not found in the active bank.");

        if (request.Format == WiegandFormat.Custom)
        {
            var format = await formatService.GetAsync(request.CustomFormatId!.Value);
            if (format is null)
                throw new McpException($"Custom format {request.CustomFormatId} not found.");

            var boundsError = CardFormatValidation.ValidateCredential(request.CardNumber, request.FacilityCode, format);
            if (boundsError is not null)
                throw new McpException(boundsError);
        }

        await orchestrator.SendCardAsync(request);

        return $"Sent a raw card read ({request.Format}, facility code {request.FacilityCode}, " +
               $"card #{request.CardNumber}) to reader {request.ReaderId}.";
    }

    [McpServerTool(Name = "simulate_raw_bits")]
    [Description(
        "Send a raw bit-stream card read to a reader — literal bits, no facility code/card number/format " +
        "rules, and no format validation. For Wiegand, bits are pulsed across D0/D1 left-to-right. For OSDP, " +
        "bits are queued as an osdp_RAW reply." + CannotVerifyPacs)]
    public static async Task<string> SimulateRawBits(RawBitsRequest request,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        var validationError = SimulationValidation.Validate(request);
        if (validationError is not null)
            throw new McpException(validationError);

        if (!bank.ContainsReader(request.ReaderId))
            throw new McpException($"Reader {request.ReaderId} not found in the active bank.");

        await orchestrator.SendBitsAsync(request);

        return $"Sent {request.Bits.Length} raw bit(s) to reader {request.ReaderId}.";
    }

    [McpServerTool(Name = "simulate_raw_card_event")]
    [Description(
        "The general-purpose version of simulate_raw_card_read: same EventType-driven behavior " +
        "(CardReadOnly/AccessCycle/EgressCycle) as simulate_card_event, but takes CardNumber/FacilityCode/" +
        "Format directly instead of looking up a CardEntryId." + CannotVerifyPacs)]
    public static async Task<string> SimulateRawCardEvent(RawDoorEventRequest request,
        SimulationOrchestrator orchestrator,
        IReaderBank bank,
        CardFormatService formatService)
    {
        var validationError = SimulationValidation.Validate(request);
        if (validationError is not null)
            throw new McpException(validationError);

        if (!bank.ContainsReader(request.ReaderId))
            throw new McpException($"Reader {request.ReaderId} not found in the active bank.");

        if (request.EventType != DoorEventType.EgressCycle && request.Format == WiegandFormat.Custom)
        {
            var format = await formatService.GetAsync(request.CustomFormatId!.Value);
            if (format is null)
                throw new McpException($"Custom format {request.CustomFormatId} not found.");

            var boundsError = CardFormatValidation.ValidateCredential(request.CardNumber, request.FacilityCode, format);
            if (boundsError is not null)
                throw new McpException(boundsError);
        }

        await orchestrator.RunRawEventAsync(request);

        return $"Queued raw {request.EventType} on reader {request.ReaderId}.";
    }

    [McpServerTool(Name = "open_door", Destructive = false, Idempotent = true)]
    [Description(
        "Force a reader's door position switch (DPS) to the open state. Direct hardware primitive — takes " +
        "effect immediately, bypassing the status tracking and timing that simulate_* events use." + CannotVerifyPacs)]
    public static async Task<string> OpenDoor([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        await orchestrator.OpenDoorAsync(readerId);

        return $"Forced reader {readerId}'s door position switch to open.";
    }

    [McpServerTool(Name = "close_door", Destructive = false, Idempotent = true)]
    [Description(
        "Force a reader's door position switch (DPS) to the closed state. Direct hardware primitive — " +
        "takes effect immediately, no status tracking." + CannotVerifyPacs)]
    public static async Task<string> CloseDoor([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        await orchestrator.CloseDoorAsync(readerId);

        return $"Forced reader {readerId}'s door position switch to closed.";
    }

    [McpServerTool(Name = "trip_rex", Destructive = false)]
    [Description(
        "Trip request-to-exit (REX) on a reader, hold for the configured quick-REX duration, then reset. " +
        "Direct hardware primitive — does not open the door itself; a panel watching REX is expected to " +
        "react to it." + CannotVerifyPacs)]
    public static async Task<string> TripRex([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        await orchestrator.QuickRexAsync(readerId);

        return $"Tripped REX on reader {readerId}; it will hold and auto-reset per the configured quick-REX duration.";
    }

    [McpServerTool(Name = "get_reader_status", ReadOnly = true)]
    [Description(
        "Get a reader's current simulation status: Idle, Running, Success, or Error — reflects the most " +
        "recent simulate_card_event/simulate_raw_card_read/simulate_raw_card_event call for this reader, " +
        "not the panel's authorization outcome.")]
    public static SimulationStatus GetReaderStatus([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        return orchestrator.GetStatus(readerId);
    }

    [McpServerTool(Name = "get_reader_connectivity", ReadOnly = true)]
    [Description(
        "Get whether a reader's transport is currently connected. Always true for Wiegand readers (no " +
        "handshake). For OSDP readers, reflects whether the panel has polled within the connection timeout " +
        "window.")]
    public static bool GetReaderConnectivity([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        return orchestrator.GetConnectivity(readerId);
    }

    [McpServerTool(Name = "get_reader_queue", ReadOnly = true)]
    [Description("Get the list of active and pending simulation items in a reader's queue.")]
    public static IReadOnlyList<SimulationQueueItemDto> GetReaderQueue([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        return orchestrator.GetQueue(readerId);
    }

    [McpServerTool(Name = "clear_reader_queue")]
    [Description("Clear all pending (not-yet-running) items from a reader's simulation queue.")]
    public static string ClearReaderQueue([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        var cleared = orchestrator.ClearQueue(readerId);

        return $"Cleared {cleared} pending item(s) from reader {readerId}'s queue.";
    }

    [McpServerTool(Name = "cancel_queue_item")]
    [Description("Cancel a specific simulation item in a reader's queue by its ID.")]
    public static string CancelQueueItem([Description("The reader/door ID, as returned by list_doors.")] int readerId,
        [Description("The queue item ID, as returned by get_reader_queue.")]
        Guid itemId,
        SimulationOrchestrator orchestrator,
        IReaderBank bank)
    {
        if (!bank.ContainsReader(readerId))
            throw new McpException($"Reader {readerId} not found in the active bank.");

        if (!orchestrator.CancelQueueItem(readerId, itemId))
            throw new McpException($"Queue item {itemId} not found for reader {readerId}.");

        return $"Cancelled queue item {itemId} for reader {readerId}.";
    }
}
