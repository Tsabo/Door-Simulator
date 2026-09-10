namespace DoorSim.Services;

/// <summary>
/// Singleton that owns the in-memory copy of global simulation timing settings.
/// Writes go to the database immediately; reads are served from the cache so the
/// orchestrator never has to await a DB call on the hot path.
/// </summary>
public class SimulationSettingsService(
    IServiceScopeFactory scopeFactory,
    ILogger<SimulationSettingsService> logger)
{
    private SimulationTimingSettings _current = new();

    public SimulationTimingSettings Current => _current;

    /// <summary>Load settings from the database on startup. Uses defaults if no row exists.</summary>
    public async Task LoadAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();
        var entity = await db.SimulationSettings.FirstOrDefaultAsync().ConfigureAwait(false);
        if (entity is not null)
        {
            _current = entity.ToDto();
            logger.LogInformation(
                "Simulation timing loaded — CardToDoor={C}ms DoorOpen={D}ms RexLead={R}ms QuickRex={Q}ms",
                _current.CardToDoorDelayMs, _current.DoorOpenMs, _current.RexLeadMs, _current.QuickRexMs);
        }
        else
        {
            logger.LogInformation("No simulation timing row found — using defaults");
        }
    }

    /// <summary>Persist updated settings and refresh the in-memory cache.</summary>
    public async Task<SimulationTimingSettings> UpdateAsync(SimulationTimingSettings dto)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

        var entity = await db.SimulationSettings.FirstOrDefaultAsync().ConfigureAwait(false);
        if (entity is null)
        {
            entity = new SimulationSettingsEntity();
            db.SimulationSettings.Add(entity);
        }

        entity.CardToDoorDelayMs = dto.CardToDoorDelayMs;
        entity.DoorOpenMs        = dto.DoorOpenMs;
        entity.RexLeadMs         = dto.RexLeadMs;
        entity.QuickRexMs        = dto.QuickRexMs;

        await db.SaveChangesAsync().ConfigureAwait(false);
        _current = entity.ToDto();

        logger.LogInformation(
            "Simulation timing updated — CardToDoor={C}ms DoorOpen={D}ms RexLead={R}ms QuickRex={Q}ms",
            _current.CardToDoorDelayMs, _current.DoorOpenMs, _current.RexLeadMs, _current.QuickRexMs);

        return _current;
    }
}
