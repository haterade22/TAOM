using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Hooks;

/// <summary>
/// Swallows a vanilla <see cref="NullReferenceException"/> in
/// <c>Agent.CheckToDropFlaggedItem()</c>.
///
/// WHY: TAOM's synthetic creature bites (warg + spider, via
/// <c>CustomAttacksUtils.TakeDamage</c> → <c>Mission.RegisterBlow</c> → <c>Agent.HandleBlow</c> →
/// <c>Mission.OnAgentHit</c>) call <c>affectedAgent.CheckToDropFlaggedItem()</c> on the victim
/// (Mission.cs:5609, v1.4.6). When the victim is a non-vanilla mount — a warg biting another warg —
/// the method passes its <c>CanWieldWeapon</c> guard but <c>Equipment[wieldedIndex].Item</c> is
/// null, so <c>.ItemFlags</c> NREs (Agent.cs:3595). Already caught by WargAttackService's try/catch
/// (non-fatal), but it aborts <c>OnAgentHit</c> mid-way and spams the log (crash report 2026-06-17,
/// warg-vs-warg). Swallowing lets <c>OnAgentHit</c> complete — damage is applied earlier in
/// <c>HandleBlow</c>; the only skipped effect is the flagged-item drop, which a mount has none of.
///
/// The Finalizer swallows ONLY <see cref="NullReferenceException"/>; any other exception
/// propagates untouched. Mirrors the Patch47/48 vanilla-crash-guard pattern. The patched method is
/// called per agent-hit, so the body is a single null-type check with no allocation or reflection.
/// </summary>
[HarmonyPatch(typeof(Agent), nameof(Agent.CheckToDropFlaggedItem))]
[HarmonyPatchCategory("Patch50_DropFlaggedItemGuard")]
public static class Agent_CheckToDropFlaggedItem_Guard_Patch
{
    [HarmonyFinalizer]
    public static Exception Finalizer(Exception __exception)
        => __exception is NullReferenceException ? null : __exception;
}
