namespace DoorSim.Client.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen;
    private string? _version;

    protected override async Task OnInitializedAsync() =>
        _version = (await SystemClient.GetInfoAsync())?.Version;

    private void DrawerToggle() => _drawerOpen = !_drawerOpen;
}
