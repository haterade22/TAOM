using System.Collections.Generic;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceConfigProvider
{
    IReadOnlyList<SpecialResource> GetAllResources();
    SpecialResource GetByKingdomId(string kingdomId);
    TroopResourceCostEntry GetTroopCost(string troopId);
}
