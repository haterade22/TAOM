using System.Collections.Generic;

namespace TAOM.Features.BanditManagement;

/// <summary>
/// One flattened hideout roster element as the assault split sees it, in roster order. Built at
/// the Harmony boundary from <c>FlattenedTroopRosterElement</c> so the service never touches an
/// engine type.
/// </summary>
public readonly struct HideoutTroopCandidate
{
    public HideoutTroopCandidate(int level, bool isHeroOrBoss, bool isWounded)
    {
        Level = level;
        IsHeroOrBoss = isHeroOrBoss;
        IsWounded = isWounded;
    }

    public int Level { get; }

    /// <summary><c>Troop.IsHero</c> or <c>Troop.Culture.BanditBoss == Troop</c>: vanilla holds every one of these back for the boss phase.</summary>
    public bool IsHeroOrBoss { get; }

    public bool IsWounded { get; }
}

/// <summary>The assault-route split: who fights in the camp (phase 1) and how many stand with the boss (phase 2).</summary>
public sealed class HideoutAssaultSplit
{
    public HideoutAssaultSplit(int firstPhaseTroopCount, int bossPhaseTroopCount, IReadOnlyList<int> firstPhaseIndices)
    {
        FirstPhaseTroopCount = firstPhaseTroopCount;
        BossPhaseTroopCount = bossPhaseTroopCount;
        FirstPhaseIndices = firstPhaseIndices;
    }

    /// <summary>Written to <c>GetPriorityListForHideoutMission</c>'s <c>out</c> parameter; the mission spawns exactly this many in phase 1.</summary>
    public int FirstPhaseTroopCount { get; }

    /// <summary>Healthy total minus <see cref="FirstPhaseTroopCount"/>: the boss plus his bodyguards.</summary>
    public int BossPhaseTroopCount { get; }

    /// <summary>
    /// Ascending indices (into the input) of the healthy regulars that make up the phase-1 priority
    /// list. Shorter than <see cref="FirstPhaseTroopCount"/> only when heroes and bosses outnumber
    /// the phase-2 budget; the engine then fills phase 1 from its non-priority heroes, which is
    /// vanilla's own shape for that case.
    /// </summary>
    public IReadOnlyList<int> FirstPhaseIndices { get; }
}

/// <summary>The sneak-in route selection: which unspawned troops stand with the boss.</summary>
public sealed class HideoutAmbushSelection
{
    public HideoutAmbushSelection(int spawnCount, IReadOnlyList<int> keepIndices)
    {
        SpawnCount = spawnCount;
        KeepIndices = keepIndices;
    }

    /// <summary>Written to <c>SpawnRemainingTroopsForBossFight</c>'s <c>spawnCount</c>; vanilla pads the kept list up to it.</summary>
    public int SpawnCount { get; }

    /// <summary>Ascending indices (into the input) of the troops kept for the boss fight.</summary>
    public IReadOnlyList<int> KeepIndices { get; }
}

/// <summary>
/// Sizes the hideout boss fight (#564): exactly one boss plus <see cref="BodyguardCount"/>
/// soldiers on both the daytime assault and the night sneak-in.
///
/// The engine sizes that fight from the whole hideout population, not from the boss party
/// template: the assault route holds back <c>total - min(floor(0.8 * total), FirstFightMax)</c>
/// troops (<c>MapEventHelper.GetPriorityListForHideoutMission</c>), and the sneak-in route spawns
/// every troop not used as a sentry (<c>HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight</c>,
/// whose <c>Clamp(pop / 2, 4, 20)</c> is a floor). Both Patch86 prefixes and
/// <c>TaomBanditDensityModel.NumberOfMaximumTroopCountForBossFightInHideout</c> relay the numbers
/// computed here. Pure: ints and index lists in, ints and index lists out.
/// </summary>
public interface IHideoutBossFightService
{
    /// <summary>MCM <c>BanditHideoutBossBodyguards</c>, read live, clamped to <c>[0, 10]</c>.</summary>
    int BodyguardCount { get; }

    /// <summary><c>1 + BodyguardCount</c>: the campaign-side boss-phase cap the hideout is trimmed against.</summary>
    int BossPhaseTroopCap { get; }

    /// <summary>
    /// Assault route. The wounded are in neither phase; every healthy hero or culture boss goes to
    /// the boss phase; the highest-level regulars fill the remaining bodyguard slots (roster order
    /// on ties); everything else is phase 1. The boss phase is never empty while a healthy troop
    /// exists, and phase 1 is never negative.
    /// </summary>
    HideoutAssaultSplit PlanAssault(IReadOnlyList<HideoutTroopCandidate> troops);

    /// <summary>
    /// Sneak-in route. Keeps the <see cref="BodyguardCount"/> highest-level unspawned troops
    /// (roster order on ties; one when there is no boss origin and the count is zero, so
    /// <c>SelectBossAgent</c> always has a pick). <c>SpawnCount</c> is that number when vanilla can
    /// pad from its troop-type cache (<paramref name="canPad"/>), else the kept count.
    /// </summary>
    HideoutAmbushSelection PlanAmbush(IReadOnlyList<int> unspawnedLevels, bool hasBossOrigin, bool canPad);
}
