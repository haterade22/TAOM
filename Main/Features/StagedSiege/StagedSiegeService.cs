using System;
using System.Text;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// One staged siege (docs/features/staged-siege.md, #1054): arms in <c>EarlyStart</c>, sets the battle up on the first pass after
/// deployment, then every <c>reconcileSeconds</c> counts the capture zone, advances the capture rule, moves the stage machine
/// and keeps the defender detachment, the flags, the gates and the AI behaviors in step. It also answers the engine's custom
/// reinforcement timer: each side may reserve troops for a batch only when the cadence is due and the stage's share of its reserve is
/// not spent (the engine spawns a batch once enough are reserved, see the feature doc).
/// One instance per mission, main thread only; the AI thread, the spawn-frame patch and the HUD read the published
/// <see cref="StageSnapshot"/> in <see cref="StagedSiegeSlot"/> instead.
///
/// Fault policy: every public method has a try. The first exception logs one error, hands the detachment back to the AI and
/// clears the slot, so the tactic weights fall to zero and vanilla's own siege tactics return; after that the timer falls back
/// to a plain trickle and <see cref="TryGetEnd"/> stays false, so vanilla still ends the battle.
/// </summary>
public sealed class StagedSiegeService
{
    internal const string Tag = "[StagedSiege]";

    private const float DriveSeconds = 2f;
    private const float LogSeconds = 5f;
    private const int ReinforcementLogsPerStage = 3;
    private const float DetachmentHeightBand = 10f;

    private readonly IStagedSiegeMissionAdapter _mission;
    private readonly StagedSiegeSceneCatalog _catalog;
    private readonly IModLogger _logger;
    private readonly object _token;

    private StagedSiegeTunables _t = StagedSiegeTunables.Default;
    private ScenePlan _scene = null!;
    private StageMachine _machine = null!;
    private ReinforcementCadence _cadence = null!;
    private ReserveBudget _budget = null!;

    private bool _armed;
    private bool _faulted;
    private bool _setUp;
    private bool _shutdown;
    private string _verdict = "not evaluated";

    private float _progress;
    private CaptureStatus _status = CaptureStatus.Idle;
    private float _nextPass = float.NegativeInfinity;
    private float _lastPass;
    private int _attackerReserve;
    private int _defenderReserve;
    private readonly int[] _standing = new int[2];

    private bool _detachmentActive;
    private bool _detachmentDriven;
    private int _detachmentStart;
    private DetachmentOrder _order = DetachmentOrder.Move;
    private float _nextDrive;

    private float _nextZoneLog;
    private float _nextDetachmentLog;
    private float _nextTacticsLog;
    private readonly float[] _nextReleaseLog = new float[2];
    private readonly bool[] _softlockLogged = new bool[2];
    private readonly bool[,] _heldLogged = new bool[2, 4];
    private readonly int[,] _builtLogged = new int[2, 4];

    public StagedSiegeService(IStagedSiegeMissionAdapter mission, StagedSiegeSceneCatalog catalog, IModLogger logger)
    {
        _mission = mission;
        _catalog = catalog;
        _logger = logger;
        _token = mission.MissionToken;
    }

    /// <summary>Armed and not faulted or shut down.</summary>
    public bool IsArmed => _armed && !_faulted && !_shutdown;

    public bool IsFaulted => _faulted;

    public object MissionToken => _token;

    /// <summary>The Balanced-method percentages the spawn-settings patch writes. Read only once armed.</summary>
    public StagedSpawnPlan SpawnPlan => _catalog.Tunables.SpawnPlan;

    private static string F(float value, string format = "F1") => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsFinite(float value) => !(float.IsNaN(value) || float.IsInfinity(value));

    private void Info(string message) => _logger.LogInfo(Tag + " " + message);

    // --- arming ------------------------------------------------------------------------------------------------------

    /// <summary>Decides from the engine's readings whether this battle is staged; the catalog is read last.</summary>
    public bool TryArm(bool enabled)
    {
        try
        {
            var playerSide = _mission.PlayerSide;
            var verdict = StagedSiegeActivation.Decide(_mission.IsSiegeBattle, _mission.HasSallyOutController, _mission.IsClientOrReplay,
                _mission.IsCampaignWallsAssault, enabled, playerSide.HasValue, true);
            ScenePlan scene = null!;
            if (verdict == ActivationVerdict.Armed && !_catalog.TryGetScene(_mission.SceneName, out scene))
                verdict = ActivationVerdict.UnrecordedScene;

            _verdict = verdict.ToString();
            if (verdict != ActivationVerdict.Armed)
            {
                LogInert(verdict);
                return false;
            }

            _t = _catalog.Tunables;
            _scene = scene;
            _machine = new StageMachine(playerSide!.Value);
            _cadence = new ReinforcementCadence(_t);
            _armed = true;
            // A record from an older walls fight must not be reported by the inert log of a later battle.
            StagedSiegeSlot.ClearLastEnded();
            StagedSiegeSlot.Arm(this);
            Publish();
            Info($"armed: scene {scene.Scene}, player {playerSide.Value}");
            return true;
        }
        catch (Exception ex)
        {
            Fault("TryArm", ex);
            return false;
        }
    }

    private void LogInert(ActivationVerdict verdict)
    {
        var text = $"inert: {verdict} (scene {_mission.SceneName}, siege state {_mission.SiegeStateName})";
        var previous = StagedSiegeSlot.LastEnded;
        if (previous.HasValue)
        {
            var ago = (DateTime.UtcNow - previous.Value.Utc).TotalSeconds;
            text += $"; previous staged siege {previous.Value.Scene} ended {previous.Value.End} {ago.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} s ago";
            StagedSiegeSlot.ClearLastEnded();
        }

        Info(text);
    }

    public void InstallTactics()
    {
        if (!IsArmed)
            return;
        try
        {
            Info($"S4 tactic installed on {_mission.InstallTactics()} team(s)");
        }
        catch (Exception ex)
        {
            Fault("InstallTactics", ex);
        }
    }

    public void NoteSpawnSettingsSwapped(string fromMethod)
    {
        if (!IsArmed)
            return;
        try
        {
            var plan = SpawnPlan;
            Info($"S2 spawn settings: {fromMethod} -> Balanced/CustomTimer (batch {F(plan.BatchPercentage, "0.###")}, desired {F(plan.DesiredPercentage, "0.###")}); timer installed");
        }
        catch (Exception ex)
        {
            Fault("NoteSpawnSettingsSwapped", ex);
        }
    }

    // --- the engine's reinforcement timer ----------------------------------------------------------------------------

    /// <summary>The engine reset the timer (it does so once, when reinforcements are enabled after deployment).</summary>
    public void ResetCadence(StagedSide side)
    {
        if (!_armed || _shutdown)
            return;
        try
        {
            _cadence.Reset(side, _mission.Time);
        }
        catch (Exception ex)
        {
            Fault("ResetCadence", ex);
        }
    }

    /// <summary>Whether this side may reserve reinforcements now. Asked by the engine every tick for each side.</summary>
    public bool ShouldReleaseBatch(StagedSide side)
    {
        if (!_armed || _shutdown)
            return false;
        try
        {
            var now = _mission.Time;
            if (_faulted)
                return Trickle(side, now);
            return _setUp && _machine.End == StagedEnd.None && MayReleaseNow(side, now);
        }
        catch (Exception ex)
        {
            Fault("ShouldReleaseBatch", ex);
            return false;
        }
    }

    private bool Trickle(StagedSide side, float now)
    {
        if (!_cadence.Due(side, now))
            return false;
        _cadence.Fired(side, now);
        return true;
    }

    private bool MayReleaseNow(StagedSide side, float now)
    {
        var i = (int)side;
        var stage = _machine.Current;
        var counts = _mission.ReadSpawnCounts(side);
        // The standing count is the one the last reconcile pass sampled, not a query per engine call.
        if (_budget.IsSoftlock(_standing[i], counts.Remaining))
        {
            if (!_softlockLogged[i])
                Info($"S2 softlock release {side}: standing 0, remaining {counts.Remaining}");
            _softlockLogged[i] = true;
            return true;
        }

        _softlockLogged[i] = false;
        if (!_cadence.Due(side, now))
            return false;

        var allowance = _budget.Allowance(side, stage);
        var spent = Math.Max(0, (side == StagedSide.Attacker ? _attackerReserve : _defenderReserve) - counts.Remaining);
        if (!_budget.MayRelease(side, stage, counts.Remaining, _standing[i]))
        {
            if (!_heldLogged[i, (int)stage])
                Info($"S2 budget held {side} stage {stage}: spent {spent} of {allowance}");
            _heldLogged[i, (int)stage] = true;
            return false;
        }

        _cadence.Fired(side, now);
        if (now >= _nextReleaseLog[i])
        {
            _nextReleaseLog[i] = now + LogSeconds;
            Info($"S2 reservation {side} stage {stage}: spent {spent}/{allowance}, remaining {counts.Remaining}, active {counts.Active}");
        }

        return true;
    }

    /// <summary>A human was built for a side: after deployment, log where the first few reinforcements appeared.</summary>
    public void NoteAgentBuilt(StagedSide side, StagePoint at)
    {
        if (!_armed || _faulted || _shutdown || !_setUp || _machine.End != StagedEnd.None)
            return;
        try
        {
            // A freshly built agent is standing. End a softlock now rather than at the next pass: every approved check
            // restarts the engine's reinforcement spawn slots (MissionBattleSideSpawnContext.cs:224-233), so approving
            // until the pass would stack the batch on one slot.
            _standing[(int)side] = Math.Max(_standing[(int)side], 1);
            var stage = _machine.Current;
            if (_builtLogged[(int)side, (int)stage] >= ReinforcementLogsPerStage)
                return;
            _builtLogged[(int)side, (int)stage]++;
            var frame = side == StagedSide.Attacker ? _scene.AttackerSpawn(stage) : _scene.DefenderSpawn(stage);
            var where = frame.HasValue
                ? $"{F(at.DistanceXY(frame.Value.Position))} m from the staged frame"
                : "no staged frame in stage A, vanilla spawn";
            Info($"S2 reinforcement {side} stage {stage} at ({F(at.X)}, {F(at.Y)}), {where}");
        }
        catch (Exception ex)
        {
            Fault("NoteAgentBuilt", ex);
        }
    }

    // --- the tick ----------------------------------------------------------------------------------------------------

    public void Tick()
    {
        if (!_armed || _faulted || _shutdown)
            return;
        try
        {
            var now = _mission.Time;
            if (!IsFinite(now) || now < _nextPass)
                return;
            _nextPass = now + _t.ReconcileSeconds;
            if (_machine.End != StagedEnd.None || !_mission.IsDeploymentFinished)
                return;
            if (_setUp)
                Pass(now);
            else
                SetUp(now);
        }
        catch (Exception ex)
        {
            Fault("Tick", ex);
        }
    }

    private void SetUp(float now)
    {
        _setUp = true;
        _lastPass = now;
        _nextDrive = now + DriveSeconds;
        var attacker = _mission.ReadSpawnCounts(StagedSide.Attacker);
        var defender = _mission.ReadSpawnCounts(StagedSide.Defender);
        _attackerReserve = attacker.Remaining;
        _defenderReserve = defender.Remaining;
        _budget = new ReserveBudget(attacker.Remaining, defender.Remaining, _t.ReserveShares, _machine.PlayerSide);
        SampleStanding();
        Info($"S2 deployment over: settings live {_mission.SpawnSettingsAreStaged}; reserve attacker {attacker.Remaining} defender {defender.Remaining}; "
            + $"active attacker {attacker.Active} defender {defender.Active}");

        SetUpFlags();
        SetUpDetachment();
        LogDefendBehaviors(_mission.EnsureDefendBehaviors(), _machine.Current);
        _mission.ResetTactics();
        Publish();
    }

    private void SampleStanding()
    {
        _standing[(int)StagedSide.Attacker] = _mission.CountStanding(StagedSide.Attacker);
        _standing[(int)StagedSide.Defender] = _mission.CountStanding(StagedSide.Defender);
    }

    private void SetUpFlags()
    {
        var zones = new[] { _scene.Zone(StageId.A), _scene.Zone(StageId.B), _scene.Zone(StageId.C), _scene.Zone(StageId.D) };
        var made = _mission.SpawnFlags(zones);
        for (var i = 0; i < zones.Length; i++)
            _mission.ColourFlag(zones[i].Id, StagedSide.Defender);
        var sb = new StringBuilder($"S2 flags: {made} spawned");
        foreach (var zone in zones)
            sb.Append($"; {zone.Id} ({F(zone.Center.X)}, {F(zone.Center.Y)}, {F(zone.Center.Z)})");
        Info(sb.ToString());
    }

    private void SetUpDetachment()
    {
        var infantry = _mission.ActiveDefenderInfantry();
        var size = DetachmentRule.Size(infantry, _t.DetachmentShare, _t.DetachmentCap);
        if (size == 0)
        {
            Info($"S3 detachment: none (defender infantry {infantry}, share {F(_t.DetachmentShare, "0.##")}, cap {_t.DetachmentCap})");
            return;
        }

        // Active before the call, so a fault halfway through still hands the formation back.
        _detachmentActive = true;
        var hold = _scene.Zone(StageId.A).Center;
        var reading = _mission.CreateDetachment(size, DetachmentRule.Slots(_scene.Detachment, size), hold, _scene.Detachment.Facing);
        _detachmentStart = reading.Units;
        _detachmentDriven = reading.Driven;
        if (reading.Units <= 0)
        {
            _detachmentActive = false;
            Info($"S3 detachment: none ({_mission.DetachmentNote})");
            return;
        }

        Info($"S3 detachment: {reading.Units} units {_mission.DetachmentNote}, driven {reading.Driven}");
    }

    private void Pass(float now)
    {
        var dt = now - _lastPass;
        _lastPass = now;
        SampleStanding();
        var stage = _machine.Current;
        var zone = _scene.Zone(stage);
        var count = _mission.CountInZone(zone.Center, zone.Radius, _t.HeightBand);
        var step = CaptureRule.Step(_progress, count.Attackers, count.Defenders, dt, _t);
        _progress = step.Progress;
        _status = step.Status;
        if (step.Status == CaptureStatus.Captured)
        {
            Apply(_machine.Capture(), now);
            return;
        }

        if (StagePlan.NeedsDefendBehavior(stage))
            LogDefendBehaviors(_mission.EnsureDefendBehaviors(), stage);
        DriveDetachment(now, stage);
        Publish();
        LogThrottled(now, stage, count);
    }

    // The effects of one or more captured points: flags, gates, cadence, detachment, snapshot, then the AI.
    private void Apply(StageTransition transition, float now)
    {
        var lastCaptured = transition.To.HasValue ? transition.To.Value - 1 : StageId.D;
        for (var captured = transition.From; captured <= lastCaptured; captured++)
        {
            _mission.ColourFlag(captured, StagedSide.Attacker);
            Info($"stage {captured} captured at {F(now)} s; next {(captured == StageId.D ? "end" : (captured + 1).ToString())}");
        }

        if (transition.OpenGates)
            Info($"S4 gates opened: {_mission.OpenGates()}");
        _cadence.OnStageChanged(now);
        _progress = 0f;
        _status = CaptureStatus.Idle;
        Array.Clear(_builtLogged, 0, _builtLogged.Length);
        if (transition.From == StageId.A)
            ReleaseDetachmentOnce();
        Publish();

        if (transition.End != StagedEnd.None)
        {
            Info($"S5 D captured: ending with {transition.End}");
            return;
        }

        LogDefendBehaviors(_mission.EnsureDefendBehaviors(), transition.To!.Value);
        _mission.ResetTactics();
    }

    private void DriveDetachment(float now, StageId stage)
    {
        if (!_detachmentActive || stage != StageId.A || now < _nextDrive)
            return;
        _nextDrive = now + DriveSeconds;

        var reading = _mission.ReadDetachment();
        if (reading.Units <= 0)
        {
            ReleaseDetachmentOnce();
            Info("S3 detachment: no units left, released");
            return;
        }

        var zone = _scene.Zone(StageId.A);
        if (_detachmentDriven)
        {
            var near = _mission.CountInZone(zone.Center, 2f * zone.Radius, DetachmentHeightBand).Attackers;
            var far = _mission.CountInZone(zone.Center, 3f * zone.Radius, DetachmentHeightBand).Attackers;
            var next = DetachmentRule.Decide(_order, near, far);
            if (next != _order)
            {
                _order = next;
                _mission.OrderDetachment(next, zone.Center, _scene.Detachment.Facing);
            }
        }

        if (now >= _nextDetachmentLog)
        {
            _nextDetachmentLog = now + LogSeconds;
            var distance = new StagePoint(reading.X, reading.Y, zone.Center.Z).DistanceXY(zone.Center);
            Info($"S3 detachment units {reading.Units}/{_detachmentStart} ai {_detachmentDriven} order {_order} {F(distance)} m from A");
        }
    }

    private void ReleaseDetachmentOnce()
    {
        if (!_detachmentActive)
            return;
        _detachmentActive = false;
        try
        {
            _mission.ReleaseDetachment();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} detachment release failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Publish() =>
        StagedSiegeSlot.Publish(StageSnapshot.Build(_token, _scene, _machine, _progress, _status));

    private void LogDefendBehaviors(int added, StageId stage)
    {
        if (added > 0)
            Info($"S4 BehaviorDefend added to {added} formation(s), stage {stage}");
    }

    private void LogThrottled(float now, StageId stage, ZoneCount count)
    {
        if (now >= _nextZoneLog)
        {
            _nextZoneLog = now + LogSeconds;
            Info($"stage {stage} zone: attackers {count.Attackers} defenders {count.Defenders} progress {F(_progress, "F2")} {_status}");
        }

        if (now >= _nextTacticsLog)
        {
            _nextTacticsLog = now + LogSeconds;
            Info("S4 tactics: " + _mission.DescribeTactics());
        }
    }

    // --- the end -----------------------------------------------------------------------------------------------------

    /// <summary>The siege ended through a captured D. Always false after a fault, so vanilla ends the battle.</summary>
    public bool TryGetEnd(out StagedEnd end)
    {
        end = _faulted || _machine == null ? StagedEnd.None : _machine.End;
        return end != StagedEnd.None;
    }

    /// <summary>Console: capture forward to a stage (null: through D). Returns the sentence to print.</summary>
    public string ForceStage(StageId? target)
    {
        if (!IsArmed)
            return $"The staged siege is not armed in this mission ({_verdict}).";
        if (!_setUp)
            return "The deployment is not over yet; try again once the battle has started.";
        try
        {
            var transition = _machine.Force(target);
            if (!transition.Changed)
                return $"no change: the siege is at stage {_machine.Current}.";
            Apply(transition, _mission.Time);
            return transition.End == StagedEnd.None
                ? $"Forced the siege from stage {transition.From} to stage {transition.To}."
                : $"Forced the siege from stage {transition.From} through D: it ends with {transition.End}.";
        }
        catch (Exception ex)
        {
            Fault("ForceStage", ex);
            return "The staged siege faulted: " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    public string DescribeStatus()
    {
        try
        {
            if (!_armed)
                return $"Staged siege: inert ({_verdict}); scene {_mission.SceneName}.";
            var sb = new StringBuilder();
            sb.AppendLine($"Staged siege: scene {_scene.Scene}, player {_machine.PlayerSide}, stage {_machine.Current}"
                + $", captured mask {_machine.CapturedMask}, gates opened {_machine.GatesOpened}, end {_machine.End}"
                + $"{(_faulted ? ", FAULTED" : "")}.");
            sb.AppendLine($"Capture: progress {F(_progress, "F2")} {_status}; set up {_setUp}.");
            sb.AppendLine($"Detachment: active {_detachmentActive}, driven {_detachmentDriven}, order {_order}, start {_detachmentStart}.");
            if (!_faulted && _setUp)
            {
                var a = _mission.ReadSpawnCounts(StagedSide.Attacker);
                var d = _mission.ReadSpawnCounts(StagedSide.Defender);
                sb.AppendLine($"Spawn: attacker active {a.Active} remaining {a.Remaining} (reserve {_attackerReserve}); "
                    + $"defender active {d.Active} remaining {d.Remaining} (reserve {_defenderReserve}).");
            }

            sb.Append("Tactics: " + _mission.DescribeTactics());
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "Staged siege status unavailable: " + ex.GetType().Name + ": " + ex.Message;
        }
    }

    // --- teardown ----------------------------------------------------------------------------------------------------

    public void Shutdown()
    {
        if (_shutdown)
            return;
        _shutdown = true;
        try
        {
            ReleaseDetachmentOnce();
            if (_armed && !_faulted && _machine.End != StagedEnd.None)
                StagedSiegeSlot.SetLastEnded(_scene.Scene, _machine.End, DateTime.UtcNow);
            StagedSiegeSlot.ClearIf(_token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} shutdown failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Fault(string site, Exception ex)
    {
        if (_faulted)
            return;
        _faulted = true;
        try
        {
            _logger.LogError($"{Tag} fault in {site}: {ex}; staged rules off, vanilla continues");
        }
        catch (Exception)
        {
            // Nothing left to report to.
        }

        ReleaseDetachmentOnce();
        StagedSiegeSlot.ClearIf(_token);
    }
}
