
using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>MCP tools wrapping <see cref="CardFormatService" /> — user-defined custom Wiegand card formats.</summary>
[McpServerToolType]
internal static class CardFormatTools
{
    [McpServerTool(Name = "list_card_formats", ReadOnly = true),
     Description("List all user-defined custom card formats.")]
    public static Task<CustomCardFormat[]> ListCardFormats(CardFormatService service) =>
        service.GetAllAsync();

    [McpServerTool(Name = "get_card_format", ReadOnly = true),
     Description("Get a single custom card format by its ID.")]
    public static async Task<CustomCardFormat> GetCardFormat(int id, CardFormatService service) =>
        await service.GetAsync(id) ?? throw new McpException($"Custom format {id} not found.");

    [McpServerTool(Name = "create_card_format", Destructive = false),
     Description(
         "Add a new custom card format. The submitted Id and CreatedAt are ignored — the server assigns " +
         "both and returns the created record.")]
    public static async Task<CustomCardFormat> CreateCardFormat(CustomCardFormat format, CardFormatService service)
    {
        var (created, error) = await service.CreateAsync(format);
        if (error is not null)
            throw new McpException(error);

        return created!;
    }

    [McpServerTool(Name = "update_card_format", Idempotent = true),
     Description("Replace an existing custom card format's mask definition (full replace).")]
    public static async Task<CustomCardFormat> UpdateCardFormat(int id, CustomCardFormat format, CardFormatService service)
    {
        var (updated, error) = await service.UpdateAsync(id, format);
        if (error is not null)
            throw new McpException(error);

        return updated ?? throw new McpException($"Custom format {id} not found.");
    }

    [McpServerTool(Name = "delete_card_format"),
     Description(
         "Remove a custom card format. Fails if the format is still referenced by a card in the library.")]
    public static async Task<string> DeleteCardFormat(int id, CardFormatService service)
    {
        var (deleted, error) = await service.DeleteAsync(id);
        if (error is not null)
            throw new McpException(error);

        if (!deleted)
            throw new McpException($"Custom format {id} not found.");

        return $"Deleted custom format {id}.";
    }
}
