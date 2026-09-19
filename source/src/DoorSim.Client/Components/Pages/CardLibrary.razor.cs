using DoorSim.Client.Components.Shared;
using MudBlazor;

namespace DoorSim.Client.Components.Pages;

public partial class CardLibrary
{
    private CardEntry[] _cards = [];
    private CustomCardFormat[] _customFormats = [];
    private string? _error;
    private bool _loading = true;

    private static DialogOptions DialogOptions => new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
    };

    protected override async Task OnInitializedAsync()
    {
        _customFormats = await Formats.GetAllAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _cards = await Cards.GetAllAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to load card library: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OpenAddDialogAsync()
    {
        var parameters = new DialogParameters<CardEditDialog>
        {
            { p => p.Editing, null },
        };

        var dialogRef = await Dialog.ShowAsync<CardEditDialog>("Add Card", parameters, DialogOptions);
        var result = await dialogRef.Result;
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenEditDialogAsync(CardEntry card)
    {
        var parameters = new DialogParameters<CardEditDialog>
        {
            { p => p.Editing, card },
        };

        var dialogRef = await Dialog.ShowAsync<CardEditDialog>($"Edit: {card.Label}", parameters, DialogOptions);
        var result = await dialogRef.Result;
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await Cards.DeleteAsync(id);
        await LoadAsync();
    }
}
