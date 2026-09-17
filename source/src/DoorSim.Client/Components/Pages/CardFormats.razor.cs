using DoorSim.Client.Components.Shared;
using MudBlazor;

namespace DoorSim.Client.Components.Pages;

/// <summary>
/// Editor and test bench for <see cref="CustomCardFormat" /> definitions. Single source of truth
/// for the page: the child components are presentational and report edits back here.
/// </summary>
public partial class CardFormats
{
    private static readonly string[] DefaultMasks = MasksOf(CardFormatCatalog.Default);

    private FormatAnalysis _analysis = CardFormatAnalyzer.Analyze(DefaultMasks);
    private int _bits = CardFormatCatalog.Default.BitCount;
    private string _cardIn = "12345";
    private DecodeResult? _decoded;
    private string? _error;
    private string _facilityIn = "100";
    private ulong _frame;
    private string? _hex;
    private string _hexIn = "";
    private CustomCardFormat[] _library = [];
    private bool _loading = true;
    private string[] _masks = [.. DefaultMasks];
    private string _name = "";
    private bool _roundTripOk;
    private bool _saved;
    private bool _saving;
    private int _selectedId = -1;
    private char _tool = 'C';

    protected override async Task OnInitializedAsync()
    {
        await LoadLibraryAsync();
        Recompute();
        _loading = false;
    }

    private async Task LoadLibraryAsync() => _library = await Formats.GetAllAsync();

    /// <summary>
    /// Re-derives everything the bench shows. Called from each mutating handler rather than from
    /// the render path, since nothing else changes the inputs.
    /// </summary>
    private void Recompute()
    {
        var cardIn = ParseValue(_cardIn);
        var facilityIn = ParseValue(_facilityIn);

        _analysis = CardFormatAnalyzer.Analyze(_masks, cardIn, facilityIn, _hexIn);

        // The encoder is deliberately tolerant, but it still refuses a mask set it can't encode,
        // and the bench must keep rendering while the user gets there.
        CardFormatEncoder? encoder = null;

        try
        {
            encoder = new CardFormatEncoder(_masks[0], NullIfBlank(_masks[1]), NullIfBlank(_masks[2]), NullIfBlank(_masks[3]));
        }
        catch (ArgumentException)
        {
            // Left null: the issue list already explains why.
        }

        _hex = null;
        _frame = 0;
        _roundTripOk = false;
        _decoded = null;

        if (encoder is null)
            return;

        try
        {
            _frame = encoder.Encode(cardIn, facilityIn);
            _hex = encoder.EncodeHex(cardIn, facilityIn);

            var (card, facility, parityValid) = encoder.DecodeWide(_frame);
            _roundTripOk = parityValid && card == cardIn && facility == facilityIn;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Test value overflows its field; the issue list reports it as an error.
        }

        if (CardFormatEncoder.TryParseHex(_hexIn, out var bits))
        {
            var (card, facility, _) = encoder.DecodeWide(bits);
            _decoded = new DecodeResult(card, facility, encoder.CheckParity(bits));
        }
    }

    private void OnMasksChanged(string[] masks)
    {
        _masks = masks;
        Recompute();
    }

    private void OnBitsChanged(int bits)
    {
        _bits = Math.Clamp(bits, 4, CardFormatEncoder.SentinelTotalBits);

        for (var i = 0; i < _masks.Length; i++)
            _masks[i] = CardFormatAnalyzer.PadMask(_masks[i], _bits);

        Recompute();
    }

    private void OnCardInChanged(string value)
    {
        _cardIn = DigitsOnly(value);
        Recompute();
    }

    private void OnFacilityInChanged(string value)
    {
        _facilityIn = DigitsOnly(value);
        Recompute();
    }

    private void OnHexInChanged(string value)
    {
        _hexIn = (value ?? "").Trim();
        Recompute();
    }

    /// <summary>
    /// Loads a built-in format as an unsaved draft. The catalogue is read-only reference data, so
    /// this deliberately leaves the selection cleared: saving creates the user's own copy rather
    /// than editing anything shared.
    /// </summary>
    private void LoadTemplate(CardFormatTemplate template)
    {
        Load(template.ToFormat());
        _selectedId = -1;
    }

    /// <summary>The four mask rows of a template, padded to its own bit count.</summary>
    private static string[] MasksOf(CardFormatTemplate template) =>
    [
        CardFormatAnalyzer.PadMask(template.CardMask, template.BitCount),
        CardFormatAnalyzer.PadMask(template.Parity1Mask, template.BitCount),
        CardFormatAnalyzer.PadMask(template.Parity2Mask, template.BitCount),
        CardFormatAnalyzer.PadMask(template.Parity3Mask, template.BitCount)
    ];

    private void Load(CustomCardFormat format)
    {
        _selectedId = format.Id;
        _name = format.Name;
        _bits = format.CardMask.Length;
        _masks =
        [
            CardFormatAnalyzer.PadMask(format.CardMask, _bits),
            CardFormatAnalyzer.PadMask(format.Parity1Mask, _bits),
            CardFormatAnalyzer.PadMask(format.Parity2Mask, _bits),
            CardFormatAnalyzer.PadMask(format.Parity3Mask, _bits)
        ];

        ClampBenchValues();

        _error = null;
        _saved = false;
        Recompute();
    }

    /// <summary>
    /// Brings the bench test values inside the newly loaded format's field widths. Without this,
    /// loading a format with no facility-code bits while the bench still holds a facility code from
    /// the previous format reports the format itself as invalid — which is misleading, since the
    /// format is fine and only the leftover test value is wrong.
    /// </summary>
    private void ClampBenchValues()
    {
        var analysis = CardFormatAnalyzer.Analyze(_masks);

        _cardIn = Clamp(_cardIn, analysis.MaxCardNumber);
        _facilityIn = Clamp(_facilityIn, analysis.MaxFacilityCode);
    }

    private static string Clamp(string value, ulong max) =>
        ParseValue(value) > max
            ? max.ToString(CultureInfo.InvariantCulture)
            : value;

    private void NewFormat()
    {
        _selectedId = -1;
        _name = "";

        for (var i = 0; i < _masks.Length; i++)
            _masks[i] = new string('X', _bits);

        _error = null;
        _saved = false;
        Recompute();
    }

    private void Revert()
    {
        var selected = _library.FirstOrDefault(p => p.Id == _selectedId);

        if (selected is not null)
            Load(selected);
    }

    private async Task SaveAsync()
    {
        _error = null;
        _saved = false;

        if (string.IsNullOrWhiteSpace(_name))
        {
            _error = "Format name is required.";
            return;
        }

        _saving = true;

        try
        {
            var dto = new CustomCardFormat(
                Math.Max(_selectedId, 0),
                _name.Trim(),
                _masks[0],
                NullIfBlank(_masks[1]),
                NullIfBlank(_masks[2]),
                NullIfBlank(_masks[3]),
                DateTimeOffset.UtcNow);

            // The API has no upsert, so resolve it here: an explicit selection updates, and
            // otherwise a name collision updates that entry rather than creating a duplicate.
            var target = _selectedId >= 0
                ? _selectedId
                : _library.FirstOrDefault(p => string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase))?.Id ?? -1;

            var (result, error) = target >= 0
                ? await Formats.UpdateAsync(target, dto with { Id = target })
                : await Formats.CreateAsync(dto);

            if (error is not null)
            {
                _error = error;
                return;
            }

            await LoadLibraryAsync();

            if (result is not null)
                _selectedId = result.Id;

            _saved = true;
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
        _error = null;
        _saved = false;

        var (deleted, error) = await Formats.DeleteAsync(id);

        if (error is not null)
        {
            _error = error;
            return;
        }

        if (_selectedId == id)
            NewFormat();

        await LoadLibraryAsync();
    }

    private static string? NullIfBlank(string mask) =>
        mask.All(c => c == 'X') ? null : mask;

    private static string DigitsOnly(string? value) =>
        new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    private static ulong ParseValue(string value) =>
        ulong.TryParse(value, out var parsed) ? parsed : 0;
}
