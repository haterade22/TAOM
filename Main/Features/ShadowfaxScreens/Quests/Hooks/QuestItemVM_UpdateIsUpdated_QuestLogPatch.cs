using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// Keeps the finished-quest "updated" dot (QuestsScreen.xml binds IsUpdated) right for a deferred item. Vanilla sets it from
/// the stages (<c>Stages.Any(s =&gt; s.IsNew)</c>), which a deferred item does not have yet; the service answers from the
/// item's journal entries and the campaign's unexamined logs, the same data each stage reads. Any other item keeps vanilla's value.
/// </summary>
[HarmonyPatch(typeof(QuestItemVM), nameof(QuestItemVM.UpdateIsUpdated))]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
[ClientOnly]
public static class QuestItemVM_UpdateIsUpdated_QuestLogPatch
{
    [HarmonyPostfix]
    public static void Postfix(QuestItemVM __instance)
    {
        var updated = QuestLogDeferCalls.UpdatedFor(__instance);
        if (updated.HasValue)
            __instance.IsUpdated = updated.Value;
    }
}
