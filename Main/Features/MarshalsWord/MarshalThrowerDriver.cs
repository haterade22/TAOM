using System;
using System.Collections.Generic;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>Starts, steps and ends a <see cref="ThrowerRun"/> per company on an Advance and publishes the stand points (build-spec 9.4); main thread only.</summary>
internal sealed class MarshalThrowerDriver
{
    private readonly IMarshalThrowerAdapter _adapter;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly OutcomeCounter _postfixCounter;
    private readonly Dictionary<int, Record> _records = new();
    private readonly HashSet<int> _refusedUntilOrderLapses = new();
    private readonly List<Watch> _watches = new();
    private readonly List<int> _ordered = new();
    private readonly List<ThrowerPoint> _points = new();
    private ThrowerPoint[] _lastPoints = Array.Empty<ThrowerPoint>();
    private double _nextPass = double.NegativeInfinity;
    private double _nextCost;
    private long _movedCounted;
    private int _serial;
    private bool _faulted;
    private bool _epochOpen;

    internal MarshalThrowerDriver(IMarshalThrowerAdapter adapter, IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _adapter = adapter;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
        _postfixCounter = outcomes.Counter(MarshalChecks.ThrowersPostfixCounterId);
    }

    internal int Epoch { get; private set; }

    internal int Published { get; private set; }

    internal void Pass(bool allowed)
    {
        var now = _adapter.Now;
        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;
        if (_faulted || SawFault(now))
            return;
        if (!allowed)
        {
            Drop();
            PublishPoints(now);
            return;
        }

        ApplyOrders(now);
        var commands = _adapter.PlayerCommands;
        if (commands)
        {
            for (var company = 0; company < MarshalFireTable.Companies; company++)
                PassCompany(company, now);
        }
        else
        {
            LoseAll("ai-control", now);
        }

        WatchMelee(now, commands);
        PublishPoints(now);
        WriteCost(now);
    }

    internal void OnAdvanceGiven(IReadOnlyList<int> companies) => _ordered.AddRange(companies);

    internal void OnPlayerTeamChanged()
    {
        _records.Clear();
        _refusedUntilOrderLapses.Clear();
        _watches.Clear();
        _ordered.Clear();
        PublishPoints(_adapter.Now);
    }

    internal void Summarize()
    {
        if (_epochOpen)
            EndEpoch();
    }

    internal ThrowerFacts? FactsOf(int company) => _records.TryGetValue(company, out var record) ? FactsOf(record) : null;

    private bool SawFault(double now)
    {
        var fault = MarshalHooks.ThrowerFault;
        if (fault == null)
            return false;
        _faulted = true;
        _logger.LogError("[MarshalsWord] the thrower stand failed: " + fault);
        var judged = 0;
        foreach (var record in _records.Values)
        {
            if (record.Gone || record.Judged)
                continue;
            Lose(record, "fault", now, faulted: true);
            judged++;
        }

        if (judged == 0)
            _outcomes.Fail(MarshalChecks.ThrowersStandId, _key + ":fault", "mission=" + _key + " reason=fault error=" + fault.GetType().Name);
        Drop();
        PublishPoints(now);
        return true;
    }

    private void Drop()
    {
        foreach (var record in _records.Values)
        {
            record.Gone = true;
            record.Judged = true;
        }

        _watches.Clear();
        _ordered.Clear();
        _refusedUntilOrderLapses.Clear();
    }

    private void ApplyOrders(double now)
    {
        if (_ordered.Count == 0)
            return;
        foreach (var company in _ordered)
        {
            _refusedUntilOrderLapses.Remove(company);
            if (_records.TryGetValue(company, out var record) && !record.Gone)
                End(record, "new-advance", now);
            FinishWatch(company, new ThrowerVerdict(ThrowerVerdictKind.Skip, "new-advance"), now);
        }

        _ordered.Clear();
    }

    private void LoseAll(string reason, double now)
    {
        foreach (var record in _records.Values)
        {
            if (!record.Gone && !record.Judged)
                Lose(record, reason, now, faulted: false);
        }
    }

    private void End(Record record, string reason, double now)
    {
        if (!record.Judged)
            Lose(record, reason, now, faulted: false);
        record.Gone = true;
    }

    private void Lose(Record record, string reason, double now, bool faulted)
    {
        record.Run.Lose(reason, now);
        WriteLine(record, now);
        Judge(record, faulted);
    }

    private void PassCompany(int company, double now)
    {
        var state = _adapter.Company(company);
        _records.TryGetValue(company, out var record);
        var live = record != null && !record.Gone;
        if (!state.OnAdvance || !state.HasUnits)
        {
            _refusedUntilOrderLapses.Remove(company);
            if (live)
                End(record!, state.HasUnits ? "order-changed" : "emptied", now);
            return;
        }

        if (live && record!.Run.State == ThrowerState.Released)
            return;
        if (_refusedUntilOrderLapses.Contains(company))
            return;
        var gates = _adapter.Gates(company);
        if (!live && MarshalThrowerRules.GateWhy(gates).Length > 0)
            return;
        var census = _adapter.Census(company);
        if (live)
            Step(record!, census, gates, now);
        else
            Start(company, census, gates, now);
    }

    private void Start(int company, ThrowerCensus census, ThrowerGates gates, double now)
    {
        if (MarshalThrowerRules.WhyNot(gates, census).Length > 0)
            return;
        var range = _adapter.Range(company);
        if (range == null || MarshalThrowerRules.WhyNot(gates with { Range = range }, census).Length > 0)
        {
            _refusedUntilOrderLapses.Add(company);
            return;
        }

        var gap = MarshalThrowerRules.Gap(gates.Own, gates.Target, gates.TargetDepth);
        var record = new Record(company, ++_serial, new ThrowerRun(census, range.Value, now), census.Units, MarshalFireTable.ShotsOf(company), gap);
        _records[company] = record;
        Step(record, census, gates, now);
    }

    private void Step(Record record, ThrowerCensus census, ThrowerGates gates, double now)
    {
        var run = record.Run;
        var gap = MarshalThrowerRules.Gap(gates.Own, gates.Target, gates.TargetDepth);
        var why = MarshalThrowerRules.WhyNot(gates with { Range = run.Range }, census);
        var approach = MarshalThrowerRules.Approach(gates.Own, gates.Target, MarshalThrowerRules.StandDistance(run.StandGap, gates.TargetDepth));
        run.Step(new ThrowerStep(census, why, gap, gates.Own, approach, now, gates.HasTarget));
        if (run.EverStood && record.ShotsAtStand < 0)
            record.ShotsAtStand = MarshalFireTable.ShotsOf(record.Company);
        record.Census = census;
        if (gates.HasTarget)
            record.Gap = gap;
        if (record.Logged != run.State || (run.State != ThrowerState.Released && now >= record.NextLine))
            WriteLine(record, now);
        if (run.State == ThrowerState.Released && !record.Judged)
            Judge(record, faulted: false);
    }

    private void WriteLine(Record record, double now)
    {
        var run = record.Run;
        record.Logged = run.State;
        record.NextLine = now + MarshalTuning.SummarySeconds;
        var point = run.Published is { } p ? One(p.X) + "," + One(p.Y) : "none";
        _logger.LogInfo("[Marshal] throwers company=" + Count(record.Company) + " state=" + run.State.ToString().ToLowerInvariant()
            + " why=" + (run.State == ThrowerState.Released ? run.ReleaseReason : "none") + " units=" + Count(record.Census.Units) + " armed=" + Count(record.Census.Armed)
            + " armed0=" + Count(run.Armed0) + " ammo=" + Count(record.Census.Ammo) + " range=" + One(run.Range) + " gap=" + One(record.Gap) + " stand_gap=" + One(run.StandGap)
            + " point=" + point + " overshoot=" + One(run.MaxOvershoot) + " speed=" + One(run.Speed) + " shots=" + Count(ShotsOf(record))
            + " run_shots=" + Count(RunShotsOf(record)));
    }

    private void Judge(Record record, bool faulted)
    {
        var run = record.Run;
        record.Shots = ShotsOf(record);
        record.RunShots = RunShotsOf(record);
        record.Judged = true;
        var key = _key + ":" + Count(record.Serial);
        var head = "mission=" + _key + " company=" + Count(record.Company);
        var stand = MarshalThrowerRules.JudgeStand(run.EverStood, run.ClosedBeforeStand, run.MaxOvershoot, faulted);
        var facts = " gap_at_stand=" + One(run.GapAtStand) + " stand_gap=" + One(run.StandGap) + " overshoot=" + One(run.MaxOvershoot);
        Write(MarshalChecks.ThrowersStandId, key, stand, head, stand.Kind == ThrowerVerdictKind.Skip ? " why=" + run.ReleaseReason : facts);
        if (faulted)
            return;
        var thrown = MarshalThrowerRules.JudgeThrow(run.EverStood, run.StoodSeconds, record.Shots);
        Write(MarshalChecks.ThrowersThrowId, key, thrown, head, thrown.Kind == ThrowerVerdictKind.Skip ? "" : " shots=" + Count(record.Shots) + " stood_s=" + One(run.StoodSeconds));
        if (run.ReleaseKind != ThrowerReleaseKind.Rule)
            return;
        record.Melee = "pending";
        _watches.Add(new Watch(record, MarshalFireTable.BlowsOf(record.Company), run.ReleasedAt, run.Position, run.DirX, run.DirY));
    }

    private void WatchMelee(double now, bool commands)
    {
        for (var i = _watches.Count - 1; i >= 0; i--)
        {
            var watch = _watches[i];
            var state = _adapter.Company(watch.Record.Company);
            if (state.HasUnits)
                watch.Note(_adapter.Gates(watch.Record.Company).Own);
            var blows = MarshalFireTable.BlowsOf(watch.Record.Company) - watch.BlowsAtRelease;
            var verdict = MarshalThrowerRules.JudgeMelee(state.OnAdvance, !state.HasUnits, state.AiControlled || !commands, blows > 0, watch.MostAdvanced, now - watch.ReleasedAt);
            if (verdict is { } judged)
                FinishWatch(watch, judged, now);
        }
    }

    private void FinishWatch(int company, ThrowerVerdict verdict, double now)
    {
        for (var i = _watches.Count - 1; i >= 0; i--)
        {
            if (_watches[i].Record.Company == company)
                FinishWatch(_watches[i], verdict, now);
        }
    }

    private void FinishWatch(Watch watch, ThrowerVerdict verdict, double now)
    {
        _watches.Remove(watch);
        var record = watch.Record;
        record.Blows = MarshalFireTable.BlowsOf(record.Company) - watch.BlowsAtRelease;
        record.MeleeSeconds = now - watch.ReleasedAt;
        record.Advanced = watch.MostAdvanced;
        record.Melee = verdict.Kind == ThrowerVerdictKind.Pass ? "pass" : verdict.Kind.ToString().ToLowerInvariant() + ":" + verdict.Reason;
        var detail = verdict.Kind == ThrowerVerdictKind.Skip ? "" : " blows=" + Count(record.Blows) + " melee_s=" + One(record.MeleeSeconds) + " advanced_m=" + One(record.Advanced);
        Write(MarshalChecks.ThrowersMeleeId, _key + ":" + Count(record.Serial), verdict, "mission=" + _key + " company=" + Count(record.Company), detail);
    }

    private void Write(string id, string key, ThrowerVerdict verdict, string head, string facts)
    {
        var reason = verdict.Kind == ThrowerVerdictKind.Pass ? "" : " reason=" + verdict.Reason;
        var detail = head + reason + facts;
        switch (verdict.Kind)
        {
            case ThrowerVerdictKind.Pass:
                _outcomes.Pass(id, key, detail);
                break;
            case ThrowerVerdictKind.Fail:
                _outcomes.Fail(id, key, detail);
                break;
            default:
                _outcomes.Skip(id, key, detail);
                break;
        }
    }

    private void PublishPoints(double now)
    {
        _points.Clear();
        for (var company = 0; company < MarshalFireTable.Companies; company++)
        {
            if (_records.TryGetValue(company, out var record) && !record.Gone && record.Run.State != ThrowerState.Released && record.Run.Published is { } point)
                _points.Add(new ThrowerPoint(company, point.X, point.Y));
        }

        if (SameAsLast())
            return;
        if (_points.Count > 0 && !_epochOpen)
            BeginEpoch(now);
        _adapter.Publish(_points);
        _lastPoints = _points.ToArray();
        if (_points.Count > 0)
            Published++;
        else if (_epochOpen)
            EndEpoch();
    }

    private bool SameAsLast()
    {
        if (_lastPoints.Length != _points.Count)
            return false;
        for (var i = 0; i < _points.Count; i++)
        {
            if (_lastPoints[i] != _points[i])
                return false;
        }

        return true;
    }

    private void BeginEpoch(double now)
    {
        Epoch++;
        Published = 0;
        _epochOpen = true;
        _movedCounted = 0;
        _nextCost = now + MarshalTuning.CostLineSeconds;
        MarshalThrowerCost.Reset();
    }

    private void WriteCost(double now)
    {
        if (!_epochOpen || now < _nextCost)
            return;
        _nextCost = now + MarshalTuning.CostLineSeconds;
        CostLine();
    }

    private void EndEpoch()
    {
        _epochOpen = false;
        var read = CostLine();
        var verdict = MarshalThrowerRules.CostVerdict(read.Published.Calls, read.Published.Ticks, read.Published.Most, Stopwatch.Frequency);
        var kind = verdict.Kind == ThrowerVerdictKind.Pass ? "" : " reason=" + verdict.Reason;
        var detail = "mission=" + _key + " epoch=" + Count(Epoch) + kind + " calls=" + Count(read.Published.Calls) + " mean_us=" + One(verdict.MeanMicros) + " max_us=" + One(verdict.MaxMicros);
        var key = _key + ":" + Count(Epoch);
        if (verdict.Kind == ThrowerVerdictKind.Pass)
            _outcomes.Pass(MarshalChecks.ThrowersCostId, key, detail);
        else if (verdict.Kind == ThrowerVerdictKind.Fail)
            _outcomes.Fail(MarshalChecks.ThrowersCostId, key, detail);
        else
            _outcomes.Skip(MarshalChecks.ThrowersCostId, key, detail);
    }

    private ThrowerCostRead CostLine()
    {
        var read = MarshalThrowerCost.Read();
        for (var i = _movedCounted; i < read.Moved; i++)
            _postfixCounter.Pass();
        _movedCounted = read.Moved;
        var published = MarshalThrowerRules.CostVerdict(read.Published.Calls, read.Published.Ticks, read.Published.Most, Stopwatch.Frequency);
        var other = MarshalThrowerRules.CostVerdict(read.Other.Calls, read.Other.Ticks, read.Other.Most, Stopwatch.Frequency);
        _logger.LogInfo("[Marshal] throwers-cost epoch=" + Count(Epoch) + " pub_calls=" + Count(read.Published.Calls) + " pub_mean_us=" + One(published.MeanMicros)
            + " pub_max_us=" + One(published.MaxMicros) + " adv_calls=" + Count(read.Other.Calls) + " adv_mean_us=" + One(other.MeanMicros) + " adv_max_us=" + One(other.MaxMicros)
            + " replaced=" + Count(read.Replaced) + " published=" + Count(Published) + " postfix=" + Count(read.Moved));
        return read;
    }

    private int ShotsOf(Record record) => record.Judged ? record.Shots : record.ShotsAtStand < 0 ? 0 : MarshalFireTable.ShotsOf(record.Company) - record.ShotsAtStand;

    private int RunShotsOf(Record record) => record.Judged ? record.RunShots : MarshalFireTable.ShotsOf(record.Company) - record.ShotsAtStart;

    private ThrowerFacts FactsOf(Record record)
    {
        var run = record.Run;
        return new ThrowerFacts(record.Company, record.Serial, run.State, run.ReleaseKind, run.ReleaseReason, record.Census.Units, run.Armed0, run.Ammo0, run.Range, run.StandGap,
            record.Gap0, run.GapAtStand, run.MinGap, run.MaxOvershoot, run.StoodSeconds, ShotsOf(record), RunShotsOf(record), run.ArmedAtRelease, run.ReleasedAt,
            run.EverStood, run.ClosedBeforeStand, record.Melee, record.Blows, record.MeleeSeconds, record.Advanced,
            run.Ammo0 - (run.State == ThrowerState.Released ? run.AmmoAtRelease : record.Census.Ammo));
    }

    private static string Count(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string One(double value) => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class Record
    {
        internal Record(int company, int serial, ThrowerRun run, int units, int shotsAtStart, float gap0)
        {
            Company = company;
            Serial = serial;
            Run = run;
            Census = new ThrowerCensus(units, run.Armed0, run.Ammo0);
            ShotsAtStart = shotsAtStart;
            Gap0 = gap0;
            Gap = gap0;
        }

        internal int Company { get; }

        internal int Serial { get; }

        internal ThrowerRun Run { get; }

        internal int ShotsAtStart { get; }

        internal float Gap0 { get; }

        internal ThrowerCensus Census { get; set; }

        internal float Gap { get; set; }

        internal int Shots { get; set; }

        internal int RunShots { get; set; }

        internal int ShotsAtStand { get; set; } = -1;

        internal float Advanced { get; set; }

        internal bool Judged { get; set; }

        internal bool Gone { get; set; }

        internal ThrowerState? Logged { get; set; }

        internal double NextLine { get; set; }

        internal string Melee { get; set; } = "none";

        internal int Blows { get; set; }

        internal double MeleeSeconds { get; set; }
    }

    private sealed class Watch
    {
        internal Watch(Record record, int blowsAtRelease, double releasedAt, GroundPoint from, float dirX, float dirY)
        {
            Record = record;
            BlowsAtRelease = blowsAtRelease;
            ReleasedAt = releasedAt;
            From = from;
            DirX = dirX;
            DirY = dirY;
        }

        internal GroundPoint From { get; }

        internal float DirX { get; }

        internal float DirY { get; }

        internal float MostAdvanced { get; private set; }

        /// <summary>Keeps the farthest the company has gone toward the enemy since the release, along the run's last direction to it.</summary>
        internal void Note(GroundPoint at)
        {
            var advanced = (at.X - From.X) * DirX + (at.Y - From.Y) * DirY;
            if (advanced > MostAdvanced)
                MostAdvanced = advanced;
        }

        internal Record Record { get; }

        internal int BlowsAtRelease { get; }

        internal double ReleasedAt { get; }
    }
}
