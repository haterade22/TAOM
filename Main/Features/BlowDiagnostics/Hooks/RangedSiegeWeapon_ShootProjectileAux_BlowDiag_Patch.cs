using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BlowDiagnostics.Hooks;

/// <summary>
/// Diagnostic prefix on <c>RangedSiegeWeapon.ShootProjectileAux(ItemObject, bool)</c> — the last
/// managed frame when a siege engine (the fire-pot FireMangonel included) launches a projectile.
/// FireMangonel/Mangonel do NOT override this method, so the base patch catches every siege shot.
///
/// WHY: the fire-pot crash fires at IMPACT, which is pure native — there is no managed frame at
/// landing. This stamp is the "nothing after this = the native impact effect faulted" marker: if
/// the last durable line is a siege-shot and no [BlowDiag] blow follows, the fault is the impact
/// particle/decal, not a blow — routing triage to the Event-Log fault offset. It also flags a null
/// projectile item (the launch-path deref hypothesis). Off by default.
/// </summary>
[HarmonyPatch(typeof(RangedSiegeWeapon), "ShootProjectileAux")]
[HarmonyPatchCategory("Patch63_BlowDiagnostics")]
public static class RangedSiegeWeapon_ShootProjectileAux_BlowDiag_Patch
{
    private static IBlowDiagnosticService _service;

    [HarmonyPrefix]
    public static void Prefix(RangedSiegeWeapon __instance, ItemObject missileItem)
    {
        try
        {
            var svc = _service ??= IoC.Resolve<IBlowDiagnosticService>();
            if (svc == null || !svc.IsEnabled) return;

            string item = missileItem?.StringId ?? "<null>";
            string side;
            try { side = __instance.Side.ToString(); } catch { side = "?"; }

            svc.LogSiegeShot(item, side);
        }
        catch { /* diagnostic must never turn a shot into a crash */ }
    }
}
