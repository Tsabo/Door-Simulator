namespace DoorSim.Endpoints;

public static class DoorsEndpoints
{
    public static IEndpointRouteBuilder MapDoorsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/doors");

        group.MapGet("/serial-ports", async (DoorConfigService svc, int? excludeDoorId) =>
            Results.Ok(await svc.GetAvailableSerialPortsAsync(excludeDoorId)));

        group.MapGet("/", async (DoorConfigService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        group.MapGet("/{id:int}", async (int id, DoorConfigService svc) =>
        {
            var door = await svc.GetAsync(id);
            return door is null ? Results.NotFound() : Results.Ok(door);
        });

        group.MapPost("/", async (DoorConfiguration dto, DoorConfigService svc, DynamicSimulatorBank bank) =>
        {
            var (result, error) = await svc.CreateAsync(dto);
            if (error is not null) return Results.BadRequest(error);
            await bank.AddDoor(result!);
            return Results.Created($"/api/doors/{result!.Id}", result);
        });

        group.MapPut("/{id:int}", async (int id, DoorConfiguration dto, DoorConfigService svc, DynamicSimulatorBank bank) =>
        {
            var (result, error) = await svc.UpdateAsync(id, dto);
            if (error is not null) return Results.BadRequest(error);
            if (result is null) return Results.NotFound();
            await bank.AddDoor(result);
            return Results.Ok(result);
        });

        group.MapDelete("/{id:int}", async (int id, DoorConfigService svc, DynamicSimulatorBank bank) =>
        {
            var deleted = await svc.DeleteAsync(id);
            if (!deleted) return Results.NotFound();
            await bank.RemoveDoor(id);
            return Results.NoContent();
        });

        return app;
    }
}
