namespace TAOM.Features.BanditManagement;

/// <summary>
/// Supplies themed LOTR encounter-menu descriptions for TAOM's bandit-culture hideouts.
///
/// Vanilla <c>HideoutCampaignBehavior.game_menu_hideout_place_on_init</c> hardcodes the
/// hideout description against the five vanilla bandit culture StringIds (<c>forest_bandits</c>,
/// <c>sea_raiders</c>, etc.) and defaults to "{=DOmb81Mu}(Undefined hideout type)" otherwise.
/// TAOM renamed those cultures to <c>dunland_raiders</c> / <c>gundabad_raiders</c> /
/// <c>harad_raiders</c> / <c>rhun_raiders</c> / <c>umbar_corsairs</c>, so the engine never
/// matches and the placeholder leaks through. Patch40 calls this service to substitute the
/// right text per TAOM bandit culture.
///
/// Returns the raw <c>{=key}default</c> template (not a resolved string) so localization
/// resolves at render time against the loaded GameText strings.
/// </summary>
public interface IHideoutDescriptionService
{
    /// <summary>
    /// The localized description template for a TAOM bandit culture, or <c>null</c> for any
    /// other culture (vanilla / other-mod hideouts keep their own engine description).
    /// </summary>
    string? GetDescription(string cultureStringId);
}
