namespace DoorSim.Endpoints;

public static class CardsEndpoints
{
    public static IEndpointRouteBuilder MapCardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards").WithTags("Cards");

        group.MapGet("/", async (CardLibraryService service) =>
                Results.Ok(await service.GetAllAsync()))
            .WithName("GetCards")
            .WithSummary("List all cards in the card library.")
            .Produces<CardEntry[]>(StatusCodes.Status200OK);

        group.MapGet("/{id:int}", async (int id, CardLibraryService service) =>
                await service.GetAsync(id) is { } card
                    ? Results.Ok(card)
                    : Results.NotFound())
            .WithName("GetCard")
            .WithSummary("Get a single card by its library ID.")
            .Produces<CardEntry>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CardEntry card, CardLibraryService service) =>
            {
                var created = await service.CreateAsync(card);

                return Results.Created($"/api/cards/{created.Id}", created);
            })
            .WithName("CreateCard")
            .WithSummary("Add a new card to the library.")
            .WithDescription("The submitted Id and CreatedAt are ignored — the server assigns both and returns " +
                "the created record.")
            .Produces<CardEntry>(StatusCodes.Status201Created);

        group.MapPut("/{id:int}", async (int id, CardEntry card, CardLibraryService service) =>
                await service.UpdateAsync(id, card) is { } updated
                    ? Results.Ok(updated)
                    : Results.NotFound())
            .WithName("UpdateCard")
            .WithSummary("Replace an existing card's details.")
            .Produces<CardEntry>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", async (int id, CardLibraryService service) =>
                await service.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound())
            .WithName("DeleteCard")
            .WithSummary("Remove a card from the library.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
