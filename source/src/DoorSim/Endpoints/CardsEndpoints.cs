namespace DoorSim.Endpoints;

public static class CardsEndpoints
{
    public static IEndpointRouteBuilder MapCardsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards");

        group.MapGet("/", async (CardLibraryService service) =>
            Results.Ok(await service.GetAllAsync()));

        group.MapGet("/{id:int}", async (int id, CardLibraryService service) =>
            await service.GetAsync(id) is { } card
                ? Results.Ok(card)
                : Results.NotFound());

        group.MapPost("/", async (CardEntry card, CardLibraryService service) =>
        {
            var created = await service.CreateAsync(card);

            return Results.Created($"/api/cards/{created.Id}", created);
        });

        group.MapPut("/{id:int}", async (int id, CardEntry card, CardLibraryService service) =>
            await service.UpdateAsync(id, card) is { } updated
                ? Results.Ok(updated)
                : Results.NotFound());

        group.MapDelete("/{id:int}", async (int id, CardLibraryService service) =>
            await service.DeleteAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        return app;
    }
}
