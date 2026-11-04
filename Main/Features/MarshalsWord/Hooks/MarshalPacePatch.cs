using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (docs/features/marshals-word.md, build-spec 6.4): the pace of a march. A postfix on the public
/// <c>HumanAIComponent.GetDesiredSpeedInFormation</c> scales the speed multiplier a soldier gets in the hold state by its company's pace,
/// so a company with a shorter walk arrives with the longest one. It runs on three threads (workers, the AI thread, the main thread), so
/// it reads one volatile reference to an immutable table and the soldier's formation, and takes no lock, allocates nothing, logs nothing
/// and reads no setting. With no march the table is null and the first statement after the charging test returns.
/// </summary>
[HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.GetDesiredSpeedInFormation))]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalPacePatch
{
    // Agent is a protected field of AgentComponent, so it is injected: three underscores and the field's own name (no leading underscore).
    [HarmonyPostfix]
    public static void Postfix(bool isCharging, ref float __result, Agent ___Agent)
    {
        if (isCharging)
            return;
        var table = MarshalHooks.Paces;
        if (table != null)
            __result = table.Scale(___Agent.Formation, __result);
    }
}
