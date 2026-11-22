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
/// side-end step of vanilla's finish for defenders, then attackers, then the rest of vanilla's finish (map visual
/// end, <c>FinalizeComponent</c>), as Patch170 does. Stands down in any co-op session, as the
/// stuck-battle guard does. After the battles it releases stranded parties, whose side no longer lists them (a player's
/// save, 2026-11-21: eliminated clans whose leftover parties made <c>DestroyClanAction</c> throw every day).
/// Details: docs/features/map-event-guard.md.
/// </summary>
public sealed class OrphanedBattleService
{
    private readonly IOrphanedBattleAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _warnedFailures = new();
    private readonly HashSet<object> _unfreedBattles = new();

    public OrphanedBattleService(IOrphanedBattleAdapter adapter, ICoopSessionProvider coop, IModLogger logger)
    {
        _adapter = adapter;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>
    /// Repairs every half-finalized AI battle, then releases every stranded party (<see cref="IsStranded"/>); returns
    /// one line per battle repaired and per party released.
    /// </summary>
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
        ReleaseStranded(report);
        return report;
    }

    /// <summary>The stranded half alone, for a finished load: the first hourly sweep can come after the clan's daily tick.</summary>
    public IReadOnlyList<string> ReleaseStrandedParties()
    {
        var report = new List<string>();
        if (_coop.IsSessionActive || _coop.ShouldDeferToHost)
            return report;
        ReleaseStranded(report);
        return report;
    }

    public static bool IsOrphanedEncounter(bool attackerIsMain, bool attackerIsArmyLeader, bool targetBattleFinalized,
        bool targetInPlayerBattle) =>
        (attackerIsMain || attackerIsArmyLeader) && targetBattleFinalized && !targetInPlayerBattle;

    public bool FinishBeforeEncounter(object battle, Func<bool> targetStillInIt)
    {
        if (_coop.IsSessionActive || _coop.ShouldDeferToHost || _unfreedBattles.Contains(battle))
            return false;
        RepairAll();
        if (!targetStillInIt())
            return true;
        _unfreedBattles.Add(battle);
        _logger.LogWarning("[MapEventGuard] could not free an orphaned battle before the player's encounter; vanilla runs it");
        return false;
    }

    /// <summary>
    /// Pure rule: a party is stranded when its side reference is set but that side does not list it. A
    /// <c>PartyBase.MapEventSide</c> write that threw inside <c>MapEventSide.RemovePartyInternal</c> after its
    /// <c>RemoveAt</c> (the battle-end crash) leaves this state, and every later write throws
    /// <c>ArgumentOutOfRangeException</c> there, so <c>DestroyClanAction</c> fails each day on the clan's leftover parties.
    /// The main party and parties in the player's encounter are left to vanilla and Patch170.
    /// </summary>
    public static bool IsStranded(bool hasSide, bool sideListsIt, bool isMainParty, bool inPlayerBattle) =>
        hasSide && !sideListsIt && !isMainParty && !inPlayerBattle;

    private void ReleaseStranded(List<string> report)
    {
        foreach (var party in _adapter.StrandedParties())
        {
            string label = party.Label();
            try
            {
                party.Release();
                _logger.LogWarning("[MapEventGuard] released a stranded party: " + label);
                report.Add(label);
            }
            catch (Exception ex)
            {
                if (_warnedFailures.Add(label))
                    _logger.LogWarning($"[MapEventGuard] could not release stranded party {label}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
