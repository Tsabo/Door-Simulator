using MudBlazor;

namespace DoorSim.Client.Components.Shared;

public partial class CardEditDialog
{
    private static readonly FormatChoice BuiltInHeader = new(WiegandFormat.Wiegand26, -1);
    private static readonly FormatChoice CustomHeader = new(WiegandFormat.Custom, -2);
    private CustomCardFormat[] _customFormats = [];
    private string? _error;
    private CardFormModel _form = new();
    private bool _saving;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public CardEntry? Editing { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _customFormats = await Formats.GetAllAsync();

        if (Editing is { } card)
        {
            _form = new CardFormModel
            {
                Label = card.Label,
                FacilityCode = card.FacilityCode,
                CardNumber = card.CardNumber,
                Format = card.Format,
                CustomFormatId = card.CustomFormatId,
            };
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_form.Label))
        {
            _error = "Label is required.";

            return;
        }

        _saving = true;
        _error = null;

        try
        {
            string? error;

            if (Editing is null)
            {
                var newCard = new CardEntry(0, _form.Label.Trim(), (ushort)_form.FacilityCode,
                    _form.CardNumber, _form.Format, DateTimeOffset.UtcNow, _form.CustomFormatId);

                (_, error) = await Cards.CreateAsync(newCard);
            }
            else
            {
                var updated = Editing with
                {
                    Label = _form.Label.Trim(),
                    FacilityCode = (ushort)_form.FacilityCode,
                    CardNumber = _form.CardNumber,
                    Format = _form.Format,
                    CustomFormatId = _form.CustomFormatId,
                };

                (_, error) = await Cards.UpdateAsync(Editing.Id, updated);
            }

            if (error is not null)
            {
                _error = error;

                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private string DescribeChoice(FormatChoice choice) =>
        choice.CustomFormatId is > 0
            ? _customFormats.FirstOrDefault(p => p.Id == choice.CustomFormatId)?.Name ?? "Custom"
            : choice.Format.ToString();

    private readonly record struct FormatChoice(WiegandFormat Format, int? CustomFormatId);

    private sealed class CardFormModel
    {
        public string Label { get; set; } = string.Empty;
        public int FacilityCode { get; set; }
        public uint CardNumber { get; set; }
        public WiegandFormat Format { get; set; } = WiegandFormat.Wiegand26;
        public int? CustomFormatId { get; set; }

        public FormatChoice Choice
        {
            get => new(Format, CustomFormatId);
            set
            {
                Format = value.Format;
                CustomFormatId = value.CustomFormatId;
            }
        }
    }
}
