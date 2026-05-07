using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Adapters;

public interface ISettlementOwnershipAdapter
{
    IReadOnlyList<FiefSummary> GetPlayerOwnedFiefs();
    bool IsPlayerCurrentlyAt(string settlementId);

    /// <summary>
    /// Resolve a sealed Settlement instance from its StringId. Returns null if not found
    /// or if the player has lost ownership since the FiefSummary was produced. Used at the
    /// boundary (consequence callback) to convert the testable DTO back into the sealed
    /// type the engine expects, while keeping the service / VM ADR-007 compliant.
    /// </summary>
    Settlement Resolve(string settlementId);
}
