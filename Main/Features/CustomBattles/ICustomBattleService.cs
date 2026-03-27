using System.Collections.Generic;

namespace TAOM.Features.CustomBattles;

public interface ICustomBattleService
{
    IReadOnlyList<string> GetFactionIds();
    IReadOnlyList<string> GetCommanderIds();
    IReadOnlyList<string> GetCommanderIdsForFaction(string factionId);
    string GetDefaultTroopIdForFormation(string factionId, int formationIndex);
}
