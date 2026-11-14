using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SettlementGuards.Hooks;

// Regression of #258: a hideout has no guards, so vanilla's guard and disguise start lines must not answer there.
// Postfix on the three private condition methods of GuardsCampaignBehavior (1.5.7 :743, :965, :986, registered
// :585, :584, :612); applied by ManualPatchApplicator. The decision is HideoutGuardDialogRule. Thread: dialog
// conditions run on the main thread. A vanilla result of false is left alone.
public static class GuardsCampaignBehavior_HideoutNoGuards_Patch
{
    public static readonly string[] TargetNames =
    {
        "conversation_guard_start_on_condition",
        "conversation_disguised_start_on_condition",
        "conversation_disguised_start_on_condition_alt",
    };

    /// <summary>The targets that resolved; fewer than <see cref="TargetNames"/> means the engine moved one.</summary>
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var name in TargetNames)
        {
            var method = AccessTools.Method(typeof(GuardsCampaignBehavior), name);
            if (method != null)
                yield return method;
        }
    }

    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (!__result) return;

        var mission = Mission.Current;
        if (mission == null) return;

        __result = HideoutGuardDialogRule.ApplyToResult(
            __result,
            mission.GetMissionBehavior<HideoutMissionController>() != null,
            mission.GetMissionBehavior<HideoutAmbushMissionController>() != null);
    }
}
