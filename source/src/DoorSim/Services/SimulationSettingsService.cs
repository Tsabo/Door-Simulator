using DoorSim.Validation;

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
    public SimulationTimingSettings Current { get; private set; } = new();

    /// <summary>Load settings from the database on startup. Uses defaults if no row exists.</summary>
    public async Task LoadAsync()
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();
        var entity = await db.SimulationSettings.FirstOrDefaultAsync().ConfigureAwait(false);
        if (entity is not null)
        {
            Current = entity.ToDto();
            logger.LogInformation(
                "Simulation timing loaded — CardToDoor={C}ms DoorOpen={D}ms RexLead={R}ms QuickRex={Q}ms",
                Current.CardToDoorDelayMs, Current.DoorOpenMs, Current.RexLeadMs, Current.QuickRexMs);
        }
        else
            logger.LogInformation("No simulation timing row found — using defaults");
    }

    /// <summary>Persist updated settings and refresh the in-memory cache.</summary>
    public async Task<(SimulationTimingSettings? Result, string? Error)> UpdateAsync(SimulationTimingSettings dto)
    {
        var validationError = TimingValidation.Validate(dto);
        if (validationError is not null)
            return (null, validationError);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

        var entity = await db.SimulationSettings.FirstOrDefaultAsync().ConfigureAwait(false);
        if (entity is null)
        {
            entity = new SimulationSettingsEntity();
            db.SimulationSettings.Add(entity);
        }

        entity.CardToDoorDelayMs = dto.CardToDoorDelayMs;
        entity.DoorOpenMs = dto.DoorOpenMs;
        entity.RexLeadMs = dto.RexLeadMs;
        entity.QuickRexMs = dto.QuickRexMs;
        entity.MetricsRetentionDays = dto.MetricsRetentionDays;

        await db.SaveChangesAsync().ConfigureAwait(false);
        Current = entity.ToDto();

        logger.LogInformation(
            "Simulation timing updated — CardToDoor={C}ms DoorOpen={D}ms RexLead={R}ms QuickRex={Q}ms",
            Current.CardToDoorDelayMs, Current.DoorOpenMs, Current.RexLeadMs, Current.QuickRexMs);

        return (Current, null);
    }
}
