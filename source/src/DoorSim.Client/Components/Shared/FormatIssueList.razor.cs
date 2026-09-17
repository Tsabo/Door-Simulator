using MudBlazor;

namespace DoorSim.Client.Components.Shared;

/// <summary>Renders a <see cref="FormatAnalysis" />'s issues with an overall verdict chip.</summary>
public partial class FormatIssueList
{
    [Parameter]
    [EditorRequired]
    public FormatAnalysis Analysis { get; set; } = null!;

    private Color VerdictColor => LevelColor(Analysis.Verdict);

    private string VerdictLabel => Analysis.Verdict switch
    {
        IssueLevel.Error => "invalid",
        IssueLevel.Warn => "check",
        _ => "valid"
    };

    private static Color LevelColor(IssueLevel level) => level switch
    {
        IssueLevel.Error => Color.Error,
        IssueLevel.Warn => Color.Warning,
        IssueLevel.Notice => Color.Info,
        _ => Color.Success
    };

    private static string LevelLabel(IssueLevel level) => level switch
    {
        IssueLevel.Error => "ERROR",
        IssueLevel.Warn => "WARN",
        IssueLevel.Notice => "NOTICE",
        _ => "OK"
    };
}
