using System;
using System.Collections.Generic;
using System.Linq;
using static TAOM.Features.StagedSiege.Domain.RunReportFormat;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// What one lord's hall fight did (docs/features/staged-siege.md "Run report", #1054): a header line, a zone line, one check line
/// per in-game check and a summary, in the same shape as <see cref="StagedSiegeRunReport"/>. No engine types and no logger; the
/// hall service feeds it and logs the lines <see cref="Build"/> returns (without the tag). Main thread only, and nothing in here throws.
/// </summary>
public sealed class StagedHallRunReport
{
    private const string TakeHall = "E:TakeHall";
    private const string HallTaken = "E:HallTaken";

    private readonly PassCost _cost = new();
    private readonly float[] _seconds = new float[4];

    private string _settlement = string.Empty;
    private int _area;
    private int _markers;
    private int? _flags;
    private float _armedAt;
    private (int Attackers, int Defenders)? _firstStanding;
    private float? _lastPass;
    private int _peakAttackers;
    private int _peakDefenders;
    private float _progressPeak;
    private float? _capturedAt;
    private bool? _cueRan;
    private string? _resultState;
    private bool _victory;
    private bool _defeated;
    private string[]? _hudPairs;
    private string[]? _hudMarkers;
    private readonly List<string> _faults = new();

    public void NoteArmed(string settlement, int area, int markers, float time)
    {
        _settlement = settlement;
        _area = area;
        _markers = markers;
        _armedAt = time;
    }

    /// <summary>How many flags the spawn returned (0 or 1). Noted on its own, so a throw after it cannot turn the count into zero.</summary>
    public void NoteFlags(int flags) => _flags = flags;

    /// <summary>A pass that counted the zone. The standing counts are the whole mission's, the zone counts the hall area's.</summary>
    public void NotePass(float time, float dt, int standingAttackers, int standingDefenders, int zoneAttackers, int zoneDefenders,
        CaptureStatus status, float progress)
    {
        NoteTime(time, standingAttackers, standingDefenders);
        _peakAttackers = Math.Max(_peakAttackers, zoneAttackers);
        _peakDefenders = Math.Max(_peakDefenders, zoneDefenders);
        if (IsFinite(progress))
            _progressPeak = Math.Max(_progressPeak, progress);
        if (IsFinite(dt) && dt > 0f)
            _seconds[status == CaptureStatus.Falling ? 1 : status == CaptureStatus.Contested ? 2 : status == CaptureStatus.Idle ? 3 : 0] += dt;
    }

    /// <summary>A pass that found a side with nobody standing and left the zone alone (vanilla ends such a hall).</summary>
    public void NotePassSkipped(float time, int standingAttackers, int standingDefenders) => NoteTime(time, standingAttackers, standingDefenders);

    private void NoteTime(float time, int standingAttackers, int standingDefenders)
    {
        _cost.Passes++;
        _firstStanding ??= (standingAttackers, standingDefenders);
        if (IsFinite(time))
            _lastPass = time;
    }

    public void NotePassCost(double milliseconds) => _cost.Add(milliseconds);

    public void NoteCaptured(float time) => _capturedAt = time;

    public void NoteCue(bool ran) => _cueRan = ran;

    public void NoteMissionResult(string state, bool victory, bool defeated)
    {
        _resultState = state;
        _victory = victory;
        _defeated = defeated;
    }

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

    public IReadOnlyList<string> Build()
    {
        var checks = new List<ReportCheck> { FlagCheck(), CaptureCheck(), EndCheck(), CueCheck(), HudCheck(), FaultsCheck() };
        var lines = new List<string> { Header(), ZoneLine() };
        foreach (var check in checks)
            lines.Add(CheckLine(check));
        lines.Add(Summary(checks, _cost, FaultText()));
        return lines;
    }

    private string FaultText() => _faults.Count == 0 ? "none" : string.Join("; ", _faults);

    private string ResultText() => _resultState == null ? "none" : $"{_resultState} victory {_victory} defeated {_defeated}";

    private string Header()
    {
        var standing = _firstStanding.HasValue ? $"attacker {_firstStanding.Value.Attackers} defender {_firstStanding.Value.Defenders}" : "none";
        return $"REPORT hall: settlement {_settlement}, area {_area}, markers {_markers}, armed {Num(_armedAt)} s, first standing {standing}, "
            + $"last pass {Secs(_lastPass)}, captured {Secs(_capturedAt)}, result {_resultState ?? "none"} victory {_victory} defeated {_defeated}";
    }

    private string ZoneLine() =>
        $"REPORT hall zone: peak attacker {_peakAttackers} defender {_peakDefenders}, progress peak {Num(_progressPeak, "F2")}, "
        + $"rising {Num(_seconds[0])} s falling {Num(_seconds[1])} s contested {Num(_seconds[2])} s idle {Num(_seconds[3])} s";

    private ReportCheck FlagCheck() => !_flags.HasValue
        ? new("hall_flag", CheckVerdict.NotSeen, "the flag count was never read (the spawn did not return)")
        : new("hall_flag", _flags.Value >= 1 ? CheckVerdict.Pass : CheckVerdict.Fail, $"{_flags.Value} spawned");

    private ReportCheck CaptureCheck() => _capturedAt.HasValue
        ? new("hall_capture", CheckVerdict.Pass, $"captured by the rule at {Secs(_capturedAt)}")
        : new("hall_capture", CheckVerdict.NotSeen, $"not captured; result {ResultText()}; peak attacker {_peakAttackers} defender {_peakDefenders}");

    private ReportCheck EndCheck()
    {
        if (!_capturedAt.HasValue)
            return new("hall_end", CheckVerdict.NotSeen, "not captured");
        if (_resultState == null)
            return new("hall_end", CheckVerdict.Fail, "captured, no result");
        var ok = _resultState == "AttackerVictory" && _victory && !_defeated;
        return new("hall_end", ok ? CheckVerdict.Pass : CheckVerdict.Fail, $"captured, result {ResultText()}");
    }

    private ReportCheck CueCheck()
    {
        if (!_capturedAt.HasValue)
            return new("hall_cue", CheckVerdict.NotSeen, "not captured");
        if (_cueRan == true)
            return new("hall_cue", CheckVerdict.Pass, "captured, the cue ran");
        return new("hall_cue", CheckVerdict.Fail, _cueRan == false ? "captured, the cue threw" : "captured, the cue never ran");
    }

    private ReportCheck HudCheck()
    {
        if (_hudPairs == null)
            return new("hud", CheckVerdict.NotSeen, "the HUD never ran");
        // The marker letters are printed, never judged: a point can be off screen.
        var markers = _hudMarkers == null || _hudMarkers.Length == 0 ? "none" : string.Join(", ", _hudMarkers);
        var seen = $"shown {string.Join(", ", _hudPairs)}; markers {markers}";
        var missing = new List<string>();
        if (!_hudPairs.Contains(TakeHall))
            missing.Add(TakeHall + " never shown");
        if (_capturedAt.HasValue && !_hudPairs.Contains(HallTaken))
            missing.Add(HallTaken + " never shown");
        return missing.Count == 0
            ? new("hud", CheckVerdict.Pass, seen)
            : new("hud", CheckVerdict.Fail, string.Join("; ", missing) + $" ({seen})");
    }

    private ReportCheck FaultsCheck() => _faults.Count == 0
        ? new("faults", CheckVerdict.Pass, "no fault")
        : new("faults", CheckVerdict.Fail, FaultText());
}
