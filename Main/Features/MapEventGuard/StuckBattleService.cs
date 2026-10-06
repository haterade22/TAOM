using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// Ends AI map battles the engine can never finish (#748, docs/features/map-event-guard.md "Stuck AI battles").
/// v1.5.4 <c>MapEvent.Update</c> runs a simulation round only while both sides have healthy troops and sets a
/// winner only inside a round, so a side that reaches 0 healthy between rounds leaves the battle open forever. A
/// destroyed party left attached makes <c>CanPartyJoinBattle</c> refuse every joiner on both sides. One write per
/// battle per pass: after a detach the next pass judges the event afresh.
/// Diagnosis from a player's Stuck Battle Guard module, a behavioural port written from TAOM's own spec
/// (docs/reference/provenance-register.md, "Stuck Battle Guard").
/// </summary>
public sealed class StuckBattleService
{
    /// <summary>In-game hours a battle must have run before the guard touches it; covers the first round.</summary>
    public const double GraceHours = 12.0;

    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IStuckBattleAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public StuckBattleService(
        IEnlistmentStateQuery enlistment, IStuckBattleAdapter adapter, ICoopSessionProvider coop, IModLogger logger)
    {
        _enlistment = enlistment;
        _adapter = adapter;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>
    /// True while a co-op session is live or a co-op mod TAOM cannot probe is loaded. BannerlordCoop's
    /// MapEvent.Update prefix stops vanilla finishing a battle that holds a remote player's party, and the
    /// player test sees only the local one; an unprobeable mod cannot tell host from client. No sweep then.
    /// </summary>
    public bool IsStandingDown => _coop.IsSessionActive || _coop.ShouldDeferToHost;

    /// <summary>Acts on every stuck battle; returns one line per battle acted on.</summary>
    public IReadOnlyList<string> Resolve()
    {
        var report = new List<string>();
        if (IsStandingDown)
            return report;

        foreach (var battle in _adapter.LiveBattles())
        {
            try
            {
                var snapshot = battle.Read();
                if (snapshot == null)
                    continue;
                var verdict = Decide(snapshot);
                if (verdict == StuckBattleVerdict.None)
                    continue;

                // Named before the write: a detach that empties a side finalizes the event and re-points its leader.
                string line = $"{battle.Label()}: {Apply(battle, verdict, snapshot)}";
                _logger.LogInfo("[StuckBattle] " + line);
                report.Add(line);
            }
            catch (Exception ex)
            {
                // One bad battle never stops the sweep or the campaign tick.
                _logger.LogWarning($"[StuckBattle] resolving a map event failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        return report;
    }

    /// <summary>Every live map event with its healthy counts and verdict, changing nothing.</summary>
    public IReadOnlyList<string> Describe()
    {
        var report = new List<string>();
        foreach (var battle in _adapter.LiveBattles())
        {
            var snapshot = battle.Read();
            if (snapshot == null)
                continue;
            report.Add($"{battle.Label()}: {snapshot.AttackerHealthy} vs {snapshot.DefenderHealthy} healthy, "
                + $"{snapshot.AgeHours:0.0}h old, {snapshot.DestroyedParties} destroyed attached -> {Decide(snapshot)}");
        }
        return report;
    }

    public StuckBattleVerdict Decide(StuckBattleSnapshot snapshot)
    {
        if (snapshot.InvolvesPlayer || snapshot.HasPendingOutcome)
            return StuckBattleVerdict.None;

        var verdict = Judge(snapshot);
        // Asked last, and only for a battle the sweep would act on: each IsCommanderParty call looks the commander up.
        return verdict != StuckBattleVerdict.None && InvolvesEnlistedCommander(snapshot)
            ? StuckBattleVerdict.None
            : verdict;
    }

    private static StuckBattleVerdict Judge(StuckBattleSnapshot snapshot)
    {
        // Positive requirement: NaN and infinity fail the gate (csharp-architecture.md, engine floats).
        if (!(snapshot.AgeHours >= GraceHours && snapshot.AgeHours < double.PositiveInfinity))
            return StuckBattleVerdict.None;

        if (snapshot.DestroyedParties > 0)
            return StuckBattleVerdict.DetachDestroyed;

        // The raid component drives a raid's progress, and a village has no healthy defenders by design.
        if (snapshot.IsVillageHostileAction)
            return StuckBattleVerdict.None;

        // Both sides empty falls into the first test: the engine's own rule outside a round
        // (MapEvent.CheckIfOneSideHasLost) is that the defenders hold.
        if (snapshot.AttackerHealthy <= 0)
            return StuckBattleVerdict.AwardDefender;
        if (snapshot.DefenderHealthy <= 0)
            return StuckBattleVerdict.AwardAttacker;
        return StuckBattleVerdict.None;
    }

    private static string Apply(IStuckBattleEventAdapter battle, StuckBattleVerdict verdict, StuckBattleSnapshot snapshot)
    {
        switch (verdict)
        {
            case StuckBattleVerdict.DetachDestroyed:
                return $"removed {battle.DetachWrecks()} wrecked parties that blocked joiners";
            case StuckBattleVerdict.AwardAttacker:
                battle.AwardVictory(attackerWins: true);
                return "no healthy defenders, the attackers win";
            default:
                battle.AwardVictory(attackerWins: false);
                return snapshot.DefenderHealthy <= 0
                    ? "no healthy troops on either side, the defenders hold"
                    : "no healthy attackers, the defenders win";
        }
    }

    // While enlisted, the commander's battle is left to the enlistment join pipeline: EnlistmentBehavior's hourly
    // tick runs after this one and would otherwise join the player into a decided, unfinished event.
    private bool InvolvesEnlistedCommander(StuckBattleSnapshot snapshot)
    {
        if (snapshot.PartyIds == null || !_enlistment.IsEnlisted)
            return false;
        foreach (var id in snapshot.PartyIds)
        {
            if (_enlistment.IsCommanderParty(id))
                return true;
        }
        return false;
    }
}
