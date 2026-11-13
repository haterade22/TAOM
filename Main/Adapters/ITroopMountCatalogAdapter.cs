using System.Collections.Generic;
using TAOM.Features.Arachnophobia;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of Arachnophobia Mode (docs/features/arachnophobia-mode.md): reads the troops, and writes their
/// mounts, upgrade targets and list flags. This data reloads with each game and goes into no save.
/// </summary>
public interface ITroopMountCatalogAdapter
{
    bool ItemExists(string itemId);

    /// <summary>Every troop, hero or not, with a battle set whose Horse item is on the Monster.</summary>
    IReadOnlyList<TroopMountRecord> ReadRiders(string monsterId);

    /// <summary>Writes the warg and its harness on every battle set of the troop that carries the spider item.
    /// Returns the number of sets written. 0 is normal when another troop shares the set and came first.</summary>
    int ReplaceMount(string troopId, string spiderItemId, string wargItemId, string harnessItemId);

    /// <summary>Every non-hero campaign troop with an upgrade target. Campaign only.</summary>
    IReadOnlyList<TroopUpgradeRecord> ReadUpgrades();

    /// <summary>Keeps only the named upgrade targets. False when the troop is gone or the engine's private setter is
    /// not found. Campaign only.</summary>
    bool SetUpgradeTargets(string troopId, IReadOnlyCollection<string> keptTargetIds);

    /// <summary>Campaign only.</summary>
    void HideFromEncyclopedia(string troopId);

    /// <summary>Removes the troop from the Custom Battle troop list. False when the troop is gone or the engine's
    /// private setter is not found. Custom Battle only: its troop list (ArmyCompositionGroupVM) and TAOM's default pick
    /// (CustomBattleService) read the flag for a soldier; the campaign reads it only for a hero's own character.</summary>
    bool MarkObsolete(string troopId);
}
