using Microsoft.JSInterop;
using MudBlazor;

namespace DoorSim.Client.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen;
    private string? _version;

    private MudThemeProvider _themeProvider = null!;
    private ThemeMode _themeMode;
    private bool _isDarkMode;

    protected override async Task OnInitializedAsync()
    {
        // index.html's inline script has already resolved the stored mode onto <html data-theme>
        // before first paint. Reading it synchronously here means the first Blazor render uses the
        // same palette as the boot loader, with no light-then-dark flash at hand-off.
        var js = (IJSInProcessRuntime)JS;
        _themeMode = Enum.TryParse<ThemeMode>(js.Invoke<string>("doorsimTheme.getMode"), true, out var mode)
            ? mode
            : ThemeMode.System;
        _isDarkMode = js.Invoke<bool>("doorsimTheme.isDark");

        _version = (await SystemClient.GetInfoAsync())?.Version;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await _themeProvider.WatchSystemDarkModeAsync(OnSystemDarkModeChanged);
    }

    private void DrawerToggle() => _drawerOpen = !_drawerOpen;

    private async Task CycleThemeModeAsync()
    {
        _themeMode = _themeMode switch
        {
            ThemeMode.System => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            var _ => ThemeMode.System,
        };
        await JS.InvokeVoidAsync("doorsimTheme.setMode", _themeMode.ToString().ToLowerInvariant());

        await SetDarkModeAsync(_themeMode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            var _ => await _themeProvider.GetSystemDarkModeAsync(),
        });
    }

    private async Task OnSystemDarkModeChanged(bool isDark)
    {
        if (_themeMode != ThemeMode.System)
            return;

        await SetDarkModeAsync(isDark);
        StateHasChanged();
    }

    // Keeps <html data-theme> in step with MudBlazor's palette; custom CSS keys off the attribute.
    private async Task SetDarkModeAsync(bool isDark)
    {
        _isDarkMode = isDark;
        await JS.InvokeVoidAsync("doorsimTheme.apply", isDark);
    }

    private string ThemeModeIcon => _themeMode switch
    {
        ThemeMode.Light => Icons.Material.Filled.LightMode,
        ThemeMode.Dark => Icons.Material.Filled.DarkMode,
        var _ => Icons.Material.Filled.BrightnessAuto,
    };
}
