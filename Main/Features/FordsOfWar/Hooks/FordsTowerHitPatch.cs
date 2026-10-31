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
/// allows: 0 for a tower still locked behind another, the ranged, hero or siege factor otherwise. A component that is not a
/// registered Fords tower comes back unchanged, so every other destructible in the game is untouched. The prefix reads one volatile
/// field first, so a mission with no Fords match costs a null check.
///
/// Callers and thread: <c>OnEntityHit</c> is reached from <c>Mission.MeleeHitCallback</c> and <c>Mission.MissileHitCallback</c>, both
/// <c>[MBCallback(null, false)]</c> natives. Native chooses their thread and no log has shown which, so this runs as if it may be off
/// the main thread (harmony-patches.md "Which thread runs your target"): it registers no blow, spawns nothing and touches no main-thread
/// collection; the hook takes the director's gate and the adapters' own locks. Harmony binds the parameters by NAME, pinned by
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
        try
        {
            empty = Describe(ref weapon, out ranged);
        }
        catch (Exception ex)
        {
            LogFault(ex);
            return;
        }

        Dispatch(hook, __instance, attackerAgent, empty, ranged, ref inflictedDamage);
    }

    // A zero-damage blow is only counted (with its real ranged class); anything else goes to the table.
    internal static void Dispatch(IFordsTowerHitHook hook, object component, object? attacker, bool weaponEmpty, bool ranged, ref int damage)
    {
        if (damage <= 0)
        {
            Note(hook, component, attacker, false, ranged);
            return;
        }

        Run(hook, component, attacker, weaponEmpty, ranged, ref damage);
    }

    /// <summary>
    /// Asks the hook and writes its damage back; a fault leaves the vanilla damage. A blow with no weapon (a kick, a mount's charge)
    /// is left alone: vanilla zeroes it right after this prefix, so asking the hook would make the rules' hit points drift below the
    /// entity's.
    /// </summary>
    internal static void Run(IFordsTowerHitHook hook, object component, object? attacker, bool weaponEmpty, bool ranged, ref int damage)
    {
        if (weaponEmpty)
        {
            Note(hook, component, attacker, true, ranged);
            return;
        }

        try
        {
            damage = hook.AdjustTowerDamage(component, attacker, ranged, damage);
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

    // Whether the blow has no weapon, and whether it is ranged: a missile (the weapon is the arrow or bolt) or a thrown or shot weapon.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Describe(ref MissionWeapon weapon, out bool ranged)
    {
        var usage = weapon.CurrentUsageItem;
        ranged = usage != null && (usage.IsRangedWeapon || usage.IsAmmo || usage.IsConsumable);
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
