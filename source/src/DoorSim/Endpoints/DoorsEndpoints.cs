namespace DoorSim.Endpoints;

public static class DoorsEndpoints
{
    public static IEndpointRouteBuilder MapDoorsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/doors").WithTags("Doors");

        group.MapGet("/serial-ports", async (DoorConfigService svc, int? excludeDoorId) =>
                Results.Ok(await svc.GetAvailableSerialPortsAsync(excludeDoorId)))
            .WithName("GetAvailableSerialPorts")
            .WithSummary("List serial ports available for a new OSDP or Modbus RTU door.")
            .WithDescription("Scans /dev/tty* (ttyRS485*, ttyACM*, ttyAMA*, ttyUSB*, ttyS*) and excludes ports " +
                "already claimed by other doors. Pass excludeDoorId when editing an existing door so its own " +
                "current port isn't excluded from the list.")
            .Produces<string[]>(StatusCodes.Status200OK);

        group.MapGet("/", async (DoorConfigService svc) =>
                Results.Ok(await svc.GetAllAsync()))
            .WithName("GetDoors")
            .WithSummary("List all configured doors.")
            .Produces<DoorConfiguration[]>(StatusCodes.Status200OK);

        group.MapGet("/{id:int}", async (int id, DoorConfigService svc) =>
            {
                var door = await svc.GetAsync(id);
                return door is null
                    ? Results.NotFound()
                    : Results.Ok(door);
            })
            .WithName("GetDoor")
            .WithSummary("Get a single door's configuration by ID.")
            .Produces<DoorConfiguration>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (DoorConfiguration dto, DoorConfigService svc, DynamicSimulatorBank bank) =>
            {
                var (result, error) = await svc.CreateAsync(dto);
                if (error is not null)
                    return Results.BadRequest(error);

                await bank.AddDoor(result!);
                return Results.Created($"/api/doors/{result!.Id}", result);
            })
            .WithName("CreateDoor")
            .WithSummary("Create a new door configuration.")
            .WithDescription("Validates GPIO pin and Modbus channel uniqueness across all doors before saving. " +
                "On success, the running simulator bank immediately starts simulating the new door — no restart " +
                "needed.")
            .Produces<DoorConfiguration>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:int}", async (int id, DoorConfiguration dto, DoorConfigService svc, DynamicSimulatorBank bank) =>
            {
                var (result, error) = await svc.UpdateAsync(id, dto);
                if (error is not null)
                    return Results.BadRequest(error);

                if (result is null)
                    return Results.NotFound();

                await bank.AddDoor(result);
                return Results.Ok(result);
            })
            .WithName("UpdateDoor")
            .WithSummary("Update an existing door's configuration.")
            .WithDescription("Re-validates pin/channel uniqueness and rebuilds the door's simulator in the " +
                "running bank immediately — no restart needed.")
            .Produces<DoorConfiguration>(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", async (int id, DoorConfigService svc, DynamicSimulatorBank bank) =>
            {
                var deleted = await svc.DeleteAsync(id);
                if (!deleted)
                    return Results.NotFound();

                await bank.RemoveDoor(id);
                return Results.NoContent();
            })
            .WithName("DeleteDoor")
            .WithSummary("Delete a door configuration.")
            .WithDescription("Removes the door from the running simulator bank and disposes its simulator " +
                "(releasing the OSDP serial port, if any).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
