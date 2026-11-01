using System;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// Patch131, the Fords of War tower damage table. <c>DestructableComponent.OnHit</c> (protected internal override; v1.5.6
/// <c>Mission.OnEntityHit</c> calls it for every script on the hit entity, Mission.cs:2261) takes the damage as
/// <c>int inflictedDamage</c> and applies it to the component's hit points. This prefix hands the blow to <see cref="IFordsTowerHitHook"/> and writes back what the match
/// allows: 0 for a tower still locked behind another, the ranged, hero or siege factor otherwise. A non-ranged blow the engine truncated to 0
/// (vanilla scales a melee blow on an object by the entity factor, Pierce 0.1, and by 0.15 for a thrust that hits with the shaft)
/// goes to the hook with the weapon's thrust damage. The table gives a blow from the tower's own side 0 first, then raises the zero
/// to a share of that (<c>towers.thrustFloorFactor</c>) before the same locks and factors apply. A component that is not a
/// registered Fords tower comes back unchanged, so every other destructible in the game is untouched. The prefix reads one volatile
/// field first, so a mission with no Fords match costs a null check.
///
/// Callers and thread: <c>OnEntityHit</c> is reached from <c>Mission.MeleeHitCallback</c> and <c>Mission.MissileHitCallback</c>, both
/// <c>[MBCallback(null, false)]</c> natives. Native chooses their thread. One log (2026-10-31 22:17, taom_debug_2026-10-31_22-08-35.log lines
/// 4863 and 4881-4896) shows <c>MissileHitCallback</c> reaching <c>OnHit_Patch1</c> on managed thread 39, the thread that log's
/// TickProfiler names as the script-tick main thread (line 383), so a missile hit has been seen on the main thread. Run 20261101-112404 traced
/// 76 melee blows (OnRegisterBlow, same call chain), all on thread 39, the main thread. Native gives no promise, so this still runs as if it may be off the main thread (harmony-patches.md "Which thread runs your target"): it registers no
/// blow, spawns nothing and touches no main-thread collection; the hook takes the director's gate and the adapters' own locks. Harmony binds the parameters by NAME, pinned by
/// <c>FordsOfWarBindingTests</c>. The body never throws: a fault logs once and leaves the vanilla damage.
/// </summary>
[HarmonyPatch(typeof(DestructableComponent), "OnHit")]
[HarmonyPatchCategory(FordsOfWarModule.PatchCategory)]
public static class FordsTowerHitPatch
{
    private static IFordsTowerHitHook? _hook;
    private static int _faultLogged;

    internal static bool HasHook => Volatile.Read(ref _hook) != null;

    /// <summary>Called by the mission logic once the match runs.</summary>
    internal static void Install(IFordsTowerHitHook hook) => Volatile.Write(ref _hook, hook);

    /// <summary>Called when the match ends; leaves a hook another mission installed alone.</summary>
    internal static void Uninstall(IFordsTowerHitHook hook) => Interlocked.CompareExchange(ref _hook, null, hook);

    [HarmonyPrefix]
    public static void Prefix(object __instance, Agent attackerAgent, ref int inflictedDamage, ref MissionWeapon weapon)
    {
        var hook = Volatile.Read(ref _hook);
        if (hook == null)
            return;

        bool empty;
        bool ranged;
        int thrust;
        try
        {
            empty = Describe(ref weapon, out ranged, out thrust);
        }
        catch (Exception ex)
        {
            LogFault(ex);
            return;
        }

        Dispatch(hook, __instance, attackerAgent, empty, ranged, thrust, ref inflictedDamage);
    }

    // A blow with no weapon is counted as weaponless, whatever its damage. A melee blow the engine truncated to 0 goes to the table when
    // the weapon has thrust damage (the table floors it); any other zero-damage blow is only counted, with its real ranged class.
    // Anything else goes to the table. A fault leaves the vanilla damage.
    internal static void Dispatch(IFordsTowerHitHook hook, object component, object? attacker, bool weaponEmpty, bool ranged, int thrustDamage,
        ref int damage)
    {
        if (weaponEmpty)
        {
            Note(hook, component, attacker, true, ranged);
            return;
        }

        if (damage <= 0 && (ranged || thrustDamage <= 0))
        {
            Note(hook, component, attacker, false, ranged);
            return;
        }

        try
        {
            damage = hook.AdjustTowerDamage(component, attacker, ranged, damage, thrustDamage);
        }
        catch (Exception ex)
        {
            LogFault(ex);
        }
    }

    // Counts a blow the table does not decide; a fault logs once and changes nothing.
    private static void Note(IFordsTowerHitHook hook, object component, object? attacker, bool weaponless, bool ranged)
    {
        try
        {
            hook.NoteUnappliedBlow(component, attacker, weaponless, ranged);
        }
        catch (Exception ex)
        {
            LogFault(ex);
        }
    }

    // Whether the blow has no weapon, whether it is ranged (a missile, whose weapon is the arrow or bolt, or a thrown or shot weapon), and
    // the thrust damage of the weapon's current usage with its item modifier (0 for no weapon).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Describe(ref MissionWeapon weapon, out bool ranged, out int thrust)
    {
        var usage = weapon.CurrentUsageItem;
        ranged = usage != null && (usage.IsRangedWeapon || usage.IsAmmo || usage.IsConsumable);
        thrust = usage != null ? weapon.GetModifiedThrustDamageForCurrentUsage() : 0;
        return weapon.IsEmpty;
    }

    private static void LogFault(Exception ex)
    {
        if (Interlocked.Exchange(ref _faultLogged, 1) == 1)
            return;

        try
        {
            TAOM.IoC.Resolve<IModLogger>()?.LogWarning(
                $"[FordsOfWar] Patch131: tower damage left as vanilla ({ex.GetType().Name}: {ex.Message}); logged once");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }
    }
}
