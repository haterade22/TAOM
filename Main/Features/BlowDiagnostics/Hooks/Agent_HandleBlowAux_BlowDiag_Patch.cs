using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.BlowDiagnostics.Domain;

namespace TAOM.Features.BlowDiagnostics.Hooks;

/// <summary>
/// Diagnostic prefix on <c>Agent.HandleBlowAux(ref Blow)</c> — the last managed frame before the
/// native <c>MBAPI.IMBAgent.HandleBlowAux</c> call. Stamps every damaging blow (victim race, blow
/// flags, damage type, missile/fall, health) to the durable log when Blow Diagnostics is ON.
///
/// WHY: a dwarf (custom race) taking a specific blow — a wounding hit, or a fire-pot AoE hit —
/// AVs inside native HandleBlowAux with no managed stack (the same fault family the spider
/// Patch47/48 guard, but those are spider-gated so a plain dwarf is unguarded). The last
/// [BlowDiag] line before the log stops names the fatal blow. Sibling of Patch48; separate class
/// so the spider guard is untouched. Runs at Priority.First so it records the PRISTINE blow
/// before Patch48 can strip CanDismount. Off by default (per-hit hot path).
/// </summary>
[HarmonyPatch(typeof(Agent), "HandleBlowAux")]
[HarmonyPatchCategory("Patch63_BlowDiagnostics")]
public static class Agent_HandleBlowAux_BlowDiag_Patch
{
    private static IBlowDiagnosticService _service;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(Agent __instance, ref Blow b)
    {
        try
        {
            var svc = _service ??= IoC.Resolve<IBlowDiagnosticService>();
            if (svc == null || !svc.IsEnabled) return;
            svc.LogBlow(BuildRecord(__instance, in b));
        }
        catch { /* diagnostic must never turn a blow into a crash */ }
    }

    // Extracts primitives from the sealed Agent/Blow at the boundary (ADR-007). Shared with the
    // Die hook. Every read is guarded; a null field just leaves its default in the record.
    internal static BlowDiagRecord BuildRecord(Agent victim, in Blow b)
    {
        var r = new BlowDiagRecord();
        try
        {
            r.VictimName = victim?.Name ?? "?";
            r.VictimRace = victim?.Character?.Race ?? -1;
            r.VictimIsPlayer = victim != null && victim == Agent.Main;

            var mount = victim?.MountAgent;
            r.VictimIsMounted = mount != null;
            r.MountMonster = mount?.Monster?.StringId ?? "";

            r.VictimHealth = victim?.Health ?? float.NaN;

            r.BlowFlags = b.BlowFlag.ToString();
            r.DamageType = b.DamageType.ToString();
            r.InflictedDamage = b.InflictedDamage;
            r.BaseMagnitude = b.BaseMagnitude;
            r.IsMissile = b.IsMissile;
            r.IsFallDamage = b.IsFallDamage;
            r.VictimBodyPart = b.VictimBodyPart.ToString();
            r.AttackerIndex = b.OwnerId;
        }
        catch { /* best-effort snapshot */ }
        return r;
    }
}
