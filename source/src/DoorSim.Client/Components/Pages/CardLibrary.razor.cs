namespace DoorSim.Client.Components.Pages;

public partial class CardLibrary
{
    private CardEntry[] _cards = [];
    private CardEntry? _editing;
    private string? _error;
    private CardFormModel _form = new();
    private bool _loading = true;
    private bool _saving;

    protected override async Task OnInitializedAsync() => await LoadAsync();

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
            if (_editing is null)
            {
                var newCard = new CardEntry(0, _form.Label, (ushort)_form.FacilityCode,
                    _form.CardNumber, _form.Format, DateTimeOffset.UtcNow);

                await Cards.CreateAsync(newCard);
            }
            else
            {
                var updated = _editing with
                {
                    Label = _form.Label,
                    FacilityCode = (ushort)_form.FacilityCode,
                    CardNumber = _form.CardNumber,
                    Format = _form.Format,
                };

                await Cards.UpdateAsync(_editing.Id, updated);
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

    private sealed class CardFormModel
    {
        public string Label { get; set; } = string.Empty;
        public int FacilityCode { get; set; }
        public uint CardNumber { get; set; }
        public WiegandFormat Format { get; set; } = WiegandFormat.Wiegand26;
    }
}
