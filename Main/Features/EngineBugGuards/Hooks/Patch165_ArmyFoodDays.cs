using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch165 (#1444; ported from Silas He's v1.5.6 fix package): the army food tooltip's last row shows huge numbers.
/// v1.5.7 <c>CampaignUIHelper.GetArmyFoodTooltip</c> (:694-722) averages <c>Food / -FoodChange</c> over the parties
/// (:712); a party whose food change is 0 or nearly 0 adds infinity or a huge number. The tooltip's own title rows
/// already sum the army's food and daily change (:697-705), so the last row now shows those totals through native's
/// <c>GetDaysUntilNoFood</c> (:653-663), which prints "never" for no loss. Same label, same format, no new text.
/// </summary>
[HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetArmyFoodTooltip))]
[HarmonyPatchCategory(Category)]
[ClientOnly]
public static class Patch165_ArmyFoodDays
{
    internal const string Category = "Patch165_ArmyFoodDays";

    [HarmonyPostfix]
    public static void Postfix(Army army, List<TooltipProperty> __result)
    {
        var leader = army?.LeaderParty;
        if (leader == null || __result == null || __result.Count == 0)
            return;
        float food = leader.Food, change = leader.FoodChange;
        foreach (var attached in leader.AttachedParties)
        {
            food += attached.Food;
            change += attached.FoodChange;
        }
        __result[__result.Count - 1].ValueLabel = CampaignUIHelper.GetDaysUntilNoFood(food, change);
    }
}
