using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch40 — Postfix on the private <c>HideoutCampaignBehavior.game_menu_hideout_place_on_init</c>.
///
/// Vanilla sets the <c>HIDEOUT_DESCRIPTION</c> GameText variable to
/// "{=DOmb81Mu}(Undefined hideout type)" then overrides it ONLY for the five vanilla bandit
/// culture StringIds (<c>forest_bandits</c>, <c>sea_raiders</c>, <c>mountain_bandits</c>,
/// <c>desert_bandits</c>, <c>steppe_bandits</c>). TAOM renamed those cultures, so the engine
/// never matches and the placeholder leaks into the encounter-menu prose.
///
/// This Postfix runs after vanilla's body — still inside on_init, before the menu renders its
/// text "{=!}{HIDEOUT_TEXT}" whose value embeds "{HIDEOUT_DESCRIPTION}". GameText variables
/// resolve lazily at render, so overriding HIDEOUT_DESCRIPTION here propagates to the displayed
/// text. Only this menu shows {HIDEOUT_DESCRIPTION}; <c>hideout_after_wait</c> uses its own
/// culture-agnostic text and needs no patch.
///
/// Cultures the service doesn't recognise (vanilla / other-mod bandits) get a null result and
/// vanilla's value is left untouched.
/// </summary>
[HarmonyPatch(typeof(HideoutCampaignBehavior), "game_menu_hideout_place_on_init")]
[HarmonyPatchCategory("Patch40_HideoutDescription")]
public static class Patch40_HideoutDescription
{
    private static IHideoutDescriptionService _service;

    private static IHideoutDescriptionService GetService() =>
        _service ??= TAOM.IoC.Resolve<IHideoutDescriptionService>();

    [HarmonyPostfix]
    public static void Postfix()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement?.IsHideout != true) return;

        var cultureId = settlement.Culture?.StringId;
        if (string.IsNullOrEmpty(cultureId)) return;

        var description = GetService()?.GetDescription(cultureId);
        if (description != null)
            GameTexts.SetVariable("HIDEOUT_DESCRIPTION", description);
    }
}
