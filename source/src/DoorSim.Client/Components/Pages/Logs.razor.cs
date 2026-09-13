using Microsoft.JSInterop;

namespace DoorSim.Client.Components.Pages;

public partial class Logs : IDisposable
{
    private const int MaxDisplayLines = 500;

    private readonly CancellationTokenSource _cts = new();
    private readonly List<LogLine> _lines = [];
    private bool _autoScroll = true;
    private ElementReference _consoleRef;
    private LogSeverity _minLevel = LogSeverity.Verbose;
    private string _filterText = "";
    private bool _excludeMode;

    private List<LogLine> FilteredLines => [.. _lines.Where(MatchesFilter)];

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    protected override Task OnInitializedAsync()
    {
        _ = ConsumeAsync();
        return Task.CompletedTask;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var line in LogsClient.SubscribeAsync(_cts.Token))
            {
                _lines.Add(line);
                if (_lines.Count > MaxDisplayLines)
                    _lines.RemoveAt(0);

                await InvokeAsync(StateHasChanged);
                if (_autoScroll)
                    await ScrollToBottomAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Page was disposed
        }
    }

    private async Task OnAutoScrollChanged(bool value)
    {
        _autoScroll = value;
        if (_autoScroll)
            await ScrollToBottomAsync();
    }

    private void OnMinLevelChanged(LogSeverity value) => _minLevel = value;

    private void OnFilterTextChanged(string value) => _filterText = value;

    private void OnExcludeModeChanged(bool value) => _excludeMode = value;

    private bool MatchesFilter(LogLine line)
    {
        if (line.Level < _minLevel)
            return false;

        if (string.IsNullOrWhiteSpace(_filterText))
            return true;

        var matches = line.Message.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                      || (line.SourceContext?.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ?? false);

        return _excludeMode ? !matches : matches;
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("doorsimLogs.scrollToBottom", _consoleRef);
        }
        catch (JSDisconnectedException)
        {
            // Page was disposed mid-call
        }
    }

    private static string LevelClass(LogSeverity level) => level.ToString().ToLowerInvariant();

    private static string LevelAbbreviation(LogSeverity level) =>
        level.ToString().ToUpperInvariant()[..Math.Min(3, level.ToString().Length)];

    private static string ShortSource(string sourceContext) =>
        sourceContext.Contains('.') ? sourceContext[(sourceContext.LastIndexOf('.') + 1)..] : sourceContext;
}
