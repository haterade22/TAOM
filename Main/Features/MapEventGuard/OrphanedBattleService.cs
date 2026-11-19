using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// Finishes AI map events that a battle-end crash left half finalized (player report 2026-11-19, the #1475 crash).
/// <c>MapEvent.FinalizeEventAux</c> marks the event finalized before it detaches the parties, so a throw in between
/// (the #1475 roster read, "Continue" on the crash dialog) leaves parties attached. <c>MapEventManager.Tick</c> then
/// drops the finalized event, and those parties stay in a battle nobody runs. After a load the event's component
/// also has no back-reference. Each pass runs Patch170's steps on every such event outside the player's encounter:
/// the rule is <see cref="HalfFinishedEncounterService.Decide"/>, then restore the back-reference and run the
/// side-end step of vanilla's finish for defenders, then attackers. Unlike Patch170 it then runs the rest of
/// vanilla's finish (map visual end, <c>FinalizeComponent</c>), which a same-session or blockade orphan still
/// needs. Stands down in any co-op session, as the
/// stuck-battle guard does. Details: docs/features/map-event-guard.md.
/// </summary>
public sealed class OrphanedBattleService
{
    private readonly IOrphanedBattleAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _warnedFailures = new();

    public OrphanedBattleService(IOrphanedBattleAdapter adapter, ICoopSessionProvider coop, IModLogger logger)
    {
        _adapter = adapter;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>Repairs every half-finalized AI battle; returns one line per battle repaired.</summary>
    public IReadOnlyList<string> RepairAll()
    {
        var report = new List<string>();
        if (_coop.IsSessionActive || _coop.ShouldDeferToHost)
            return report;

        foreach (var battle in _adapter.AttachedFinalizedBattles())
        {
            string label = battle.Label();
            try
            {
                var snapshot = battle.Read();
                var action = HalfFinishedEncounterService.Decide(snapshot);
                if (action != HalfFinishedEncounterService.Action.CompleteFinalize)
                    continue;

                if (snapshot.BackReferenceMissing)
                    battle.RestoreBackReference();
                int defenders = battle.EndSide(0);
                int attackers = battle.EndSide(1);
                battle.FinishEvent();

                string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0}: released {1} parties (defenders {2}, attackers {3}), backReferenceRestored={4}",
                    label, defenders + attackers, defenders, attackers, snapshot.BackReferenceMissing);
                _logger.LogWarning("[MapEventGuard] finished an orphaned battle: " + line);
                report.Add(line);
            }
            catch (Exception ex)
            {
                // One bad event never stops the sweep. A partial repair keeps its progress; the next pass retries.
                if (_warnedFailures.Add(label))
                    _logger.LogWarning($"[MapEventGuard] could not finish orphaned battle {label}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return report;
    }
}
