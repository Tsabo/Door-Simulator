using DoorSim.Hosting;

namespace DoorSim.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").WithTags("System");

        group.MapGet("/info", (HostCapabilities capabilities) =>
                Results.Ok(GetSystemInfo(capabilities)))
            .WithName("GetSystemInfo")
            .WithSummary("Get the server version and host hardware capabilities.")
            .WithDescription("When gpioAvailable is false, Wiegand doors and GPIO DPS/REX pins are rejected — " +
                             "use OSDP for card reads and Modbus for door contacts.")
            .Produces<SystemInfo>();

        return app;
    }

    internal static SystemInfo GetSystemInfo(HostCapabilities capabilities) => new(
        AppVersion.Display,
        RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        capabilities.GpioAvailable);
}
