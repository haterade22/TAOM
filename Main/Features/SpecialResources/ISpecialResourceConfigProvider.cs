using System.Collections.Generic;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceConfigProvider
{
    IReadOnlyList<SpecialResource> GetAllResources();
    SpecialResource GetByKingdomId(string kingdomId);
    SpecialResource GetByCultureId(string cultureId);

    /// <summary>The resource a cost row's <c>resource_id</c> names; null for an unknown or null id.</summary>
    SpecialResource GetById(string resourceId);

    TroopResourceCostEntry GetTroopCost(string troopId);
}
