using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static TAOM.Features.StagedSiege.Domain.RunReportFormat;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// What one staged walls battle did, collected through plain methods while it runs and printed once when it ends
/// (docs/features/staged-siege.md "Run report", #1054): a header line, one line per stage reached, one check line per in-game
/// check with a verdict and the numbers behind it, and a summary. No engine types, no logger and no clock: the service feeds
/// it and logs the lines <see cref="Build"/> returns (without the tag). Main thread only, and nothing in here throws: a report
/// must never be the thing that breaks a battle. Check ids never change once shipped, so a grep over the log stays valid.
/// </summary>
public sealed class StagedSiegeRunReport
{
    /// <summary>A reinforcement batch spreads in ranks, so up to this far from the staged frame still means the frame resolved.</summary>
    public const float FrameToleranceMetres = 25f;

    /// <summary>The engine releases a batch the moment the check approves, so a side empty this long means the release did not help.</summary>
    public const float SoftlockLimitSeconds = 15f;

    /// <summary>Time a stage must last before an attacker peak below the minimum counts against the AI pathing inward.</summary>
    public const float InwardLimitSeconds = 120f;

    /// <summary>Every staged siege spawns one flag per point, A to D.</summary>
    public const int ExpectedFlags = 4;

    private const int StageCount = 4;

    private readonly int _minAttackers;
    private readonly float _refillBurstSeconds;
    private readonly StageRecord[] _stages = Enumerable.Range(0, StageCount).Select(_ => new StageRecord()).ToArray();
    private readonly PassCost _cost = new();

    private string _scene = string.Empty;
    private StagedSide _player;
    private bool _customBattle;
    private float _armedAt;

    private bool _setUp;
    private float _setUpAt;
    private bool _settingsLive;
    private int? _flags;
    private float? _lastPass;
    private int? _teams;

    private bool _detachmentFormed;
    private string? _detachmentNone;
    private int _detachmentStart;
    private int _detachmentMin;
    private int _detachmentMax;
    private string? _detachmentReleased;

    private int _gatesOpened;

    private readonly float?[] _softlockOpen = new float?[2];
    private readonly double[] _softlockLongest = new double[2];
    private readonly bool[] _softlockNonFinite = new bool[2];
    private int _softlockSpans;

    private bool? _cueRan;
    private StagedEnd _end;
    private float? _endAt;
    private string? _resultState;
    private bool _victory;
    private bool _defeated;
    private SpawnCounts? _survivorsAttacker;
    private SpawnCounts? _survivorsDefender;
    private string[]? _hudPairs;
    private string[]? _hudMarkers;
    private readonly List<string> _faults = new();

    public StagedSiegeRunReport(int minAttackers, float refillBurstSeconds)
    {
        _minAttackers = minAttackers;
        _refillBurstSeconds = refillBurstSeconds;
    }

    // --- facts -------------------------------------------------------------------------------------------------------

    public void NoteArmed(string scene, StagedSide playerSide, bool customBattle, float time)
    {
        _scene = scene;
        _player = playerSide;
        _customBattle = customBattle;
        _armedAt = time;
    }

    public void NoteTacticsInstalled(int teams) => _teams = teams;

    public void NoteSetUp(float time, bool settingsLive, SpawnCounts attacker, SpawnCounts defender)
    {
        _setUp = true;
        _setUpAt = time;
        _settingsLive = settingsLive;
        NoteStageStarted(StageId.A, time, attacker, defender);
    }

    /// <summary>How many flags the spawn returned. Noted on its own, so a throw after it cannot turn the count into zero.</summary>
    public void NoteFlags(int flags) => _flags = flags;

    public void NoteDetachmentFormed(int units)
    {
        _detachmentFormed = true;
        _detachmentStart = units;
        _detachmentMin = units;
        _detachmentMax = units;
    }

    public void NoteDetachmentNone(string reason) => _detachmentNone = reason;

    public void NoteDetachmentReading(int units)
    {
        if (!_detachmentFormed)
            return;
        _detachmentMin = Math.Min(_detachmentMin, units);
        _detachmentMax = Math.Max(_detachmentMax, units);
    }

    public void NoteDetachmentReleased(float time, string why) => _detachmentReleased ??= $"{Num(time)} s ({why})";

    public void NoteStageStarted(StageId stage, float time, SpawnCounts attacker, SpawnCounts defender)
    {
        var s = _stages[(int)stage];
        s.Started = true;
        s.Start = time;
        s.LastSeen = time;
        s.StartCounts[0] = attacker;
        s.StartCounts[1] = defender;
    }

    /// <summary>One reconcile pass. <paramref name="attackerStanding"/> and <paramref name="defenderStanding"/> are the sampled counts.</summary>
    public void NotePass(float time, float dt, StageId stage, int zoneAttackers, int zoneDefenders, CaptureStatus status, float progress,
        int attackerStanding, int defenderStanding)
    {
        _cost.Passes++;
        if (IsFinite(time))
            _lastPass = time;

        var s = _stages[(int)stage];
        if (IsFinite(time))
            s.LastSeen = time;
        s.PeakAttackers = Math.Max(s.PeakAttackers, zoneAttackers);
        s.PeakDefenders = Math.Max(s.PeakDefenders, zoneDefenders);
        if (IsFinite(progress))
            s.ProgressPeak = Math.Max(s.ProgressPeak, progress);
        if (IsFinite(dt) && dt > 0f)
            s.Seconds[SecondsSlot(status)] += dt;

        if (attackerStanding > 0)
            NoteSoftlockClosed(StagedSide.Attacker, time);
        if (defenderStanding > 0)
            NoteSoftlockClosed(StagedSide.Defender, time);
    }

    // A pass that ends in a capture counts its seconds as the bar rising.
    private static int SecondsSlot(CaptureStatus status) =>
        status == CaptureStatus.Falling ? 1 : status == CaptureStatus.Contested ? 2 : status == CaptureStatus.Idle ? 3 : 0;

    /// <summary>The cost of the pass just noted, in milliseconds.</summary>
    public void NotePassCost(double milliseconds) => _cost.Add(milliseconds);

    public void NoteStageCaptured(StageId stage, float time, bool forced)
    {
        var s = _stages[(int)stage];
        s.Captured = true;
        s.Forced = forced;
        s.End = time;
        if (!s.Started)
            s.Start = time;
    }

    public void NoteGatesOpened(int count) => _gatesOpened += count;

    /// <summary>One reinforcement built after set-up. The distance is null when the stage has no staged frame.</summary>
    public void NoteReinforcement(StagedSide side, StageId stage, float? distanceToFrame)
    {
        var i = (int)side;
        var s = _stages[(int)stage];
        s.Reinforcements[i]++;
        if (!distanceToFrame.HasValue)
            return;
        if (!IsFinite(distanceToFrame.Value))
        {
            s.Unmeasured++;
            return;
        }

        s.FrameSamples[i]++;
        s.FrameMax[i] = s.FrameMax[i].HasValue ? Math.Max(s.FrameMax[i]!.Value, distanceToFrame.Value) : distanceToFrame.Value;
    }

    /// <summary>The softlock branch approved a release. Approved again while open, it is the same span.</summary>
    public void NoteSoftlockOpened(StagedSide side, StageId stage, float time)
    {
        var i = (int)side;
        if (_softlockOpen[i].HasValue)
            return;
        _softlockOpen[i] = time;
        _softlockSpans++;
        _stages[(int)stage].Softlocks[i]++;
    }

    /// <summary>An agent was built for the side, or a pass sampled it standing: a span that is open closes.</summary>
    public void NoteSoftlockClosed(StagedSide side, float time)
    {
        var i = (int)side;
        if (!_softlockOpen[i].HasValue)
            return;
        KeepSpan(i, (double)time - _softlockOpen[i]!.Value);
        _softlockOpen[i] = null;
    }

    private void KeepSpan(int side, double span)
    {
        if (!IsFinite(span))
            _softlockNonFinite[side] = true;
        else
            _softlockLongest[side] = Math.Max(_softlockLongest[side], span);
    }

    public void NoteBudgetHeld(StagedSide side, StageId stage) => _stages[(int)stage].Holds[(int)side]++;

    public void NoteVictoryCue(bool ran) => _cueRan = ran;

    public void NoteStagedEnd(StagedEnd end, float time)
    {
        _end = end;
        _endAt = time;
    }

    public void NoteMissionResult(string state, bool victory, bool defeated, SpawnCounts? attacker, SpawnCounts? defender)
    {
        _resultState = state;
        _victory = victory;
        _defeated = defeated;
        _survivorsAttacker = attacker;
        _survivorsDefender = defender;
    }

    /// <summary>What the HUD showed: its <c>letter:kind</c> pairs and marker letters, or null when its view never ran.</summary>
    public void NoteHud(IReadOnlyCollection<string>? pairs, IReadOnlyCollection<string>? markers)
    {
        _hudPairs = pairs == null || pairs.Count == 0 ? null : pairs.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        _hudMarkers = markers?.OrderBy(m => m, StringComparer.Ordinal).ToArray();
    }

    /// <summary>A fault that took the service down. Only the first one is kept: the service ignores the rest.</summary>
    public void NoteFault(string site, string exceptionType)
    {
        if (!_faults.Any(f => f.StartsWith("service ", StringComparison.Ordinal)))
            _faults.Add($"service {site}: {exceptionType}");
    }

    /// <summary>A fault the mission edge caught around a call into the service. The service stays live; every one is kept.</summary>
    public void NoteBoundaryFault(string site, string exceptionType) => _faults.Add($"boundary {site}: {exceptionType}");

    // --- the report --------------------------------------------------------------------------------------------------

    public IReadOnlyList<string> Build()
    {
        var checks = Checks();
        var lines = new List<string> { Header() };
        for (var i = 0; i < StageCount; i++)
            if (Reached(i) || _stages[i].Captured)
                lines.Add(StageLine(i));
        foreach (var check in checks)
            lines.Add(CheckLine(check));
        lines.Add(Summary(checks, _cost, FaultText()));
        return lines;
    }

    private static string SideName(int side) => side == 0 ? "attacker" : "defender";

    private string FaultText() => _faults.Count == 0 ? "none" : string.Join("; ", _faults);

    private bool Reached(int i) => _stages[i].Started;

    private string ReachedName()
    {
        for (var i = StageCount - 1; i >= 0; i--)
            if (Reached(i))
                return ((StageId)i).ToString();
        return "none";
    }

    private string ResultText() => _resultState == null ? "none" : $"{_resultState} victory {_victory} defeated {_defeated}";

    private string Header()
    {
        var survivors = _survivorsAttacker.HasValue && _survivorsDefender.HasValue
            ? $"attacker {_survivorsAttacker.Value.Active}+{_survivorsAttacker.Value.Remaining} defender {_survivorsDefender.Value.Active}+{_survivorsDefender.Value.Remaining}"
            : "none";
        return $"REPORT walls: scene {_scene}, player {_player}, {(_customBattle ? "custom battle" : "campaign")}, armed {Num(_armedAt)} s, "
            + $"set up {Secs(_setUp ? _setUpAt : (float?)null)}, last pass {Secs(_lastPass)}, stage reached {ReachedName()}, end {_end}, "
            + $"result {_resultState ?? "none"} victory {_victory} defeated {_defeated}, survivors {survivors}";
    }

    private static float? Duration(StageRecord s)
    {
        var end = s.Captured ? s.End : s.LastSeen;
        var d = end - s.Start;
        return IsFinite(d) && d >= 0f ? d : (float?)null;
    }

    private static string Frame(float? max) => max.HasValue ? Num(max.Value) + " m" : "none";

    private string StageLine(int i)
    {
        var s = _stages[i];
        var name = ((StageId)i).ToString();
        if (!s.Started)
            return $"REPORT stage {name}: skipped, captured by {(s.Forced ? "forced" : "rule")} at {Secs(s.End)}";

        var end = s.Captured ? s.End : s.LastSeen;
        var captured = s.Captured ? "captured by " + (s.Forced ? "forced" : "rule") : "not captured";
        var sb = new StringBuilder($"REPORT stage {name}: start {Secs(s.Start)}, end {Secs(end)}, duration {Secs(Duration(s))}, {captured}, ");
        sb.Append($"zone peak attacker {s.PeakAttackers} defender {s.PeakDefenders}, progress peak {Num(s.ProgressPeak, "F2")}, ");
        sb.Append($"rising {Num(s.Seconds[0])} s falling {Num(s.Seconds[1])} s contested {Num(s.Seconds[2])} s idle {Num(s.Seconds[3])} s, ");
        sb.Append($"reinforcements attacker {s.Reinforcements[0]} defender {s.Reinforcements[1]}, ");
        sb.Append($"frame max attacker {Frame(s.FrameMax[0])} defender {Frame(s.FrameMax[1])}, unmeasured {s.Unmeasured}, ");
        sb.Append($"start counts attacker {s.StartCounts[0].Active}+{s.StartCounts[0].Remaining} defender {s.StartCounts[1].Active}+{s.StartCounts[1].Remaining}, ");
        sb.Append($"budget holds attacker {s.Holds[0]} defender {s.Holds[1]}, softlocks attacker {s.Softlocks[0]} defender {s.Softlocks[1]}");
        return sb.ToString();
    }

    private List<ReportCheck> Checks() => new()
    {
        TacticsCheck(),
        SettingsCheck(),
        FlagsCheck(),
        DetachmentCheck(),
        CaptureRuleCheck(),
        GatesCheck(),
        FramesCheck(),
        RefillCheck(),
        SoftlockCheck(),
        InwardCheck(),
        EndChainCheck(),
        CueCheck(),
        HudCheck(),
        FaultsCheck(),
    };

    private ReportCheck TacticsCheck()
    {
        if (!_teams.HasValue)
            return new("tactics", CheckVerdict.NotSeen, "never installed" + (_faults.Count > 0 ? "; fault first: " + FaultText() : string.Empty));
        return new("tactics", _teams.Value >= 1 ? CheckVerdict.Pass : CheckVerdict.Fail, $"installed on {_teams.Value} team(s)");
    }

    private ReportCheck SettingsCheck() => !_setUp
        ? new("spawn_settings", CheckVerdict.NotSeen, "no set-up")
        : new("spawn_settings", _settingsLive ? CheckVerdict.Pass : CheckVerdict.Fail, $"settings live {_settingsLive} at set-up");

    private ReportCheck FlagsCheck()
    {
        if (!_setUp)
            return new("flags", CheckVerdict.NotSeen, "no set-up");
        if (!_flags.HasValue)
            return new("flags", CheckVerdict.NotSeen, "the flag count was never read (the spawn did not return)");
        return new("flags", _flags.Value >= ExpectedFlags ? CheckVerdict.Pass : CheckVerdict.Fail, $"{_flags.Value} spawned, expected {ExpectedFlags}");
    }

    private ReportCheck DetachmentCheck()
    {
        if (!_setUp)
            return new("detachment", CheckVerdict.NotSeen, "no set-up");
        if (!_detachmentFormed)
            return new("detachment", CheckVerdict.NotSeen, "none formed: " + (_detachmentNone ?? "no reason recorded"));
        var text = $"start {_detachmentStart}, min {_detachmentMin}, max {_detachmentMax}, "
            + (_detachmentReleased != null ? "released " + _detachmentReleased : "not released");
        return _detachmentMax <= _detachmentStart
            ? new("detachment", CheckVerdict.Pass, text)
            : new("detachment", CheckVerdict.Fail, text + " (reinforcements joined it)");
    }

    private ReportCheck CaptureRuleCheck()
    {
        var rule = _stages.Count(s => s.Captured && !s.Forced);
        var forced = _stages.Count(s => s.Captured && s.Forced);
        return rule >= 1
            ? new("capture_rule", CheckVerdict.Pass, $"{rule} stage(s) captured by the rule, {forced} forced")
            : new("capture_rule", CheckVerdict.NotSeen, $"no capture by the rule, {forced} forced");
    }

    private ReportCheck GatesCheck()
    {
        if (!_stages[(int)StageId.B].Captured)
            return new("gates", CheckVerdict.NotSeen, "B not captured");
        return new("gates", _gatesOpened >= 1 ? CheckVerdict.Pass : CheckVerdict.Fail, $"B captured, {_gatesOpened} opened");
    }

    private ReportCheck FramesCheck()
    {
        var measured = 0;
        var unmeasured = 0;
        float? max = null;
        string worst = string.Empty;
        for (var i = 0; i < StageCount; i++)
        {
            unmeasured += _stages[i].Unmeasured;
            for (var side = 0; side < 2; side++)
            {
                measured += _stages[i].FrameSamples[side];
                var m = _stages[i].FrameMax[side];
                if (m.HasValue && (!max.HasValue || m.Value > max.Value))
                {
                    max = m;
                    worst = $"stage {(StageId)i} {SideName(side)}";
                }
            }
        }

        if (!max.HasValue)
            return new("reinforcement_frames", CheckVerdict.NotSeen, $"no measured sample, {unmeasured} unmeasured");
        var text = $"{measured} measured, {unmeasured} unmeasured, max {Num(max.Value)} m ({worst}), tolerance {Num(FrameToleranceMetres)} m";
        return max.Value <= FrameToleranceMetres
            ? new("reinforcement_frames", CheckVerdict.Pass, text)
            : new("reinforcement_frames", CheckVerdict.Fail, text);
    }

    private ReportCheck RefillCheck()
    {
        var evaluated = 0;
        var misses = new List<string>();
        var held = new List<string>();
        for (var i = (int)StageId.B; i < StageCount; i++)
        {
            var s = _stages[i];
            var duration = Duration(s);
            if (!s.Started || !duration.HasValue || !(duration.Value >= _refillBurstSeconds))
                continue;
            evaluated++;
            for (var side = 0; side < 2; side++)
            {
                if (s.StartCounts[side].Remaining == 0 || s.Reinforcements[side] > 0)
                    continue;
                // The budget rounds the cumulative allowance down, so a small reserve can get nothing for a stage on purpose.
                if (s.Holds[side] > 0)
                    held.Add($"stage {(StageId)i} {SideName(side)} held by budget");
                else
                    misses.Add($"stage {(StageId)i} {SideName(side)} 0 reinforcements in {Num(duration.Value)} s");
            }
        }

        if (misses.Count > 0)
            return new("refill", CheckVerdict.Fail, string.Join("; ", misses));
        return evaluated == 0
            ? new("refill", CheckVerdict.NotSeen, $"no stage from B on lasted {Num(_refillBurstSeconds)} s")
            : new("refill", CheckVerdict.Pass, $"{evaluated} stage(s) evaluated" + (held.Count > 0 ? "; " + string.Join("; ", held) : string.Empty));
    }

    private ReportCheck SoftlockCheck()
    {
        if (_softlockSpans == 0)
            return new("softlock", CheckVerdict.NotSeen, "no softlock occurred");
        var longest = new double[2];
        var bad = false;
        for (var side = 0; side < 2; side++)
        {
            longest[side] = _softlockLongest[side];
            if (_softlockNonFinite[side])
                bad = true;
            if (_softlockOpen[side].HasValue)
            {
                var span = (double)(_lastPass ?? _softlockOpen[side]!.Value) - _softlockOpen[side]!.Value;
                if (!IsFinite(span))
                    bad = true;
                else
                    longest[side] = Math.Max(longest[side], span);
            }
        }

        var overall = Math.Max(longest[0], longest[1]);
        var text = $"{_softlockSpans} span(s), longest {Num(overall, "F1")} s (attacker {SpanText(longest[0], 0)}, defender {SpanText(longest[1], 1)}), "
            + $"limit {Num(SoftlockLimitSeconds)} s";
        return !bad && overall < SoftlockLimitSeconds
            ? new("softlock", CheckVerdict.Pass, text)
            : new("softlock", CheckVerdict.Fail, text);
    }

    private string SpanText(double span, int side) =>
        span > 0d || _softlockOpen[side].HasValue || _softlockNonFinite[side] ? Num(span, "F1") + " s" : "none";

    private ReportCheck InwardCheck()
    {
        if (!_stages[(int)StageId.C].Started)
            return new("ai_inward", CheckVerdict.NotSeen, "C not reached");
        var peaks = new List<string>();
        var allEnough = true;
        string tooShort = string.Empty;
        for (var i = (int)StageId.C; i <= (int)StageId.D; i++)
        {
            var s = _stages[i];
            if (!s.Started)
                continue;
            var name = ((StageId)i).ToString();
            peaks.Add($"{name} peak {s.PeakAttackers}");
            if (s.PeakAttackers >= _minAttackers)
                continue;
            allEnough = false;
            var duration = Duration(s);
            if (duration.HasValue && duration.Value >= InwardLimitSeconds)
                return new("ai_inward", CheckVerdict.Fail,
                    $"{name} peak {s.PeakAttackers} below {_minAttackers} over {Num(duration.Value)} s");
            tooShort = $"{name} lasted {Secs(duration)}, too short to judge";
        }

        return allEnough
            ? new("ai_inward", CheckVerdict.Pass, string.Join(", ", peaks) + $" (minimum {_minAttackers})")
            : new("ai_inward", CheckVerdict.NotSeen, tooShort);
    }

    private static (string State, bool Victory, bool Defeated)? ExpectedResult(StagedEnd end)
    {
        switch (end)
        {
            case StagedEnd.DefenderPushedBack: return ("DefenderPullBack", false, false);
            case StagedEnd.AttackerWon: return ("AttackerVictory", true, false);
            case StagedEnd.PlayerDefeated: return ("AttackerVictory", false, true);
            default: return null;
        }
    }

    private ReportCheck EndChainCheck()
    {
        var expected = ExpectedResult(_end);
        if (!expected.HasValue)
            return new("end_chain", CheckVerdict.NotSeen, $"no staged end; result {ResultText()}; stage reached {ReachedName()}");
        var want = $"{expected.Value.State} victory {expected.Value.Victory} defeated {expected.Value.Defeated}";
        if (_resultState == null)
            return new("end_chain", CheckVerdict.Fail, $"{_end} at {Secs(_endAt)} expected {want}, no result");
        var matches = _resultState == expected.Value.State && _victory == expected.Value.Victory && _defeated == expected.Value.Defeated;
        return matches
            ? new("end_chain", CheckVerdict.Pass, $"{_end} at {Secs(_endAt)} gave {ResultText()}")
            : new("end_chain", CheckVerdict.Fail, $"{_end} at {Secs(_endAt)} expected {want}, got {ResultText()}");
    }

    private ReportCheck CueCheck()
    {
        if (_end != StagedEnd.AttackerWon)
            return new("victory_cue", CheckVerdict.NotSeen, $"end {_end}, no victory cue owed");
        if (_cueRan == true)
            return new("victory_cue", CheckVerdict.Pass, "AttackerWon, the cue ran");
        // Vanilla's BattleEndLogic is earlier in the behavior list and can end the battle in the same check, so a staged end that never
        // reached MissionEnded is not a defect of the cue. Only a throw fails.
        return _cueRan == false
            ? new("victory_cue", CheckVerdict.Fail, "AttackerWon, the cue threw")
            : new("victory_cue", CheckVerdict.NotSeen,
                "AttackerWon, but the staged end never reached MissionEnded (vanilla ended the battle first, or a boundary fault, see faults)");
    }

    private ReportCheck HudCheck()
    {
        if (_hudPairs == null)
            return new("hud", CheckVerdict.NotSeen, "the HUD never ran");
        if (ReachedName() == "none")
            return new("hud", CheckVerdict.NotSeen, "no stage reached");
        var kind = _player == StagedSide.Attacker ? "Take" : "Hold";
        var problems = new List<string>();
        for (var i = 0; i < StageCount; i++)
        {
            if (!Reached(i))
                continue;
            var letter = ((StageId)i).ToString();
            if (_hudPairs.Contains(letter + ":" + kind))
                continue;
            var shown = _hudPairs.FirstOrDefault(p => p.StartsWith(letter + ":", StringComparison.Ordinal));
            problems.Add(shown == null ? $"{letter} never shown" : $"{letter} shown as {shown.Substring(letter.Length + 1)}, expected {kind}");
        }

        // The marker letters are printed, never judged: a point can be off screen.
        var markers = _hudMarkers == null || _hudMarkers.Length == 0 ? "none" : string.Join(", ", _hudMarkers);
        var seen = $"shown {string.Join(", ", _hudPairs)}; markers {markers}";
        return problems.Count == 0
            ? new("hud", CheckVerdict.Pass, seen)
            : new("hud", CheckVerdict.Fail, string.Join("; ", problems) + $" ({seen})");
    }

    private ReportCheck FaultsCheck() => _faults.Count == 0
        ? new("faults", CheckVerdict.Pass, "no fault")
        : new("faults", CheckVerdict.Fail, FaultText());

    private sealed class StageRecord
    {
        public bool Started;
        public float Start;
        public float LastSeen;
        public bool Captured;
        public bool Forced;
        public float End;
        public int PeakAttackers;
        public int PeakDefenders;
        public float ProgressPeak;
        public readonly float[] Seconds = new float[4];
        public readonly int[] Reinforcements = new int[2];
        public readonly int[] FrameSamples = new int[2];
        public readonly float?[] FrameMax = new float?[2];
        public int Unmeasured;
        public readonly SpawnCounts[] StartCounts = new SpawnCounts[2];
        public readonly int[] Holds = new int[2];
        public readonly int[] Softlocks = new int[2];
    }
}
