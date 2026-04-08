using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards;

public interface ISettlementGuardService
{
    string ResolveGuardTroopId(SettlementGuardContext context, string spawnPointTag);
    string ResolveSpearItemId(string cultureId);
}
