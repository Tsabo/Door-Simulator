namespace DoorSim.Services;

/// <summary>CRUD service for the card library. Scoped — one instance per request.</summary>
public class CardLibraryService(DoorSimDbContext db)
{
    public async Task<CardEntry[]> GetAllAsync() =>
        await db.Cards
            .OrderBy(c => c.Label)
            .Select(c => c.ToDto())
            .ToArrayAsync()
            .ConfigureAwait(false);

    public async Task<CardEntry?> GetAsync(int id)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);
        return entity?.ToDto();
    }

    public async Task<CardEntry> CreateAsync(CardEntry dto)
    {
        var entity = CardEntity.FromDto(dto);
        entity.Id = 0;
        entity.CreatedAt = DateTimeOffset.UtcNow;

        db.Cards.Add(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);

        return entity.ToDto();
    }

    public async Task<CardEntry?> UpdateAsync(int id, CardEntry dto)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        entity.Label = dto.Label;
        entity.FacilityCode = dto.FacilityCode;
        entity.CardNumber = dto.CardNumber;
        entity.Format = dto.Format;

        await db.SaveChangesAsync().ConfigureAwait(false);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await db.Cards.FindAsync(id).ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        db.Cards.Remove(entity);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }
}
