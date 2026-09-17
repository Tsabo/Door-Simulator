using DoorSim.Validation;

namespace DoorSim.Services;

/// <summary>CRUD service for the card library. Scoped — one instance per request.</summary>
public class CardLibraryService(DoorSimDbContext db)
{
    public async Task<CardEntry[]> GetAllAsync() =>
        await db.Cards
            .OrderBy(p => p.Label)
            .Select(p => p.ToDto())
            .ToArrayAsync()
            .ConfigureAwait(false);

    public async Task<CardEntry?> GetAsync(int id)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);

        return entity?.ToDto();
    }

    public async Task<(CardEntry? Result, string? Error)> CreateAsync(CardEntry dto)
    {
        var validationError = CardValidation.Validate(dto);

        if (validationError is not null)
            return (null, validationError);

        if (dto.Format == WiegandFormat.Custom)
        {
            var customFormatError = await ValidateCustomFormatAsync(dto).ConfigureAwait(false);

            if (customFormatError is not null)
                return (null, customFormatError);
        }

        var entity = CardEntity.FromDto(dto);
        entity.Id = 0;
        entity.CreatedAt = DateTimeOffset.UtcNow;

        db.Cards.Add(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return (entity.ToDto(), null);
    }

    public async Task<(CardEntry? Result, string? Error)> UpdateAsync(int id, CardEntry dto)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);

        if (entity is null)
            return (null, null);

        var validationError = CardValidation.Validate(dto);

        if (validationError is not null)
            return (null, validationError);

        if (dto.Format == WiegandFormat.Custom)
        {
            var customFormatError = await ValidateCustomFormatAsync(dto).ConfigureAwait(false);

            if (customFormatError is not null)
                return (null, customFormatError);
        }

        entity.Label = dto.Label;
        entity.FacilityCode = dto.FacilityCode;
        entity.CardNumber = dto.CardNumber;
        entity.Format = dto.Format;
        entity.CustomFormatId = dto.CustomFormatId;

        await db.SaveChangesAsync().ConfigureAwait(false);

        return (entity.ToDto(), null);
    }

    /// <summary>
    /// Loads the referenced custom format and range-checks the card's credential against it —
    /// the plain presence check in <see cref="CardValidation.Validate(CardEntry)" /> can't do this
    /// since the format definition lives in the database.
    /// </summary>
    private async Task<string?> ValidateCustomFormatAsync(CardEntry dto)
    {
        var formatEntity = await db.CardFormats.FindAsync(dto.CustomFormatId!.Value).ConfigureAwait(false);

        if (formatEntity is null)
            return $"Custom format {dto.CustomFormatId} not found.";

        return CardFormatValidation.ValidateCredential(dto.CardNumber, dto.FacilityCode, formatEntity.ToDto());
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);

        if (entity is null)
            return false;

        db.Cards.Remove(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return true;
    }
}
