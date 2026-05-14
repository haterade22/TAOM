using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.CompanionTactics.BattleActionBar.Hooks;

/// <summary>
/// Postfix on <c>Formation.SetMovementOrder(MovementOrder)</c> — implements the previously-dead
/// MCM setting <c>CancelStanceOnMove</c>. When enabled, any movement order change clears the
/// formation's recorded stance.
///
/// v1.3.15 verified signature (ilspycmd):
///   public void SetMovementOrder(MovementOrder input)
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) })]
// Shared with Patch31_FormationSetMovementOrder. The category is applied from
// OnMissionBehaviorInitialize (one-shot guarded) because MovementOrder.cctor reads
// Mission.Current.CurrentTime — null during OnSubModuleLoad / OnGameInitializationFinished.
[HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
public static class Patch35_Formation_SetMovementOrder
{
    // Phase 9b #164 — `?` nullable annotations match the `??=` lazy-init pattern. Codex review
    // pattern: the fields ARE null before first invocation, declaring them non-nullable is
    // misleading even though C# allows the lazy assignment without warnings.
    private static ICompanionTacticsSettingsProvider? _settings;
    private static ITroopStanceManager? _stances;
    // Phase 9b #164 — one-shot logging guard for the catch (was bare).
    private static bool _exceptionLogged;

    [HarmonyPostfix]
    public static void Postfix(Formation __instance)
    {
        try
        {
            _settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();
            if (_settings == null || !_settings.CancelStanceOnMove) return;
            if (__instance == null) return;

            // Phase 9b #149 — team filter. Formation.SetMovementOrder is invoked from the async AI
            // tick (Mission.doAsyncAITick → TickAgentsAndTeamsAsync → BehaviorXxx.TickOccasionally →
            // Formation.SetMovementOrder) for EVERY team's formations. Without this filter, the
            // Postfix mutates TroopStanceManager._stances Dictionary from a worker thread,
            // racing main-thread reads/writes from BattleActionBarMissionView. .NET 4.7.2
            // `Dictionary<TKey,TValue>` is not concurrent-safe → KeyNotFoundException or silent
            // bucket-chain corruption. Stances are player-team-only semantically, so the
            // team filter is the simpler fix vs adding locks. See `cluster-harmony-patches.md`
            // Cluster B finding B1.
            if (__instance.Team != Mission.Current?.PlayerTeam) return;

            _stances ??= IoC.Resolve<ITroopStanceManager>();
            _stances?.ClearStance((int)__instance.FormationIndex);
        }
        catch (Exception ex)
        {
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try { IoC.Resolve<IModLogger>()?.LogError(
                    $"[CompanionTactics] Patch35 CancelStanceOnMove postfix failed: " +
                    $"{ex.GetType().Name}: {ex.Message}. CancelStanceOnMove disabled for the rest " +
                    $"of this session (one-shot log)."); }
                catch { /* logger unavailable — keep silent fallback */ }
            }
        }
    }
}
