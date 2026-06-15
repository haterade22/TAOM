using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.Hooks;

/// <summary>
/// Strips <see cref="BlowFlags.CanDismount"/> from a blow about to land on a spider-mounted rider, an instant
/// before native <c>Agent.HandleBlowAux</c> runs.
///
/// WHY: a non-lethal <c>CanDismount</c> melee hit on the mounted Spider Rider AVs inside native HandleBlowAux
/// reading 0x0000000000000003 (debugger-proven 2026-06-15; stack: MeleeHitCallback → Mission.RegisterBlow →
/// Agent.RegisterBlow → HandleBlow → HandleBlowAux). This is the engine's native mounted-DISMOUNT path for the
/// non-vanilla spider mount — the SAME fault class as the mounted-DEATH path that
/// <see cref="Agent_Die_SpiderDismount_Patch"/> (Patch47) routes around. Patch47 covers death (it hard-dismounts
/// before native Die so the rider dies on-foot); this covers the non-lethal hit (native HandleBlowAux still
/// attempts the broken dismount whenever the blow carries CanDismount, even when the rider survives).
///
/// Stripping CanDismount both sidesteps the crash AND enforces the intended design: the spider is a LOCKED mount
/// (CanAgentRideMount=false + MountDifficulty=999 in TaomAgentStatCalculateModel) — its rider must not be knocked
/// off, which would orphan the spider into a riderless creature + an on-foot goblin. The blow's damage still
/// applies; only the dismount effect is suppressed. Vanilla mounts (horse/camel) and every other agent are
/// untouched — the guard fires only when the victim's mount is the spider Monster.
///
/// NOTE: the Harad war-elephant shares this rider-on-non-vanilla-mount architecture and likely has the same
/// latent fault on a mahout dismount-on-hit; it has not surfaced (mahouts are rarely melee-reachable atop the
/// elephant). If it ever does, extend the guard with <c>IsElephantMonster</c> here. Kept spider-only to match
/// Patch47's scope and the only debugger-proven crash.
/// </summary>
[HarmonyPatch(typeof(Agent), "HandleBlowAux")]
[HarmonyPatchCategory("Patch48_SpiderHitDismountGuard")]
public static class Agent_HandleBlowAux_SpiderDismountGuard_Patch
{
    private static ISpiderAttackService _service;

    [HarmonyPrefix]
    public static void Prefix(Agent __instance, ref Blow b)
    {
        try
        {
            // Cheap early-out: only knockdown/dismount weapons set CanDismount, so most damaging hits skip the
            // spider lookup entirely (HandleBlowAux is a per-damaging-hit hot path).
            if ((b.BlowFlag & BlowFlags.CanDismount) == 0) return;

            var svc = _service ??= IoC.Resolve<ISpiderAttackService>();
            if (svc == null) return;

            Agent mount = __instance?.MountAgent;
            if (mount != null && svc.IsSpiderMonster(mount.Monster?.StringId))
                b.BlowFlag &= ~BlowFlags.CanDismount;
        }
        catch
        {
            // Guard must never turn a blow into a crash of its own; the blow proceeds unmodified.
        }
    }
}
