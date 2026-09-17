using MudBlazor;

namespace DoorSim.Client.Components.Shared;

/// <summary>
/// The four-row bit grid. Presentational: it reports every edit back through
/// <see cref="MasksChanged" /> rather than holding mask state of its own.
/// </summary>
public partial class BitMap
{
    private static readonly string[] RowLabels = ["Card mask", "Parity 1", "Parity 2", "Parity 3"];

    private bool _painting;

    /// <summary>Card mask at index 0, parity masks at 1-3, each padded to <see cref="Bits" />.</summary>
    [Parameter]
    [EditorRequired]
    public string[] Masks { get; set; } = [];

    [Parameter]
    public EventCallback<string[]> MasksChanged { get; set; }

    [Parameter]
    [EditorRequired]
    public int Bits { get; set; }

    /// <summary>The token a click paints.</summary>
    [Parameter]
    public char Tool { get; set; } = 'C';

    [Parameter]
    public EventCallback<char> ToolChanged { get; set; }

    private string MaskAt(int row) => row < Masks.Length
        ? CardFormatAnalyzer.PadMask(Masks[row], Bits)
        : new string('X', Bits);

    private async Task StartPaint(int row, int index)
    {
        _painting = true;
        await Paint(row, index);
    }

    private async Task ContinuePaint(int row, int index)
    {
        if (_painting)
            await Paint(row, index);
    }

    /// <summary>
    /// Ends a drag. Bound to both mouseup and mouseleave on the scroll container, so releasing the
    /// button outside the grid doesn't leave painting stuck on — Blazor can't watch the window for
    /// a mouseup without JS interop, and this page stays JS-free.
    /// </summary>
    private void StopPaint() => _painting = false;

    private async Task Paint(int row, int index)
    {
        var legal = row == 0
            ? CardFormatAnalyzer.CardPaintTokens
            : CardFormatAnalyzer.ParityPaintTokens;

        // A token that means nothing for this row is ignored rather than reported as an error.
        if (!legal.Contains(Tool))
            return;

        var cells = MaskAt(row).ToCharArray();

        if (index >= cells.Length || cells[index] == Tool)
            return;

        // One parity bit per rule: writing a new indicator clears whatever was there.
        if (row > 0 && Tool is 'E' or 'O')
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] is 'E' or 'O')
                    cells[i] = 'X';
            }
        }

        cells[index] = Tool;

        await WriteMask(row, new string(cells));
    }

    private async Task OnMaskTyped(int row, string? raw)
    {
        var legal = row == 0
            ? CardFormatAnalyzer.CardPaintTokens
            : CardFormatAnalyzer.ParityPaintTokens;

        var filtered = new string((raw ?? "")
            .ToUpperInvariant()
            .Where(legal.Contains)
            .ToArray());

        await WriteMask(row, CardFormatAnalyzer.PadMask(filtered, Bits));
    }

    private async Task WriteMask(int row, string mask)
    {
        var updated = new string[RowLabels.Length];

        for (var i = 0; i < updated.Length; i++)
            updated[i] = i == row
                ? mask
                : MaskAt(i);

        await MasksChanged.InvokeAsync(updated);
    }

    private static string Display(char token) => token == 'X'
        ? "·"
        : token.ToString();

    private static string TokenClass(char token) => token switch
    {
        'C' => "bit--c",
        'F' => "bit--f",
        'P' => "bit--p",
        'E' => "bit--eo",
        'O' => "bit--eo",
        '1' => "bit--one",
        '0' => "bit--zero",
        _ => "bit--x"
    };

    private static Color TokenColor(char token) => token switch
    {
        'C' => Color.Info,
        'F' => Color.Tertiary,
        'P' => Color.Warning,
        'E' or 'O' => Color.Primary,
        '1' => Color.Success,
        '0' => Color.Error,
        _ => Color.Default
    };

    private static string RoleName(char token) => token switch
    {
        'C' => "card",
        'F' => "facility",
        'P' => "in parity",
        'E' => "even bit",
        'O' => "odd bit",
        '1' => "always on",
        '0' => "always off",
        _ => "unused"
    };
}
