using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Adapters;

public interface ISettlementOwnershipAdapter
{
    IReadOnlyList<FiefSummary> GetPlayerOwnedFiefs();

    /// <summary>
    /// Fast-path count of the player's owned towns + castles. Implementations MUST avoid
    /// iterating <c>Settlement.All</c> (~862 entries) and build no <c>FiefSummary</c> objects —
    /// the F6 patch polls this every frame for the empty-fief gate, so the cost difference
    /// between iterating <c>Clan.PlayerClan.Settlements</c> (a small cached list) vs.
    /// <c>Settlement.All</c> matters. Returns 0 if there is no player clan. Audit issue #143.
    /// </summary>
    int GetPlayerOwnedFiefCount();

    bool IsPlayerCurrentlyAt(string settlementId);

    /// <summary>
    /// Resolve a sealed Settlement instance from its StringId. Returns null if not found
    /// or if the player has lost ownership since the FiefSummary was produced. Used at the
    /// boundary (consequence callback) to convert the testable DTO back into the sealed
    /// type the engine expects, while keeping the service / VM ADR-007 compliant.
    /// </summary>
    Settlement Resolve(string settlementId);
}
