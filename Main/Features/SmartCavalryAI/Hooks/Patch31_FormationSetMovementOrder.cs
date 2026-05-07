using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Adapters.Models;
using TAOM.Core.Logging;

namespace TAOM.Features.SmartCavalryAI.Hooks;

/// <summary>
/// Postfix on <c>Formation.SetMovementOrder</c>. When a cavalry formation on the player
/// team is just-ordered to <c>Charge</c> or <c>ChargeToTarget</c>, hand control to
/// <see cref="ICavalryChargeService.HandleChargeOrder"/> which decides reroute vs
/// line-charge and starts the state machine.
///
/// <para>Uses the v1.3.15 enum names (<c>MovementOrderEnum.Charge=2</c>,
/// <c>ChargeToTarget=3</c>) — NEVER int casts. The decompiled v1.4 source used
/// integer casts against values 4 and 5, which silently mismatch on v1.3.15.</para>
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.SetMovementOrder))]
[HarmonyPatchCategory("Patch31_SmartCavalryAI")]
public static class Patch31_FormationSetMovementOrder
{
    // Cached IoC singletons. Each `??=` performs a single dictionary lookup on first
    // postfix invocation per process; subsequent calls are direct field reads.
    private static ISmartCavalryAISettingsProvider? _settings;
    private static ICavalryChargeService? _service;
    private static IBattlefieldQueryAdapter? _battlefield;
    private static IModLogger? _logger;

    private static ISmartCavalryAISettingsProvider Settings =>
        _settings ??= IoC.Resolve<ISmartCavalryAISettingsProvider>();
    private static ICavalryChargeService Service =>
        _service ??= IoC.Resolve<ICavalryChargeService>();
    private static IBattlefieldQueryAdapter Battlefield =>
        _battlefield ??= IoC.Resolve<IBattlefieldQueryAdapter>();
    private static IModLogger Logger =>
        _logger ??= IoC.Resolve<IModLogger>();

    [HarmonyPostfix]
    public static void Postfix(Formation __instance, MovementOrder input)
    {
        // Cheapest gates first — thread-local read, null check, enum check.
        if (SmartCavalryRecursionGuard.IsSuppressed) return;
        if (__instance == null) return;
        if (input.OrderEnum != MovementOrder.MovementOrderEnum.Charge &&
            input.OrderEnum != MovementOrder.MovementOrderEnum.ChargeToTarget) return;

        // PlayerTeam null in spectator/custom-battle missions; bail before any IoC resolve.
        var team = Mission.Current?.PlayerTeam;
        if (team == null || __instance.Team != team) return;

        // Cheapest IoC-backed gate before the heavier per-formation work.
        var settings = Settings;
        if (!settings.IsEnabled) return;

        var cav = new FormationAdapter(__instance);
        if (!cav.RepresentativeIsCavalry) return;

        // Resolve the target formation.
        Formation? target = input.OrderEnum == MovementOrder.MovementOrderEnum.ChargeToTarget
            ? input.TargetFormation
            : NearestEnemyFormation(__instance);
        if (target == null || target.CountOfUnits == 0) return;

        var targetPosition2d = target.CurrentPosition;
        var targetPosition = new Vec3(targetPosition2d.x, targetPosition2d.y, 0f, -1f);

        var commands = new CavalryCommandAdapter(__instance);
        var time = Mission.Current?.CurrentTime ?? 0f;

        Service.HandleChargeOrder(cav, commands, Battlefield, target, targetPosition, time);

        if (settings.IsDebugMode)
        {
            Logger.LogInfo($"[SmartCavalryAI] {__instance.FormationIndex} ordered {input.OrderEnum} → state={Service.GetState(__instance)}");
        }
    }

    private static Formation? NearestEnemyFormation(Formation own)
    {
        var mission = Mission.Current;
        if (mission == null || own?.Team == null) return null;
        Formation? best = null;
        var bestDistance = float.MaxValue;
        foreach (var team in mission.Teams)
        {
            // Defensive: skip own.Team explicitly. IsFriendOf(self) is true in standard
            // setups but quirky multi-team custom battles have produced false here.
            if (team == null || team == own.Team) continue;
            if (team.IsFriendOf(own.Team)) continue;
            foreach (var enemy in team.FormationsIncludingEmpty)
            {
                if (enemy == null || enemy.CountOfUnits == 0) continue;
                if (ReferenceEquals(enemy, own)) continue;  // belt-and-braces self-charge guard
                var d = own.CurrentPosition.Distance(enemy.CurrentPosition);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = enemy;
                }
            }
        }
        return best;
    }
}
