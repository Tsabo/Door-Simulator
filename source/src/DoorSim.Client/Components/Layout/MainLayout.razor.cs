namespace DoorSim.Client.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen;

    private void DrawerToggle() => _drawerOpen = !_drawerOpen;
}
