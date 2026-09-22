using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>MCP tools wrapping <see cref="CardLibraryService" /> — the saved card library.</summary>
[McpServerToolType]
internal static class CardTools
{
    [McpServerTool(Name = "list_cards", ReadOnly = true)]
    [Description("List all cards in the card library.")]
    public static Task<CardEntry[]> ListCards(CardLibraryService service) =>
        service.GetAllAsync();

    [McpServerTool(Name = "get_card", ReadOnly = true)]
    [Description("Get a single card by its library ID.")]
    public static async Task<CardEntry> GetCard(int id, CardLibraryService service) =>
        await service.GetAsync(id) ?? throw new McpException($"Card {id} not found.");

    [McpServerTool(Name = "create_card", Destructive = false)]
    [Description(
        "Add a new card to the library. The submitted Id and CreatedAt are ignored — the server assigns " +
        "both and returns the created record.")]
    public static async Task<CardEntry> CreateCard(CardEntry card, CardLibraryService service)
    {
        var (created, error) = await service.CreateAsync(card);
        if (error is not null)
            throw new McpException(error);

        return created!;
    }

    [McpServerTool(Name = "update_card", Idempotent = true)]
    [Description("Replace an existing card's details (full replace, not a partial patch).")]
    public static async Task<CardEntry> UpdateCard(int id, CardEntry card, CardLibraryService service)
    {
        var (updated, error) = await service.UpdateAsync(id, card);
        if (error is not null)
            throw new McpException(error);

        return updated ?? throw new McpException($"Card {id} not found.");
    }

    [McpServerTool(Name = "delete_card")]
    [Description("Remove a card from the library. Irreversible.")]
    public static async Task<string> DeleteCard(int id, CardLibraryService service)
    {
        if (!await service.DeleteAsync(id))
            throw new McpException($"Card {id} not found.");

        return $"Deleted card {id}.";
    }
}
