using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The commander and summon proof (phase2-plan.md section 6): the commander is told to the run once the engine has spawned him, his spawn health is read back, a war cry's start and end refresh the
/// standing attackers, and the <c>commander</c> and <c>summon</c> checks are written when his wave clears. Risk 1 (does a hero spawned mid-wave fight as a soldier) is logged every state window.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private readonly Dictionary<int, SummonTally> _summons = new();
    private LdScaleSample? _commanderAtSpawn;

    /// <summary>What one summon did: how many it queued, how many of its soldiers the field spawned, and when (wave seconds).</summary>
    private sealed class SummonTally
    {
        public int Queued;
        public int Spawned;
        public float FiredAt;
        public float LastSpawnedAt;
        public string AbilityId = "";
        public bool Judged;
    }

    private void BeginCommander()
    {
        _summons.Clear();
        _commanderAtSpawn = null;
    }

    private SummonTally TallyFor(int use)
    {
        if (!_summons.TryGetValue(use, out var tally))
            _summons[use] = tally = new SummonTally();
        return tally;
    }

    /// <summary>An attacker batch has spawned: the commander's tells the run he is up (or gone, when nothing spawned), a summon's counts toward its use.</summary>
    private void OnAttackerBatchSpawned(SoldierBatch batch, LdSpawnReport report)
    {
        if (batch.Role == AttackerRole.Summoned)
        {
            var tally = TallyFor(batch.Use);
            tally.Spawned += report.Spawned;
            if (report.Spawned > 0)
                tally.LastSpawnedAt = _run.WaveSeconds;
        }
        else if (batch.Role == AttackerRole.Commander)
            OnCommanderSpawned(batch, report);
    }

    private void OnCommanderSpawned(SoldierBatch batch, LdSpawnReport report)
    {
        var spec = _run.Commander?.Spec;
        if (report.Spawned == 0)
        {
            _logger.LogWarning($"{Tag}[Commander] w{_run.Wave} {batch.TroopId} could not be spawned; the wave goes on without him");
            _run.CommanderDown(false);
            return;
        }

        _run.CommanderUp();
        _commanderAtSpawn = _effects?.ReadCommander();
        var read = _commanderAtSpawn is { HasAgent: true } sample ? F2(sample.Ratio) : "unread";
        _logger.LogInfo($"{Tag}[Commander] w{_run.Wave} spawned {batch.TroopId} rank={spec?.Rank.Id} steps={spec?.Steps} health_x={F2(batch.HealthFactor)} read_x={read} "
            + $"abilities={string.Join(",", spec?.Abilities.Select(a => a.Id) ?? new string[0])}");
    }

    /// <summary>A war cry began or ended (or its commander fell): the attackers standing derive their driven properties again, from the table already published.</summary>
    private void RefreshAttackerStats()
    {
        if (_effects == null)
            return;

        var touched = _effects.RefreshAttackers();
        _logger.LogInfo($"{Tag}[Commander] refresh attackers n={touched.Count} ms={F2((float)touched.Milliseconds)}");
    }

    private void OnCommanderEvent(RunEvent e)
    {
        switch (e.Kind)
        {
            case RunEventKind.WarCry:
                _logger.LogInfo($"{Tag}[Commander] w{e.Wave} war cry {e.Detail}");
                break;
            case RunEventKind.WarCryEnded:
                _logger.LogInfo($"{Tag}[Commander] w{e.Wave} war cry over ({e.Detail})");
                break;
            case RunEventKind.Summoned:
                var tally = TallyFor(IntField(e.Detail, "use="));
                tally.Queued = IntField(e.Detail, "count=");
                tally.FiredAt = FloatField(e.Detail, "at=");
                tally.AbilityId = TextField(e.Detail, "ability=");
                _logger.LogInfo($"{Tag}[Commander] w{e.Wave} summon {e.Detail}");
                break;
            case RunEventKind.CommanderEnded:
                var commander = _run.Commander;
                _logger.LogInfo($"{Tag}[Commander] w{e.Wave} ended {e.Detail} after {F1(commander?.AliveSeconds ?? 0f)} s, uses {Uses(commander)}");
                break;
        }
    }

    private static string Uses(CommanderState? commander) =>
        commander == null ? "none" : string.Join(",", commander.Uses.Select(u => u.Key + ":" + u.Value));

    /// <summary>The wave cleared: its commander's check, and one check per summon. A wave with no commander writes nothing.</summary>
    private void CommanderCleared(int wave, WaveStats stats)
    {
        var spec = _plan.Waves.Commanders.ForWave(wave);
        if (spec == null || stats.Commander is not { } record)
            return;

        var endless = _plan.Waves.ForWave(wave).HealthFactor;
        LdScaleSample? atSpawn = _effects == null ? null : _commanderAtSpawn ?? new LdScaleSample("", 0f, 0f, 0);
        var verdict = LongDefeatCommanderChecks.Commander(wave, record, spec, endless, atSpawn, _run.OwnedBoons);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.CommanderId, "w" + wave, verdict);
        _logger.LogInfo($"{Tag}[Commander] w{wave} {verdict.Result.ToString().ToLowerInvariant()} {verdict.Detail}");

        foreach (var use in _summons.Keys.OrderBy(k => k).ToList())
        {
            var tally = _summons[use];
            tally.Judged = true;
            var expected = spec.Abilities.FirstOrDefault(a => a.Kind == CommanderAbilityKind.Summon && a.Id == tally.AbilityId)?.Count ?? 0;
            var summon = LongDefeatCommanderChecks.Summon(wave, use, tally.Queued, expected, tally.Spawned, tally.FiredAt, tally.LastSpawnedAt, stats.Seconds);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SummonId, $"w{wave}s{use}", summon);
            _logger.LogInfo($"{Tag}[Summon] {summon.Result.ToString().ToLowerInvariant()} {summon.Detail}");
        }
    }

    /// <summary>The run ended inside a wave: a summon whose soldiers had not all come is a skip, never a pass or a failure.</summary>
    private void FinishCommander()
    {
        foreach (var pair in _summons.Where(s => !s.Value.Judged).OrderBy(s => s.Key).ToList())
        {
            pair.Value.Judged = true;
            var skip = LongDefeatCommanderChecks.SummonUnfinished(_run.Wave, pair.Key, pair.Value.Queued, pair.Value.Spawned);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.SummonId, $"w{_run.Wave}s{pair.Key}", skip);
        }
    }

    /// <summary>Every state window while he stands: where he is, his health and what his formation does (risk 1).</summary>
    private void LogCommander()
    {
        if (_effects == null || _run.Commander is not { Up: true, Ended: false })
            return;

        _logger.LogInfo($"{Tag}[Commander] w{_run.Wave} {_effects.DescribeCommander()}");
    }

    private static int IntField(string detail, string key) =>
        int.TryParse(Field(detail, key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static float FloatField(string detail, string key) =>
        float.TryParse(Field(detail, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;

    private static string TextField(string detail, string key)
    {
        var start = detail.IndexOf(key, System.StringComparison.Ordinal);
        return start < 0 ? "" : new string(detail.Substring(start + key.Length).TakeWhile(c => !char.IsWhiteSpace(c)).ToArray());
    }

    private static string Field(string detail, string key)
    {
        var start = detail.IndexOf(key, System.StringComparison.Ordinal);
        return start < 0 ? "" : new string(detail.Substring(start + key.Length).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
    }
}
