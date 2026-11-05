namespace TAOM.Features.Enlistment;

/// <summary>
/// The single menu-guard authority. Consulted by the Patch66 SetNextMenu prefix on every
/// menu transition (hot path — decisions are a state read + one HashSet probe).
/// Policy: redirect ONLY while EnlistedAttached. EnlistedBattle deliberately lets battle
/// menus through — the battle service transitions state BEFORE pushing encounter menus,
/// and that ordering contract is what keeps commander battles joinable. The one member that acts
/// in EnlistedBattle is <see cref="ShouldBlockBattleExit"/>, which hides vanilla exits from the
/// commander's battle.
/// </summary>
public interface IEnlistmentMenuService
{
    /// <summary>True when the requested native menu must be rewritten to the service wait menu.</summary>
    bool TryRedirectMenu(string requestedMenuId, out string redirectedMenuId);

    /// <summary>
    /// True when the given menu id is one enlistment is entitled to take over. Used by the
    /// maintenance pump so it never closes a settlement or encounter menu the player legitimately
    /// owns; unlike <see cref="TryRedirectMenu"/> this asks only about the id, not about state.
    /// </summary>
    bool IsRedirectable(string menuId);

    /// <summary>
    /// True when the enlisted soldier is in the commander's battle, does not lead his side and still
    /// has a way forward in the encounter menu (a safe-passage truce can grey out both 'Attack' and
    /// 'Send troops', and then the exits are released). Read by the Patch66 postfix on
    /// <c>MapEventHelper.CanMainPartyLeaveBattleCommonCondition</c> to hide vanilla 'Leave' and
    /// 'Abandon army'. The soldier joins inside the commander's army, or without one when the merge
    /// fails (#495); 'Abandon army' leaves a stray army and the latched EnlistedBattle state, 'Leave'
    /// leaves the latched state (#827).
    /// </summary>
    bool ShouldBlockBattleExit();

    /// <summary>
    /// What the encounter menu's 'Send troops' option should do for this soldier (#830). Gated exactly
    /// like <see cref="ShouldBlockBattleExit"/>: <see cref="SitOutVerdict.Unchanged"/> unless he is in
    /// the commander's battle and does not lead his side, with no snapshot read outside that gate. The
    /// verdict is also recorded in <see cref="SitOutLedger"/> for the battle-end payout, on every path
    /// except while the player's map event already has a winner.
    /// </summary>
    SitOutVerdict EvaluateSitOut();
}
