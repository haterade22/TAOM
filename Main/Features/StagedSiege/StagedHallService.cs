using System;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// Point E in the lord's hall (docs/features/staged-siege.md "The lord's hall"), for a settlement whose walls scene the catalog
/// records and whose saved siege state is the lord's hall. It arms in <c>EarlyStart</c> of the hall mission, then every
/// <c>reconcileSeconds</c> counts the attackers and defenders inside the hall's last fight area and runs the Phase 1
/// <see cref="CaptureRule"/> on it. When the bar fills it stops both spawners, announces the capture once and lets the mission
/// behavior end the mission as an attacker victory, the same result a vanilla hall win gives. The walls service owns the walls; this
/// one owns the hall, and the two never run in one mission. One instance per mission, main thread only; the HUD reads the
/// <see cref="HallSnapshot"/> published through <see cref="StagedSiegeSlot"/>.
///
/// Fault policy: every public method has a try. The first exception logs one error and clears the slot, and
/// <see cref="TryGetCaptured"/> stays false afterwards, so vanilla still ends the hall by depletion.
/// </summary>
public sealed class StagedHallService
{
    private readonly IStagedHallAdapter _hall;
    private readonly StagedSiegeSceneCatalog _catalog;
    private readonly IModLogger _logger;
    private readonly object _token;

    private HallZone _zone = HallZone.Build(null);
    private string _settlement = string.Empty;
    private string _verdict = "not evaluated";
    private bool _armed;
    private bool _faulted;
    private bool _shutdown;
    private bool _captured;
    private StagedHallRunReport? _report;
    private bool _reportWritten;
    private float _progress;
    private CaptureStatus _status = CaptureStatus.Idle;
    private float _nextPass = float.NegativeInfinity;
    private float _lastPass = float.NaN;
    private float _nextLog;

    public StagedHallService(IStagedHallAdapter hall, StagedSiegeSceneCatalog catalog, IModLogger logger)
    {
        _hall = hall;
        _catalog = catalog;
        _logger = logger;
        _token = hall.MissionToken;
    }

    /// <summary>Armed and not faulted or shut down.</summary>
    public bool IsArmed => _armed && !_faulted && !_shutdown;

    public bool IsFaulted => _faulted;

    public bool IsCaptured => _captured;

    public object MissionToken => _token;

    private static string F(float value, string format = "F1") => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private void Info(string message) => _logger.LogInfo(StagedSiegeService.Tag + " " + message);

    // --- arming ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Decides from the engine's readings, the saved siege state among them, whether point E is live in this hall mission. It never
    /// reads the walls fight's in-memory record, so a retreat, a reload and a restart arm the same as a straight win.
    /// </summary>
    public bool TryArm(bool enabled)
    {
        try
        {
            if (!_hall.IsHallMission)
            {
                _verdict = HallVerdict.NotAHallMission.ToString();
                return false;
            }

            _settlement = _hall.EncounterSettlementId;
            // The catalog is read only once every cheaper guard (the switch above all) has passed, so a hall fight with the
            // switch off never loads staged_siege.json; the verdict order stays the same.
            var verdict = StagedHallActivation.Decide(true, _hall.HasCampaign, _hall.IsClientOrReplay, _hall.IsInLordsHall, enabled,
                true, true);
            if (verdict == HallVerdict.Armed && !_catalog.TryGetScene(_hall.WallsSceneName, out _))
                verdict = HallVerdict.WallsSceneNotRecorded;
            if (verdict == HallVerdict.Armed)
            {
                _zone = HallZone.Build(_hall.ReadMarkers());
                if (_zone.IsEmpty)
                    verdict = HallVerdict.NoMarkers;
            }

            _verdict = verdict.ToString();
            if (verdict != HallVerdict.Armed)
            {
                Info($"hall inert: {verdict} (settlement {(_settlement.Length == 0 ? "none" : _settlement)})");
                return false;
            }

            _armed = true;
            _report = new StagedHallRunReport();
            // Fed before the calls that can throw, so a fault in the flag still reports the hall and the flag spawned.
            _report.NoteArmed(_settlement, _zone.AreaIndex, _zone.MarkerCount, _hall.Time);
            StagedSiegeSlot.ArmHall(this);
            var made = _hall.SpawnFlag(_zone.Centroid);
            _report.NoteFlags(made);
            if (made > 0)
                _hall.ColourFlag(StagedSide.Defender);
            Publish();
            Info($"hall armed: settlement {_settlement}, area {_zone.AreaIndex}, markers {_zone.MarkerCount}");
            return true;
        }
        catch (Exception ex)
        {
            Fault("TryArm", ex);
            return false;
        }
    }

    /// <summary>
    /// The mission edge caught an exception around a call into this service. It goes into the run report only: the service stays
    /// live, so this neither faults nor disarms it.
    /// </summary>
    public void NoteBoundaryFault(string site, Exception ex) => _report?.NoteBoundaryFault(site, ex.GetType().Name);

    // --- the tick ----------------------------------------------------------------------------------------------------

    public void Tick()
    {
        if (!_armed || _faulted || _shutdown || _captured)
            return;
        try
        {
            var now = _hall.Time;
            if (float.IsNaN(now) || float.IsInfinity(now) || now < _nextPass)
                return;
            _nextPass = now + _catalog.Tunables.ReconcileSeconds;
            var started = Stopwatch.GetTimestamp();
            Pass(now);
            _report?.NotePassCost((Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
        }
        catch (Exception ex)
        {
            Fault("Tick", ex);
        }
    }

    private void Pass(float now)
    {
        var dt = float.IsNaN(_lastPass) ? 0f : now - _lastPass;
        _lastPass = now;

        // A side with nobody standing is a finished hall: BattleEndLogic ends it within seconds (a depleted side at its next
        // once-a-second check, a routed one after 3 s), and it comes first in the logic list.
        var standingAttackers = _hall.CountStanding(StagedSide.Attacker);
        var standingDefenders = _hall.CountStanding(StagedSide.Defender);
        if (standingAttackers == 0 || standingDefenders == 0)
        {
            _report?.NotePassSkipped(now, standingAttackers, standingDefenders);
            Publish();
            return;
        }

        var count = _hall.CountInHall(_zone);
        var step = CaptureRule.Step(_progress, count.Attackers, count.Defenders, dt, _catalog.Tunables);
        _progress = step.Progress;
        _status = step.Status;
        _report?.NotePass(now, dt, standingAttackers, standingDefenders, count.Attackers, count.Defenders, step.Status, step.Progress);
        if (step.Status == CaptureStatus.Captured)
        {
            Capture(now);
            return;
        }

        Publish();
        if (now >= _nextLog)
        {
            _nextLog = now + 5f;
            Info($"hall E zone: attackers {count.Attackers} defenders {count.Defenders} progress {F(_progress, "F2")} {_status}");
        }
    }

    private void Capture(float now)
    {
        _captured = true;
        _report?.NoteCaptured(now);
        _hall.ColourFlag(StagedSide.Attacker);
        _hall.StopSpawners();
        try
        {
            _hall.AnnounceHallTaken();
            _report?.NoteCue(true);
        }
        catch (Exception)
        {
            // Still a fault, as before the report existed: the caller's catch turns it off. The report only needs to know the cue threw.
            _report?.NoteCue(false);
            throw;
        }

        Publish();
        Info($"hall E captured at {F(now)} s");
    }

    private void Publish() =>
        StagedSiegeSlot.PublishHall(new HallSnapshot(_token, _progress, _status, _zone.Centroid, _captured));

    // --- the end -----------------------------------------------------------------------------------------------------

    /// <summary>The hall was taken. Always false after a fault, so vanilla ends the hall.</summary>
    public bool TryGetCaptured() => _captured && !_faulted;

    public string DescribeStatus()
    {
        if (!_armed)
            return $"Staged hall: inert ({_verdict}).";
        return $"Staged hall: settlement {_settlement}, area {_zone.AreaIndex}, {_zone.MarkerCount} marker(s), progress {F(_progress, "F2")} "
            + $"{_status}, captured {_captured}{(_faulted ? ", FAULTED" : "")}.";
    }

    /// <summary>
    /// The mission's result as plain values, from <c>OnMissionResultReady</c>, for the run report. A faulted service still takes it.
    /// </summary>
    public void NoteMissionResult(string state, bool victory, bool defeated)
    {
        if (_report == null || _shutdown)
            return;
        _report.NoteMissionResult(state, victory, defeated);
    }

    // --- teardown ----------------------------------------------------------------------------------------------------

    public void Shutdown()
    {
        if (_shutdown)
            return;
        _shutdown = true;
        StagedSiegeSlot.ClearHallIf(_token);
        RecordCampaignSettlement();
        WriteReport();
    }

    // For StagedSiegeCampaignLog, which logs what became of this settlement once the map event ends.
    private void RecordCampaignSettlement()
    {
        if (_armed)
            StagedSiegeSlot.RecordCampaignSettlement(_settlement);
    }

    // Once, after a fault too: the report matters most then. It must never fail a mission end.
    private void WriteReport()
    {
        if (_report == null || _reportWritten)
            return;
        _reportWritten = true;
        try
        {
            var hud = StagedSiegeSlot.HudFor(_token);
            _report.NoteHud(hud?.Pairs, hud?.Markers);
            foreach (var line in _report.Build())
                Info(line);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{StagedSiegeService.Tag} run report failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Fault(string site, Exception ex)
    {
        if (_faulted)
            return;
        _faulted = true;
        _report?.NoteFault(site, ex.GetType().Name);
        try
        {
            _logger.LogError($"{StagedSiegeService.Tag} hall fault in {site}: {ex}; point E off, vanilla ends the hall");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }

        StagedSiegeSlot.ClearHallIf(_token);
    }
}
