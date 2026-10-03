using System.Collections.Generic;

namespace TAOM.Features.CustomBattles;

public interface ICustomBattleService
{
    IReadOnlyList<string> GetFactionIds();
    IReadOnlyList<string> GetCommanderIds();
    IReadOnlyList<string> GetCommanderIdsForFaction(string factionId);

    /// <summary>
    /// Commander ids for a faction. If the faction has a curated entry, returns that exact ordered
    /// list and <paramref name="takeMax"/> is IGNORED (curated lists may exceed the cap). Otherwise
    /// returns the default: valid (2-segment, hero) lords of that culture, alphabetical, capped at
    /// <paramref name="takeMax"/>.
    /// </summary>
    IReadOnlyList<string> GetCommanderIdsForFaction(string factionId, int takeMax);
    /// <summary>
    /// The culture's troop for a formation slot. A troop the slot list can show is always preferred. With
    /// <paramref name="vanillaHasPick"/> that is the only troop returned, so vanilla's pick is replaced only by a
    /// fitting one. Without a vanilla pick, the first candidate that loads is returned even if it does not fit:
    /// vanilla spawns the default as-is when the slot is empty.
    /// </summary>
    string GetDefaultTroopIdForFormation(string factionId, int formationIndex, bool vanillaHasPick);
}
