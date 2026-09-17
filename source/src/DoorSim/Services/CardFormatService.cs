using DoorSim.Validation;

namespace DoorSim.Services;

/// <summary>CRUD service for user-defined custom card formats. Scoped — one instance per request.</summary>
public class CardFormatService(DoorSimDbContext db)
{
    public async Task<CustomCardFormat[]> GetAllAsync() =>
        await db.CardFormats
            .OrderBy(p => p.Name)
            .Select(p => p.ToDto())
            .ToArrayAsync()
            .ConfigureAwait(false);

    public async Task<CustomCardFormat?> GetAsync(int id)
    {
        var entity = await db.CardFormats.FindAsync(id).ConfigureAwait(false);

        return entity?.ToDto();
    }

    public async Task<(CustomCardFormat? Result, string? Error)> CreateAsync(CustomCardFormat dto)
    {
        var validationError = CardFormatValidation.ValidateDefinition(dto);

        if (validationError is not null)
            return (null, validationError);

        var entity = CardFormatEntity.FromDto(dto);
        entity.Id = 0;
        entity.CreatedAt = DateTimeOffset.UtcNow;

        db.CardFormats.Add(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return (entity.ToDto(), null);
    }

    public async Task<(CustomCardFormat? Result, string? Error)> UpdateAsync(int id, CustomCardFormat dto)
    {
        var entity = await db.CardFormats.FindAsync(id).ConfigureAwait(false);

        if (entity is null)
            return (null, null);

        var validationError = CardFormatValidation.ValidateDefinition(dto);

        if (validationError is not null)
            return (null, validationError);

        entity.Name = dto.Name;
        entity.CardMask = dto.CardMask;
        entity.Parity1Mask = dto.Parity1Mask;
        entity.Parity2Mask = dto.Parity2Mask;
        entity.Parity3Mask = dto.Parity3Mask;

        await db.SaveChangesAsync().ConfigureAwait(false);

        return (entity.ToDto(), null);
    }

    /// <summary>
    /// Deletes a format. Returns <c>(false, null)</c> if it doesn't exist, and
    /// <c>(false, error)</c> if it's still referenced by a card in the library.
    /// </summary>
    public async Task<(bool Deleted, string? Error)> DeleteAsync(int id)
    {
        var entity = await db.CardFormats.FindAsync(id).ConfigureAwait(false);

        if (entity is null)
            return (false, null);

        if (await db.Cards.AnyAsync(p => p.CustomFormatId == id).ConfigureAwait(false))
            return (false, $"Custom format {id} is still in use by one or more library cards.");

        db.CardFormats.Remove(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return (true, null);
    }
}
