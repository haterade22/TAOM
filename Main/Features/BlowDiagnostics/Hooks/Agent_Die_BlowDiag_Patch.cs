using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BlowDiagnostics.Hooks;

/// <summary>
/// Diagnostic prefix on <c>Agent.Die(Blow, KillInfo)</c> — the wound→death case. Stamps the
/// killing blow (same record shape as the HandleBlowAux hook) to the durable log when Blow
/// Diagnostics is ON, so a native AV inside the death path names its victim + killing blow.
/// Sibling of Patch47; separate class so the spider death guard is untouched. Off by default.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.Die))]
[HarmonyPatchCategory("Patch63_BlowDiagnostics")]
public static class Agent_Die_BlowDiag_Patch
{
    private static IBlowDiagnosticService _service;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(Agent __instance, Blow b)
    {
        try
        {
            var svc = _service ??= IoC.Resolve<IBlowDiagnosticService>();
            if (svc == null || !svc.IsEnabled) return;
            svc.LogDeath(Agent_HandleBlowAux_BlowDiag_Patch.BuildRecord(__instance, in b));
        }
        catch { /* diagnostic must never turn a death into a crash */ }
    }
}
