namespace TAOM.Features.Enlistment;

/// <summary>
/// The single menu-guard authority. Consulted by the Patch66 SetNextMenu prefix on every
/// menu transition (hot path — decisions are a state read + one HashSet probe).
/// Policy: redirect ONLY while EnlistedAttached. EnlistedBattle deliberately lets battle
/// menus through — the battle service transitions state BEFORE pushing encounter menus,
/// and that ordering contract is what keeps commander battles joinable.
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
}
