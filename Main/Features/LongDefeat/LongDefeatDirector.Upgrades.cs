using System;
using System.Collections.Generic;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Upgrading the soldiers the player has (#1161). Between waves the director reads the standing defenders by troop on the first tick of a calm, then every <see cref="StandingReadSeconds"/> and on
/// any tick that spawned soldiers, and hands the counts to the run (an order moves its own counts), so the shop sells only soldiers who stand. Each order is carried out on the tick after its buy, before field care, and
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
        if (_standingRead && _standingClock > 0f && fx.DefenderBatches.Count == 0)
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
            var paid = order.Step.Price * _run.CompleteUpgrade(order, report.Replaced);
            // A fade that empties a held split group hands it to the AI (Formation.RemoveUnit, Formation.cs:2376-2379), and the replacement joining it does not take it back.
            if (report.Replaced > 0)
                NoteEngineTookSplit("an upgrade");
            LongDefeatChecks.Record(_outcomes, LongDefeatUpgradeChecks.Id, _mission + "u" + ++_upgradeOrders, LongDefeatUpgradeChecks.Replaced(order, report));
            _logger.LogInfo($"{Tag}[Upgrade] before wave {order.BeforeWave}: {order.Step.FromTroop} to {order.Step.ToTroop}, ordered {order.Count}, found {report.Found}, replaced {report.Replaced}, " +
                $"read back {report.ReadBack}, paid {paid}" + (report.Problem != null ? $", {report.Problem}" : "") + (report.Miss != null ? $", first miss {report.Miss}" : ""));
        }
    }
}
