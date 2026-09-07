namespace TAOM.Features.StaleCharacterRepair;

/// <summary>
/// Makes a save-restored character whose ModuleData definition is gone INERT, so the engine's
/// several unguarded dereferences of its null fields cannot fire. See
/// <see cref="StaleCharacterRepairService"/> for the chain and for what this deliberately does not
/// fix.
/// </summary>
public interface IStaleCharacterRepairService
{
    /// <summary>
    /// Repairs every stale character and returns how many were repaired. Zero on every healthy
    /// load, which is the normal case and is completely silent. A non-zero result is logged with
    /// the ids, because the repair keeps the save loadable while hiding a data defect.
    /// </summary>
    int RepairStaleCharacters();
}
