using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// Brackets the Quest log's open (docs/features/shadowfax-screens.md): the prefix opens the defer scope, with its snapshot of
/// the toggle, and the [ScreenOpen] Quests open window; the void finalizer closes both and writes the line. Observe only:
/// nothing here changes what the constructor does, and a void finalizer keeps Harmony's rethrow of the original exception.
/// The link between the two is <c>__state</c>, so a finalizer whose prefix was stripped closes nothing.
/// </summary>
[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, new[] { typeof(Action) })]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
[ClientOnly]
public static class QuestsVM_Ctor_QuestLogPatch
{
    [HarmonyPrefix]
    public static void Prefix(out QuestOpenCall? __state) => __state = QuestLogDeferCalls.BeginOpen();

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, QuestOpenCall? __state)
        => QuestLogDeferCalls.EndOpen(__state, __exception);
}
