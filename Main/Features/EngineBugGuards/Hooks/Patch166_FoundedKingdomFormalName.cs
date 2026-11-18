using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.EngineBugGuards.Domain;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch166 (#1444; ported from Silas He's v1.5.6 fix package): a kingdom the player founds is formally "Kingdom of
/// {clan name}", whatever name the player chose. The founding flow (<c>GovernorCampaignBehavior</c> :296) passes no
/// formal name, so v1.5.7 <c>KingdomManager.CreateKingdom</c> builds that one (:127-131), and the encyclopedia text
/// built next uses it (:134). This prefix gives the player's own founding the chosen name through native's
/// <c>str_generic_kingdom_formal_name</c>, as the kingdom screen's rename does (<c>KingdomManagementVM</c> :861-870).
/// A kingdom founded before this fix keeps its name; the rename button sets it. Not <c>ClientOnly</c>: the formal
/// name is saved campaign state, set where the kingdom is created.
/// </summary>
[HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.CreateKingdom))]
[HarmonyPatchCategory(Category)]
public static class Patch166_FoundedKingdomFormalName
{
    internal const string Category = "Patch166_FoundedKingdomFormalName";

    [HarmonyPrefix]
    public static void Prefix(TextObject kingdomName, Clan founderClan, ref TextObject formalName)
    {
        if (kingdomName == null || founderClan == null)
            return;
        if (!EngineDisplayRules.ShouldUseChosenFormalName(founderClan == Clan.PlayerClan, formalName != null, founderClan.IsRebelClan))
            return;
        TextObject formal = GameTexts.FindText("str_generic_kingdom_formal_name");
        formal.SetTextVariable("KINGDOM_FORMAL_NAME", kingdomName);
        formalName = formal;
    }
}
