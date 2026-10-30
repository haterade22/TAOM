using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// Builds a deferred finished quest the moment it is selected: SetSelectedItem copies the item's stages into the right-hand
/// list, so they must exist first. It also runs for the constructor's own first selection. The build runs whatever the
/// toggle says now (a quest deferred before the switch was turned off still has to build), and a null item is left to
/// vanilla. A build that throws is the service's to contain.
/// </summary>
[HarmonyPatch(typeof(QuestsVM), "SetSelectedItem")]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
[ClientOnly]
public static class QuestsVM_SetSelectedItem_QuestLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(QuestItemVM quest) => QuestLogDeferCalls.EnsureBuilt(quest);
}
