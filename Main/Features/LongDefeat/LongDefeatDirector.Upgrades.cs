using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Upgrading the soldiers the player has (#1161). Between waves the director reads the standing defenders by troop on the first tick of a calm, then every <see cref="StandingReadSeconds"/> and on
/// any tick that spawned or replaced soldiers, and hands the counts to the run, so the shop sells only soldiers who stand. Each order is carried out on the tick after its buy, before field care, and
/// writes one <c>upgrade</c> line.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private const float StandingReadSeconds = 0.5f;

    private float _standingClock;
    private bool _standingRead;
    private int _upgradeOrders;

    private void TickStanding(float dt, RunEffects fx)
    {
        if (_run.Phase is not (RunPhase.Preparation or RunPhase.Intermission))
        {
            _standingRead = false;
            return;
        }

        _standingClock -= dt;
        if (_standingRead && _standingClock > 0f && fx.DefenderBatches.Count == 0 && fx.Upgrades.Count == 0)
            return;

        _standingRead = true;
        _standingClock = StandingReadSeconds;
        _run.ObserveStanding(_field.StandingDefendersByTroop());
    }

    private void ApplyUpgrades(IReadOnlyList<UpgradeOrder> orders)
    {
        foreach (var order in orders)
        {
            var report = _field.ReplaceDefenders(order.Step.FromTroop, order.Step.ToTroop, order.Count, order.HealthFactor);
            _run.CompleteUpgrade(order, report.Replaced);
            var beforeWave = _run.Wave + 1;
            LongDefeatChecks.Record(_outcomes, LongDefeatUpgradeChecks.Id, "u" + ++_upgradeOrders, LongDefeatUpgradeChecks.Replaced(beforeWave, order, report));
            var paid = order.Step.Price * Math.Max(0, Math.Min(order.Count, report.Replaced));
            _logger.LogInfo($"{Tag}[Upgrade] before wave {beforeWave}: {order.Step.FromTroop} to {order.Step.ToTroop}, ordered {order.Count}, found {report.Found}, replaced {report.Replaced}, " +
                $"read back {report.ReadBack}, paid {paid}" + (report.Problem != null ? $", {report.Problem}" : ""));
        }
    }
}
