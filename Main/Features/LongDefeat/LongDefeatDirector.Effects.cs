using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

internal sealed partial class LongDefeatDirector
{
    // The defender classes whose recorded muster place had no navmesh: warned once, then left on the scene's frame for the rest of the run.
    private readonly HashSet<FormationSlot> _musterFallback = new();

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static string F2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string Percent(float share) => (share * 100f).ToString("F0", CultureInfo.InvariantCulture) + "%";

    /// <summary>Turns what the run decided into adapter calls, in the order the engine needs.</summary>
    private void Apply(RunEffects fx)
    {
        // First, before any soldier of the wave exists: the wave's published numbers are read when a soldier is built or struck.
        if (fx.PublishStats is { } stats)
            _effects?.PublishStats(stats);
        if (fx.ApplyBoon is { } boon)
            ApplyBoonToStanding(boon);

        var standing = fx.DefenderBatches.Count > 0 ? _field.StandingDefenderPoints() : Array.Empty<LdPoint>();
        var closes = fx.SpawnPlayer ? new List<FormationSlot>() : null;
        foreach (var batch in fx.DefenderBatches)
            SpawnBatch(batch, false, fx.SpawnPlayer, standing, closes);

        if (fx.SpawnPlayer && !SpawnLord(false))
            _logger.LogError($"{Tag}[Boot] the player's lord could not be spawned");
        if (closes != null)
        {
            foreach (var slot in closes)
                _field.EndBatch(slot, false);
        }

        if (fx.FinishInitialSpawn)
            _field.FinishInitialSpawn();

        if (fx.StartWave is { } start)
        {
            _logger.LogInfo($"{Tag}[Wave] {start.Wave} starts: first batch {start.Foot} foot and {start.Ranged} ranged{ScaleText(start)}");
            BeginScaleWatch(start);
            BeginAffixes(start.Wave);
        }

        foreach (var batch in fx.AttackerBatches)
            SpawnBatch(batch, true, false, standing, closes);

        if (fx.RefreshAttackerTactic)
        {
            _field.RefreshAttackerTactic();
            SampleScaleAtSpawn();
            ReadAffixEffects();
        }

        if (fx.OrderCharge)
            _field.OrderCharge();
        if (fx.RestoreDefenderTactic)
            _field.RestoreDefenderTactic();
        if (fx.FellAttackers > 0)
        {
            var felled = _field.Fell(fx.FellAttackers);
            if (felled != fx.FellAttackers)
                _logger.LogWarning($"{Tag}[Wave] asked to fell {fx.FellAttackers} attackers, {felled} fell; deaths up to the shortfall pay no bounty before the wave clears");
        }

        foreach (var e in fx.Events)
            OnEvent(e);
        if (fx.RestShare > 0f)
        {
            var rest = _field.RestDefenders(fx.RestShare);
            _logger.LogInfo($"{Tag}[Wave] {_run.WavesCleared} rest: defenders {rest.Count}, mean health {Percent(rest.MeanBefore)} to {Percent(rest.MeanAfter)}");
        }

        if (_run.Result != null)
            Finish();
    }

    /// <summary>
    /// One engine batch. Its first soldier opens it (placing the formation when the run says so), its last closes it. A defender
    /// batch is not closed here when the player spawns in the same call: the foot formation reserved a slot for the lord, so it
    /// closes after him.
    /// </summary>
    private void SpawnBatch(SoldierBatch b, bool attackers, bool playerFollows, IReadOnlyList<LdPoint> standing, List<FormationSlot>? closes)
    {
        var reserve = playerFollows && !attackers && b.Slot == FormationSlot.Infantry ? 1 : 0;
        if (b.FirstIndex == 0)
        {
            if (b.Place)
                _field.PlaceFormation(b.Slot, b.BatchTotal + reserve, attackers);
            else
                _field.BeginBatch(b.Slot, b.BatchTotal + reserve, attackers);
            if (!attackers)
                UseMusterPlace(b.Slot);
        }

        var report = _field.SpawnBatch(b, attackers);
        if (report.Spawned < b.Count)
            _logger.LogWarning($"{Tag}[Spawn] spawned {report.Spawned} of {b.Count} {b.TroopId} ({(attackers ? "attackers" : "defenders")})");

        var last = b.FirstIndex + b.Count >= b.BatchTotal;
        if (last && !(playerFollows && !attackers))
            _field.EndBatch(b.Slot, attackers);
        else if (last)
            closes!.Add(b.Slot);

        LogSpawnLevel(b, attackers, report, last);
        if (!attackers)
        {
            JudgeDefenderBatch(b, report, standing);
            ReadSpawnedAfterPick(b, report);
        }
    }

    // Every defender batch, garrison or bought pack, goes to the recorded place of its class, since the formation's own order position moves with the AI. Attackers never do.
    private void UseMusterPlace(FormationSlot slot)
    {
        var frame = _plan.Muster?.For(slot);
        if (frame == null || _musterFallback.Contains(slot))
            return;

        if (_field.SetMusterFrame(slot, frame))
            return;

        _musterFallback.Add(slot);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.MusterId, _mission + slot, LongDefeatChecks.MusterPlaceLost(slot));
        _logger.LogWarning($"{Tag}[Muster] the recorded {slot} place ({F1(frame.X)}, {F1(frame.Y)}, {F1(frame.Z)}) has no navmesh; defenders of that class spawn at the scene's frame");
    }

    private void LogSpawnLevel(SoldierBatch b, bool attackers, LdSpawnReport report, bool last)
    {
        var key = (attackers, b.Slot);
        if (b.FirstIndex == 0 || !_tallies.TryGetValue(key, out var tally))
            _tallies[key] = tally = new BatchTally();

        tally.Requested += b.Count;
        tally.Spawned += report.Spawned;
        tally.Far += report.Far;
        tally.OffMesh += report.OffMesh;
        tally.OnPoint += report.OnPoint;
        if (!last)
            return;

        _tallies.Remove(key);
        var wrong = tally.Far > 0 || tally.OffMesh > 0;
        _logger.LogInfo($"{Tag} spawn level {(wrong ? "WRONG" : "ok")}: {(attackers ? "attackers" : "defenders")} {b.Slot} {tally.Spawned} of {tally.Requested}, far {tally.Far}, off the navmesh {tally.OffMesh}, on the frame point {tally.OnPoint}");
    }

    private void JudgeDefenderBatch(SoldierBatch b, LdSpawnReport report, IReadOnlyList<LdPoint> standing)
    {
        var overlap = MusterPlan.CountOverlaps(report.Points, standing, LongDefeatChecks.OverlapRadius);
        if (!b.Reinforcement)
        {
            if (b.FirstIndex == 0 && b.Slot == FormationSlot.Infantry)
                _musterBatchesSpawned++;
            var label = b.Slot == FormationSlot.Infantry ? $"{_musterBatchesSpawned}/{_run.MusterBatchCount}" : "archers";
            _logger.LogInfo($"{Tag}[Muster] batch {label} {b.Slot} {b.TroopId} count={report.Spawned} on_point={report.OnPoint} overlap={overlap} far={report.Far} off_mesh={report.OffMesh}");
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.MusterId, _mission, LongDefeatChecks.Muster(report.OnPoint, overlap, report.Far, report.OffMesh, report.Spawned, _plan.Muster?.For(b.Slot) != null && !_musterFallback.Contains(b.Slot) ? "recorded" : "scene"));
            return;
        }

        var offer = _plan.Shop.Offers.FirstOrDefault(o => o.TroopId == b.TroopId && o.PackSize + _run.Rules.PackBonus == b.Count);
        var key = offer?.Id ?? b.TroopId;
        _logger.LogInfo($"{Tag}[Shop] pack {key} count={report.Spawned} of {b.Count} on_point={report.OnPoint} overlap={overlap}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.ShopId, key, LongDefeatChecks.Shop(b.Count, report.Spawned, report.OnPoint, overlap));
    }

    private void OnEvent(RunEvent e)
    {
        switch (e.Kind)
        {
            case RunEventKind.Charge:
                _chargedWave = e.Wave;
                _logger.LogInfo($"{Tag}[Wave] {e.Wave} stale: both teams charge");
                break;
            case RunEventKind.Felled:
                _logger.LogInfo($"{Tag}[Wave] {e.Wave} stragglers fall: {e.Detail}");
                break;
            case RunEventKind.Bought:
                _logger.LogInfo($"{Tag}[Shop] bought {e.Detail}");
                break;
            case RunEventKind.Horn:
                LongDefeatChecks.Record(_outcomes, LongDefeatChecks.EarlyStartId, "w" + e.Wave,
                    LongDefeatChecks.EarlyStart(_run.LastHornSecondsLeft, _run.LastHornBonus, _plan.Rules, _run.Factors.EarlyGold));
                break;
            case RunEventKind.BoonOffered:
            case RunEventKind.BoonLapsed:
            case RunEventKind.BoonTaken:
            case RunEventKind.Rerolled:
                OnBoonEvent(e);
                break;
            case RunEventKind.WaveCleared:
                OnWaveCleared(e);
                break;
        }
    }

    private void OnWaveCleared(RunEvent e)
    {
        FlushScale();
        var stats = _run.Stats.Waves.LastOrDefault(w => w.Wave == e.Wave);
        if (stats != null)
        {
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.WaveId, "w" + e.Wave,
                LongDefeatChecks.Wave(stats, _plan.Waves.ForWave(e.Wave).Count, _plan.Rules.StragglerShare));
            _logger.LogInfo($"{Tag}[Wave] {e.Wave} cleared in {F1(stats.Seconds)} s: kills {stats.Kills}, felled {stats.Felled}, lost {stats.Lost}; {e.Detail}");
        }

        _logger.LogInfo($"{Tag}[Player] wave {e.Wave} {_field.DescribePlayer()} kills={_run.Stats.TotalKills}");

        if (_chargedWave == e.Wave)
        {
            _chargedWave = 0;
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.StaleId, "w" + e.Wave, LongDefeatChecks.StaleCleared());
        }

        if (_run.Result != null || _mainBody != 0)
            return;

        _respawned = true;
        if (!SpawnLord(true))
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PlayerDeathId, "d" + _run.Stats.PlayerDeaths, LongDefeatChecks.PlayerDeath(false, "respawn-refused"));
    }

    /// <summary>The end of the run, from the tick that ended it or from <see cref="Shutdown"/>. Once.</summary>
    private void Finish()
    {
        if (_finished || _run.Result is not { } result)
            return;

        _finished = true;
        if (_chargedWave != 0)
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.StaleId, "w" + _chargedWave, LongDefeatChecks.StaleStranded(result.Reason, _run.GetView().DefendersStanding));
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.NoRoutId, _mission, LongDefeatChecks.NoRout(_run.Stats));
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GoldId, _mission, LongDefeatChecks.Gold(_run.Purse, _run.Stats, _run.WavesCleared));
        ReportText = LongDefeatRunReport.Build(_plan.FortressId, _plan.Options, _plan.Provenance, result, _run.Stats, _run.Purse, _startedUtc, DateTime.UtcNow, _session.MaxSpeed, _run.BoonSummary());
        var written = WriteReport();
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.EndId, _mission, LongDefeatChecks.End(result, ReportText.Length > 0 && written));
        SaveBestRun(result);
        _logger.LogInfo($"{Tag}[End] {result.Reason} at wave {result.Wave}: {result.WavesCleared} cleared, {result.Kills} kills, {_run.Stats.WavesFelled} wave(s) ended by felling, defenders {_run.GetView().DefendersStanding}");
        FlushScale();
    }

    private bool WriteReport()
    {
        var file = _records?.Report;
        if (file == null || ReportText.Length == 0)
            return true;

        if (file.TryWrite(_mission, ReportText, out var error))
        {
            _session.RecordReport(file.PathFor(_mission));
            return true;
        }

        _logger.LogWarning($"{Tag}[End] the report file was not written: {error}");
        return false;
    }

    private void SaveBestRun(RunResult result)
    {
        var store = _records?.Best;
        if (store == null)
            return;

        var run = new BestRun(result.WavesCleared, result.Kills);
        var current = store.Load(_plan.FortressId);
        if (!run.IsBetter(current))
        {
            _session.RecordBest(current!);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BestRunId, _mission, LongDefeatChecks.BestRunKept(run, current!));
            return;
        }

        var saved = store.TrySave(_plan.FortressId, run, out var error);
        if (saved)
            _session.RecordBest(run);
        else
        {
            if (current != null)
                _session.RecordBest(current);
            _logger.LogWarning($"{Tag}[End] the best run was not saved: {error}");
        }

        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BestRunId, _mission,
            LongDefeatChecks.BestRunSaved(run, saved ? store.Load(_plan.FortressId) : null, error));
    }
}
