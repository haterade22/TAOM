using HarmonyLib;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Strips <see cref="BlowFlags.CanDismount"/> from a blow about to land on a creature-mounted rider, an instant
/// before native <c>Agent.HandleBlowAux</c> runs.
///
/// WHY: a non-lethal <c>CanDismount</c> melee hit on the mounted Spider Rider AVs inside native HandleBlowAux
/// reading 0x0000000000000003 (debugger-proven 2026-06-15; stack: MeleeHitCallback → Mission.RegisterBlow →
/// Agent.RegisterBlow → HandleBlow → HandleBlowAux). This is the engine's native mounted-DISMOUNT path for the
/// non-vanilla spider mount — the SAME fault class as the mounted-DEATH path that
/// <see cref="Agent_Die_SpiderDismount_Patch"/> (Patch47) routes around. Patch47 covers death (it hard-dismounts
/// before native Die so the rider dies on-foot); this covers the non-lethal hit (native HandleBlowAux still
/// attempts the broken dismount for an Active humanoid rider when the blow carries CanDismount and no shrug-off
/// applies, even when the rider survives).
///
/// Stripping CanDismount both sidesteps the crash AND enforces the intended design: the spider is a LOCKED mount
/// (CanAgentRideMount=false + MountDifficulty=999 in TaomAgentStatCalculateModel) — its rider must not be knocked
/// off, which would orphan the spider into a riderless creature + an on-foot goblin. The blow's damage still
/// applies; only the dismount effect is suppressed. Vanilla mounts and every other agent are untouched: the guard
/// fires only when the victim's mount is a creature mount (<see cref="CreatureMounts"/>).
///
/// Covers every creature mount since 2026-11-08, the same predicate as Patch47. Only the spider's hit crash is
/// debugger-proven; on v1.5.6 native reads this flag at RVA 0x602170, and the dismount path it enables (0x5EC2F0)
/// uses the same unguarded action lookup as the mounted-death branches. The cost: a hit can no longer knock a
/// ram, warg, elk or chariot rider off. Dismounting and remounting by choice are unchanged.
/// </summary>
[HarmonyPatch(typeof(Agent), "HandleBlowAux")]
[HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
public static class Agent_HandleBlowAux_SpiderDismountGuard_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Agent __instance, ref Blow b)
    {
        try
        {
            // Cheap early-out: only knockdown/dismount weapons set CanDismount, so most damaging hits skip the
            // mount lookup entirely (HandleBlowAux is a per-damaging-hit hot path).
            if ((b.BlowFlag & BlowFlags.CanDismount) == 0) return;

            Agent mount = __instance?.MountAgent;
            if (mount != null && CreatureMounts.IsCreatureMount(mount.Monster?.StringId))
                b.BlowFlag &= ~BlowFlags.CanDismount;
        }
        catch
        {
            // Guard must never turn a blow into a crash of its own; the blow proceeds unmodified.
        }
    }
}
