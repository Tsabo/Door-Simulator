namespace DoorSim.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/simulation", (SimulationSettingsService svc) =>
                Results.Ok(svc.Current))
            .WithName("GetSimulationSettings")
            .WithSummary("Get the current simulation timing settings.")
            .WithDescription("Covers card-to-door delay, door-open duration, REX lead time, and quick-REX " +
                "duration used by the /api/simulate endpoints.")
            .Produces<SimulationTimingSettings>(StatusCodes.Status200OK);

        group.MapPut("/simulation", async (SimulationTimingSettings dto, SimulationSettingsService svc) =>
                Results.Ok(await svc.UpdateAsync(dto)))
            .WithName("UpdateSimulationSettings")
            .WithSummary("Update simulation timing settings.")
            .WithDescription("Applies immediately to all subsequent simulated events — no restart required.")
            .Produces<SimulationTimingSettings>(StatusCodes.Status200OK);

        return app;
    }
}
