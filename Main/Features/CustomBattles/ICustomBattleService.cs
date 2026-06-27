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
    string GetDefaultTroopIdForFormation(string factionId, int formationIndex);
}
