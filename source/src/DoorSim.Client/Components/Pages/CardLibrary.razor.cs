namespace DoorSim.Client.Components.Pages;

public partial class CardLibrary
{
    private CardEntry[] _cards = [];
    private CustomCardFormat[] _customFormats = [];
    private CardEntry? _editing;
    private string? _error;
    private CardFormModel _form = new();
    private bool _loading = true;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        _customFormats = await Formats.GetAllAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _cards = await Cards.GetAllAsync();
        _loading = false;
    }

    private void BeginEdit(CardEntry card)
    {
        _editing = card;
        _form = new CardFormModel
        {
            Label = card.Label,
            FacilityCode = card.FacilityCode,
            CardNumber = card.CardNumber,
            Format = card.Format,
            CustomFormatId = card.CustomFormatId
        };

        _error = null;
    }

    private void CancelEdit()
    {
        _editing = null;
        _form = new CardFormModel();
        _error = null;
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

            if (_editing is null)
            {
                var newCard = new CardEntry(0, _form.Label, (ushort)_form.FacilityCode,
                    _form.CardNumber, _form.Format, DateTimeOffset.UtcNow, _form.CustomFormatId);

                (_, error) = await Cards.CreateAsync(newCard);
            }
            else
            {
                var updated = _editing with
                {
                    Label = _form.Label,
                    FacilityCode = (ushort)_form.FacilityCode,
                    CardNumber = _form.CardNumber,
                    Format = _form.Format,
                    CustomFormatId = _form.CustomFormatId
                };

                (_, error) = await Cards.UpdateAsync(_editing.Id, updated);
            }

            if (error is not null)
            {
                _error = error;
                return;
            }

            _editing = null;
            _form = new CardFormModel();
            await LoadAsync();
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

    private async Task DeleteAsync(int id)
    {
        await Cards.DeleteAsync(id);
        await LoadAsync();
    }

    private string DescribeChoice(FormatChoice choice) =>
        choice.CustomFormatId is > 0
            ? _customFormats.FirstOrDefault(f => f.Id == choice.CustomFormatId)?.Name ?? "Custom"
            : choice.Format.ToString();

    private static readonly FormatChoice BuiltInHeader = new(WiegandFormat.Wiegand26, -1);
    private static readonly FormatChoice CustomHeader = new(WiegandFormat.Custom, -2);

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
