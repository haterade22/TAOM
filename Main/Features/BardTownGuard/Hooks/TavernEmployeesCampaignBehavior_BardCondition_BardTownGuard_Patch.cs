using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.BardTownGuard.Hooks;

/// <summary>
/// Patch110, #824: transpiler on private <c>TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition</c>
/// (v1.4.8, TavernEmployeesCampaignBehavior.cs:641-665). Vanilla reads <c>.Name</c> of a randomly picked town that is
/// null when the culture owns no other town; the one pick call is swapped for <see cref="BardTownPickHook.PickTown"/>.
/// Callers, thread and co-op notes: docs/reference/harmony-patch-registry.md "Patch110_BardTownGuard".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(BardTownGuardModule.PatchCategory)]
public static class TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch
{
    private static IModLogger? _logger;

    internal static void Initialize(IModLogger logger) => _logger = logger;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => AccessTools.Method(typeof(TavernEmployeesCampaignBehavior), "conversation_talk_bard_on_condition");

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        => BardTownTranspiler.SwapPick(instructions, _logger);
}
