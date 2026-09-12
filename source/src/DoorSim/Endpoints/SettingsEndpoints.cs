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
            .Produces<SimulationTimingSettings>();

        group.MapPut("/simulation", async (SimulationTimingSettings dto, SimulationSettingsService svc) =>
            {
                var (result, error) = await svc.UpdateAsync(dto);
                if (error is not null)
                    return Results.BadRequest(error);

                return Results.Ok(result);
            })
            .WithName("UpdateSimulationSettings")
            .WithSummary("Update simulation timing settings.")
            .WithDescription("Applies immediately to all subsequent simulated events — no restart required.")
            .Produces<SimulationTimingSettings>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        return app;
    }
}
