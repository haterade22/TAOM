using TAOM.Adapters;

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

    /// <summary>
    /// Logs every hero whose character is hollow, with the facts that name how it got there, and writes nothing to
    /// the character. Silent on a healthy load.
    /// </summary>
    void ReportHollowHeroCharacters();

    /// <summary>
    /// Repairs a parent whose character is hollow, just before a birth clones it (crash bundle 3fbdc0c5). Silent for a
    /// healthy parent. Never throws. True when this call repaired the parent.
    /// </summary>
    bool RepairOffspringParent(IHeroCharacterStateAdapter parent);

    /// <summary>
    /// Writes the line that says whether a birth finished after a parent needed the repair: a pass, or a fail when
    /// <paramref name="failure"/> is set.
    /// </summary>
    void ReportBirthAfterRepair(string childId, System.Exception failure);
}
