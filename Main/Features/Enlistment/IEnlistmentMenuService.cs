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
}
