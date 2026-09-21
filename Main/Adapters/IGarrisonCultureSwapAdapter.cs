using System.Collections.Generic;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary adapter (ADR-007) for replacing a converted fief's standing troops. Covers the two
/// rosters (<c>Town.GarrisonParty.MemberRoster</c> and
/// <c>Settlement.MilitiaPartyComponent.MobileParty.MemberRoster</c>), the per-culture troop data the
/// mapper matches against, and the single player-facing notice — everything in this feature that
/// touches a sealed TaleWorlds type, so the service and mapper touch none.
///
/// All access is by settlement <c>StringId</c> and culture <c>StringId</c>, matching
/// <see cref="ICultureConversionAdapter"/>.
/// </summary>
public interface IGarrisonCultureSwapAdapter
{
    /// <summary>
    /// Snapshot of the fortification's garrison roster. Empty for a village (no <c>Town</c>), for a
    /// settlement with no garrison party — which is the normal state straight after a siege, since
    /// <c>ChangeOwnerOfSettlementAction</c> destroys the old party and creates an empty one — or
    /// when the id does not resolve.
    /// </summary>
    IReadOnlyList<GarrisonTroopInfo> GetGarrisonRoster(string settlementId);

    /// <summary>
    /// Snapshot of the settlement's militia roster. Works for towns, castles and villages alike;
    /// empty when the settlement has no militia party yet (a legitimate state, the component is
    /// created lazily by the first <c>Settlement.Militia</c> top-up).
    /// </summary>
    IReadOnlyList<GarrisonTroopInfo> GetMilitiaRoster(string settlementId);

    /// <summary>
    /// The culture's regular troops indexed by (role, tier), built once and cached for the process.
    /// Null when the culture id does not resolve. Militia troops are excluded so a garrison swap
    /// never hands a town militia-grade bodies.
    /// </summary>
    CultureTroopIndex? GetCultureTroopIndex(string cultureId);

    /// <summary>The culture's four militia slot troops, or null when the culture id does not resolve.</summary>
    CultureMilitiaTroops? GetCultureMilitiaTroops(string cultureId);

    /// <summary>
    /// Applies the swaps to the garrison roster and returns how many bodies were replaced. A swap
    /// whose troop ids no longer resolve, or whose old troop has since left the roster, is skipped
    /// rather than throwing.
    /// </summary>
    int ApplyGarrisonSwaps(string settlementId, IReadOnlyList<TroopSwap> swaps);

    /// <summary>As <see cref="ApplyGarrisonSwaps"/>, against the militia roster.</summary>
    int ApplyMilitiaSwaps(string settlementId, IReadOnlyList<TroopSwap> swaps);

    /// <summary>
    /// Shows the player the one-line "your garrison changed" notice. Called only for a fief owned by
    /// the player's clan, and only when something actually moved.
    /// </summary>
    void NotifyPlayerTroopsSwapped(string settlementId, int troopCount);
}
