namespace DoorSim.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("/simulation", (SimulationSettingsService svc) =>
            Results.Ok(svc.Current));

        group.MapPut("/simulation", async (SimulationTimingSettings dto, SimulationSettingsService svc) =>
            Results.Ok(await svc.UpdateAsync(dto)));

        return app;
    }
}
