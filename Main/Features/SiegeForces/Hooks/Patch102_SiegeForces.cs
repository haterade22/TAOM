using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Features.SiegeForces.Hooks;

/// <summary>
/// Patch102, the picker's spawn-totals fit. <c>DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase</c> (public, v1.5.3
/// :195) gets both sides' total and initial spawn, and the siege handler passes the involved men for both. Troops the
/// player left out are never allocated, so the player side's numbers must shrink by that many or deployment waits for
/// reserved troops that never come. v1.5.3 callers: SandBoxSiegeMissionSpawnHandler (wall battle),
/// SandBoxBattleMissionSpawnHandler (field battle), SallyOutMissionController, CustomBattleMissionSpawnHandler and
/// CustomSiegeMissionSpawnHandler. It runs for every such battle, Custom Battle included, so the guard order is the
/// contract: ask the service whether a record is pending first (a pure read of its own state) and read no engine state
/// when none is. The service does the rest, acting only on the battle and side the record was made for. The whole body
/// is in a try that leaves the four ints untouched and names no engine member (see the entry patch). Harmony binds the
/// four ints by NAME, pinned by SiegeForcesBindingTests. Once per battle; not on <c>ExcludedTargetMethods</c>.
///
/// DECLARED FIRST ON PURPOSE: PatchCategoryIndex applies a category's classes in assembly order and a class that fails
/// stops the rest, so with this class first a failed fit leaves the entry patch unapplied (no picker without its fit).
/// SiegeForcesWiringTests pins that order; the service also offers nothing unless this prefix is attached.
/// </summary>
[HarmonyPatch(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase))]
[HarmonyPatchCategory(SiegeForcesModule.PatchCategory)]
public static class Patch102_SpawnTotalsFit
{
    private static SiegeForcesService? _service;

    [HarmonyPrefix]
    public static void Prefix(object __instance, ref int defenderTotalSpawn, ref int attackerTotalSpawn,
        ref int defenderInitialSpawn, ref int attackerInitialSpawn)
    {
        try
        {
            if (!HasPending())
                return;

            Fit(__instance, ref defenderTotalSpawn, ref attackerTotalSpawn, ref defenderInitialSpawn, ref attackerInitialSpawn);
        }
        catch (Exception ex)
        {
            Patch102_StartSiegeMissionPicker.LogFault("spawn totals not fitted", ex, "deployment may stall");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HasPending() => (_service ??= TAOM.IoC.Resolve<SiegeForcesService>()).HasPending;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fit(object instance, ref int defenderTotal, ref int attackerTotal, ref int defenderInitial, ref int attackerInitial)
    {
        var playerIsAttacker = ((DefaultBattleMissionAgentSpawnLogic)instance).PlayerSide == BattleSideEnum.Attacker;
        var fitted = (_service ??= TAOM.IoC.Resolve<SiegeForcesService>()).FitSpawnTotals(
            playerIsAttacker, new SpawnTotals(defenderTotal, attackerTotal, defenderInitial, attackerInitial));
        if (fitted == null)
            return;

        defenderTotal = fitted.Value.DefenderTotal;
        attackerTotal = fitted.Value.AttackerTotal;
        defenderInitial = fitted.Value.DefenderInitial;
        attackerInitial = fitted.Value.AttackerInitial;
    }
}

/// <summary>
/// Patch102, the siege troop picker's entry (docs/features/siege-forces.md). <c>PlayerSiege.StartSiegeMission</c> (public
/// static, v1.5.3 PlayerSiege.cs:161) opens the wall battle. Its only caller in the v1.5.3 campaign and SandBox sources is
/// <c>MenuHelper.EncounterAttackConsequence</c> (:316), on the main thread, for an assault and a defence alike; the lord's
/// hall branch (:296-313) and the sally-out and relief-force branches (:275-281) never reach it. This prefix opens the
/// vanilla "Manage Troops" screen and returns false; on Done the service arms its plan and calls the public method
/// again under a thread-static latch, so the prefix steps aside and nothing vanilla does is copied.
///
/// A fault before the screen opens returns true: vanilla is the unpatched game, the safe default HERE (it opens the
/// mission with every troop). A fault while the mission opens disarms and reaches the Done callback. The prefix body
/// names no engine member: the engine work is in the NoInlining helpers below, which compile on their first call.
/// OfferPicker compiles inside the prefix's try; Resume compiles later, in the Done callback, outside it. A member that
/// stops resolving fails when a method is compiled, before any try inside it can run, and PatchShield swallows that at
/// the patched method and skips the original (this target is not on its <c>ExcludedTargetMethods</c> list and runs once
/// per assault). SiegeForcesBindingTests pins every member the helpers, the adapters and the model reference.
/// </summary>
[HarmonyPatch(typeof(PlayerSiege), nameof(PlayerSiege.StartSiegeMission))]
[HarmonyPatchCategory(SiegeForcesModule.PatchCategory)]
public static class Patch102_StartSiegeMissionPicker
{
    [ThreadStatic]
    private static bool _resuming;

    private static SiegeForcesService? _service;

    [HarmonyPrefix]
    public static bool Prefix(Settlement settlement)
    {
        if (_resuming)
            return true;

        try
        {
            return !OfferPicker(settlement);
        }
        catch (Exception ex)
        {
            LogFault("picker not offered", ex, "vanilla runs");
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool OfferPicker(Settlement settlement) =>
        (_service ??= TAOM.IoC.Resolve<SiegeForcesService>()).TryOfferPicker(() => Resume(settlement));

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Resume(Settlement settlement)
    {
        _resuming = true;
        try
        {
            PlayerSiege.StartSiegeMission(settlement);
        }
        finally
        {
            _resuming = false;
        }
    }

    internal static void LogFault(string what, Exception ex, string consequence)
    {
        try
        {
            TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[SiegeForces] Patch102: {what} ({ex.GetType().Name}: {ex.Message}); {consequence}");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }
    }
}
