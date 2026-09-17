namespace DoorSim.Endpoints;

public static class CardFormatsEndpoints
{
    public static IEndpointRouteBuilder MapCardFormatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/card-formats").WithTags("CardFormats");

        group.MapGet("/", async (CardFormatService service) =>
                Results.Ok(await service.GetAllAsync()))
            .WithName("GetCardFormats")
            .WithSummary("List all user-defined custom card formats.")
            .Produces<CustomCardFormat[]>();

        group.MapGet("/{id:int}", async (int id, CardFormatService service) =>
                await service.GetAsync(id) is { } format
                    ? Results.Ok(format)
                    : Results.NotFound())
            .WithName("GetCardFormat")
            .WithSummary("Get a single custom card format by its ID.")
            .Produces<CustomCardFormat>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CustomCardFormat format, CardFormatService service) =>
            {
                var (created, error) = await service.CreateAsync(format);

                if (error is not null)
                    return Results.BadRequest(error);

                return Results.Created($"/api/card-formats/{created!.Id}", created);
            })
            .WithName("CreateCardFormat")
            .WithSummary("Add a new custom card format.")
            .WithDescription("The submitted Id and CreatedAt are ignored — the server assigns both and returns " +
                             "the created record.")
            .Produces<CustomCardFormat>(StatusCodes.Status201Created)
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:int}", async (int id, CustomCardFormat format, CardFormatService service) =>
            {
                var (updated, error) = await service.UpdateAsync(id, format);

                if (error is not null)
                    return Results.BadRequest(error);

                return updated is not null
                    ? Results.Ok(updated)
                    : Results.NotFound();
            })
            .WithName("UpdateCardFormat")
            .WithSummary("Replace an existing custom card format's mask definition.")
            .Produces<CustomCardFormat>()
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", async (int id, CardFormatService service) =>
            {
                var (deleted, error) = await service.DeleteAsync(id);

                if (error is not null)
                    return Results.BadRequest(error);

                return deleted
                    ? Results.NoContent()
                    : Results.NotFound();
            })
            .WithName("DeleteCardFormat")
            .WithSummary("Remove a custom card format.")
            .WithDescription("Fails with 400 if the format is still referenced by a card in the library.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<string>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
