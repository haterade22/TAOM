using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// The deferral itself (docs/features/shadowfax-screens.md). <c>QuestItemVM.PopulateQuestLog</c> adds one stage per journal
/// log; its only callers are the three constructors. For a finished quest (the journal constructor set QuestLogEntry) built
/// inside the Quest log's open, with the toggle on and the adapter bound, the prefix skips it and the service marks the item.
/// Everywhere else the original runs: active quests and issues (no journal entry), the on-demand rebuild (the replay flag),
/// and every call outside an open. Private target; the adapter replays it through reflection and the binding tests pin it, and the prefix reads the
/// parameter <c>log</c> by name: the service keeps the first and last one on the item's mark, so the date sort needs no second walk of the journal.
/// </summary>
[HarmonyPatch(typeof(QuestItemVM), "PopulateQuestLog")]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
[ClientOnly]
public static class QuestItemVM_PopulateQuestLog_QuestLogPatch
{
    [HarmonyPrefix]
    public static bool Prefix(QuestItemVM __instance, JournalLog log)
        => !QuestLogDeferCalls.ShouldSkipPopulate(__instance, __instance.QuestLogEntry != null, log);
}
