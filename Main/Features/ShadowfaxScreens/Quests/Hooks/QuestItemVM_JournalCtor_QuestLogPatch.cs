using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// Times the finished-quest (journal) constructor of QuestItemVM and counts it, inside the Quest log's open. It runs once per
/// old quest, so it is no timer window: the service totals it and the open line carries the total. Observe only.
/// </summary>
[HarmonyPatch(typeof(QuestItemVM), MethodType.Constructor,
    new[] { typeof(JournalLogEntry), typeof(Action<QuestItemVM>), typeof(QuestsVM.QuestCompletionType) })]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
[ClientOnly]
public static class QuestItemVM_JournalCtor_QuestLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = QuestLogDeferCalls.BeginItem();

    [HarmonyFinalizer]
    public static void Finalizer(long __state) => QuestLogDeferCalls.EndItem(__state);
}
