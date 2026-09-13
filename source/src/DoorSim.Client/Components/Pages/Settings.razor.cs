namespace DoorSim.Client.Components.Pages;

public partial class Settings
{
    private string? _error;
    private TimingFormModel _form = new();
    private bool _loading = true;
    private bool _saved;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        var current = await SettingsClient.GetTimingAsync();
        _form = new TimingFormModel
        {
            CardToDoorDelayMs = current.CardToDoorDelayMs,
            DoorOpenMs = current.DoorOpenMs,
            RexLeadMs = current.RexLeadMs,
            QuickRexMs = current.QuickRexMs,
            MetricsRetentionDays = current.MetricsRetentionDays,
        };

        _loading = false;
    }

    private async Task SaveAsync()
    {
        _saving = true;
        _saved = false;
        _error = null;

        var dto = new SimulationTimingSettings(
            _form.CardToDoorDelayMs,
            _form.DoorOpenMs,
            _form.RexLeadMs,
            _form.QuickRexMs,
            _form.MetricsRetentionDays);

        var (_, error) = await SettingsClient.UpdateTimingAsync(dto);
        if (error is not null)
            _error = error;
        else
            _saved = true;

        _saving = false;
    }

    private sealed class TimingFormModel
    {
        public int CardToDoorDelayMs { get; set; } = 500;
        public int DoorOpenMs { get; set; } = 5000;
        public int RexLeadMs { get; set; } = 500;
        public int QuickRexMs { get; set; } = 2000;
        public int MetricsRetentionDays { get; set; } = 30;
    }
}
