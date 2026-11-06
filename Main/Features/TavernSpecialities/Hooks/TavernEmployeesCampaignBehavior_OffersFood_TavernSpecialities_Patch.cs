using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.TavernSpecialities.Hooks;

/// <summary>
/// Patch111, #937: prefix on private <c>TavernEmployeesCampaignBehavior.conversation_tavernmaid_offers_food_on_condition</c>
/// (v1.4.8, TavernEmployeesCampaignBehavior.cs:729-820). Vanilla sets DRINK and FOOD inside seven culture branches and
/// sets nothing for any other culture, so a TAOM town offers the previous town's dishes or the line with both dish names blank. The prefix
/// pre-sets default dishes before vanilla runs; each of vanilla's seven branches then overwrites both variables, so
/// no culture list is needed here. The strings carry keys registered in the module strings. Callers, thread,
/// PatchShield and co-op notes: docs/reference/harmony-patch-registry.md "Patch111_TavernSpecialities".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(TavernSpecialitiesModule.PatchCategory)]
public static class TavernEmployeesCampaignBehavior_OffersFood_TavernSpecialities_Patch
{
    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(TavernEmployeesCampaignBehavior), "conversation_tavernmaid_offers_food_on_condition");

    [HarmonyPrefix]
    public static void Prefix()
    {
        MBTextManager.SetTextVariable("DRINK", new TextObject("{=taom_tavern_drink_default}a mug of ale"));
        MBTextManager.SetTextVariable("FOOD", new TextObject("{=taom_tavern_food_default}a bowl of stew"));
    }
}
