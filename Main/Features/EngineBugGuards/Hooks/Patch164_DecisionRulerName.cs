using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch164 (#1444; ported from Silas He's v1.5.6 fix package): alliance and call-to-war decisions name another
/// kingdom's ruler. v1.5.7 <c>StartAllianceDecision.GetChooseDescription</c> (:144-151), the two text methods of
/// <c>ProposeCallToWarAgreementDecision</c> (:159-169, :182-191) and <c>AcceptCallToWarAgreementDecision.GetChooseDescription</c>
/// (:180-189) set <c>RULER</c> on the outer text, but <c>{RULER.NAME}</c> sits inside the nested
/// <c>RULER_NAME_AND_TITLE</c> text, which is resolved on its own and falls back to whichever hero another text set
/// last. This postfix sets <c>RULER</c> on the nested text, the way native's own single-clan path does
/// (<c>AllianceCampaignBehavior</c> :158-160). It also fills the called kingdom's name that native's
/// <c>ProposeCallToWarAgreementDecision.GetChooseDescription</c> leaves blank. Text only.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(Category)]
public static class Patch164_DecisionRulerName
{
    internal const string Category = "Patch164_DecisionRulerName";

    internal const string RulerTitleText = "str_faction_ruler_name_with_title";

    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(StartAllianceDecision), nameof(StartAllianceDecision.GetChooseDescription));
        yield return AccessTools.Method(typeof(ProposeCallToWarAgreementDecision), nameof(ProposeCallToWarAgreementDecision.GetChooseTitle));
        yield return AccessTools.Method(typeof(ProposeCallToWarAgreementDecision), nameof(ProposeCallToWarAgreementDecision.GetChooseDescription));
        yield return AccessTools.Method(typeof(AcceptCallToWarAgreementDecision), nameof(AcceptCallToWarAgreementDecision.GetChooseDescription));
    }

    [HarmonyPostfix]
    public static void Postfix(KingdomDecision __instance, TextObject __result)
    {
        var kingdom = __instance?.Kingdom;
        var leader = kingdom?.Leader;
        if (__result == null || leader == null || kingdom!.Culture == null)
            return;
        TextObject title = GameTexts.FindText(RulerTitleText, kingdom.Culture.StringId);
        title.SetCharacterProperties("RULER", leader.CharacterObject);
        __result.SetTextVariable("RULER_NAME_AND_TITLE", title);

        // ProposeCallToWarAgreementDecision.GetChooseDescription (:182-191) writes {CALLED_KINGDOM} but sets
        // KINGDOM_NAME, so the called kingdom's name is blank; GetChooseTitle sets it already, and setting it again there
        // writes the same value.
        if (__instance is ProposeCallToWarAgreementDecision call && call.CalledKingdom != null)
            __result.SetTextVariable("CALLED_KINGDOM", call.CalledKingdom.InformalName);
    }
}
