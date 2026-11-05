using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>A scripted engine for the Seat's state machine: facts and the camera are fields, every write is recorded.</summary>
internal sealed class FakeSeatScreen : ISeatScreenAdapter
{
    internal SeatEngineFacts Facts = new(Deployment: false, Interlude: false, BattleMode: true, PhotoMode: false, CustomCamera: false, MissionEnded: false,
        RageHoldsHero: false, RadialMenuActive: false);
    internal SeatPose Camera = new(100f, 200f, 103f, 0f, 0.1f);
    internal float ViewAngleDegrees = 65f;
    internal bool CameraReadable = true;
    internal float Ground = 100f;
    internal Func<float, float, float>? GroundAt;
    internal float? ClampX;
    internal SeatInput Input = default;
    internal bool LookWritten = true;
    internal bool GlideBegins = true;
    internal float HeightLimit;
    internal bool ClampGivesNaN;
    internal Exception? ApplyThrows;
    internal Exception? LookThrows;
    internal Exception? GlideThrows;
    internal Exception? ControllerOnThrows;

    internal readonly List<string> Calls = new();
    internal readonly List<(SeatPose Pose, float ViewAngle, float ListenerZ)> Frames = new();
    internal readonly List<bool> ControllerWrites = new();
    internal readonly List<(float Yaw, float Pitch)> Looks = new();
    internal int GlideCalls;
    internal int HeightLimitReads;
    internal readonly List<SeatMessage> Messages = new();
    internal readonly List<string?> MessageArguments = new();
    internal int InputReads;
    internal int FactsReads;
    internal readonly List<(float X, float Y)> GroundReads = new();

    // Stage S2 and S3: the panel, the cursor, the hidden interface and the kept target.
    internal bool PanelOpen;
    internal bool Look;
    internal bool ReopenWorks = true;
    internal bool EngineRefusesReopen;
    internal bool TransferOpen;
    internal int TransferReads;
    internal SeatCloseSignals Signals;
    internal bool UiHidden;
    internal bool Dialog;
    internal bool OtherLayer;
    internal SeatKeepKind KeepResult = SeatKeepKind.Soldier;
    internal SeatTargetRead Kept = new(false, 100f, 210f, 101f);
    internal readonly List<bool> CursorWrites = new();
    internal readonly List<bool> UiWrites = new();
    internal int Reopens;
    internal int Holds;
    internal int Unkeeps;
    internal int SignalReads;
    internal int DialogReads;
    internal int LayerReads;
    internal int KeepCalls;
    internal Exception? CursorThrows;
    internal Exception? UiThrows;
    internal Exception? KeepThrows;

    // Stage S4 and S5: the real clock, and the take of a soldier.
    internal float Real;
    internal int RealReads;
    internal SeatTakeResult TakeResult = SeatTakeResult.Taken(SeatTakeKind.Soldier, 2);
    internal int TakeCalls;
    internal Exception? TakeThrows;

    public SeatEngineFacts ReadFacts()
    {
        FactsReads++;
        return Facts;
    }

    public bool TryReadCamera(out SeatPose pose, out float viewAngle)
    {
        pose = Camera;
        viewAngle = ViewAngleDegrees;
        return CameraReadable;
    }

    public float GroundHeight(float x, float y, float fromZ)
    {
        GroundReads.Add((x, y));
        return GroundAt?.Invoke(x, y) ?? Ground;
    }

    public (float X, float Y) ClampToBoundary(float x, float y) =>
        ClampGivesNaN ? (float.NaN, float.NaN) : (ClampX.HasValue && x > ClampX.Value ? ClampX.Value : x, y);

    public float ReadHeightLimit()
    {
        HeightLimitReads++;
        return HeightLimit;
    }

    public SeatInput ReadInput(float dt)
    {
        InputReads++;
        return Input;
    }

    public void ApplyFrame(SeatPose pose, float viewAngle, float listenerZ)
    {
        if (ApplyThrows != null)
            throw ApplyThrows;
        Frames.Add((pose, viewAngle, listenerZ));
    }

    public void SetHeroController(bool enabled)
    {
        ControllerWrites.Add(enabled);
        Calls.Add(enabled ? "controller:on" : "controller:off");
        if (enabled && ControllerOnThrows != null)
            throw ControllerOnThrows;
    }

    public bool WriteLook(float yaw, float pitch)
    {
        if (LookThrows != null)
            throw LookThrows;
        Looks.Add((yaw, pitch));
        return LookWritten;
    }

    public bool BeginGlide()
    {
        if (GlideThrows != null)
            throw GlideThrows;
        GlideCalls++;
        return GlideBegins;
    }

    public void Say(SeatMessage message, string? argument = null)
    {
        Messages.Add(message);
        MessageArguments.Add(argument);
    }

    public bool IsPanelOpen() => PanelOpen;

    public bool LookHeld() => Look;

    public void SetCursor(bool show)
    {
        CursorWrites.Add(show);
        Calls.Add(show ? "cursor:on" : "cursor:off");
        if (CursorThrows != null)
            throw CursorThrows;
    }

    public void HoldCursorPosition() => Holds++;

    public SeatCloseSignals ReadCloseSignals()
    {
        SignalReads++;
        return Signals;
    }

    // A reopen that works opens the panel, as OpenToggleOrder does (MissionOrderVM.cs:720). EngineRefusesReopen is the engine returning
    // silently from its own CheckCanBeOpened (MissionOrderVM.cs:716-719): the site was reached and the panel stays closed.
    public bool OpenPanel()
    {
        Reopens++;
        if (ReopenWorks && !EngineRefusesReopen)
            PanelOpen = true;
        return ReopenWorks;
    }

    public bool TransferMenuOpen()
    {
        TransferReads++;
        return TransferOpen;
    }

    public bool ReadUiHidden() => UiHidden;

    public void WriteUiHidden(bool hidden)
    {
        UiWrites.Add(hidden);
        if (UiThrows != null)
            throw UiThrows;
        UiHidden = hidden;
    }

    public bool DialogShown()
    {
        DialogReads++;
        return Dialog;
    }

    public bool OtherLayerFocused()
    {
        LayerReads++;
        return OtherLayer;
    }

    public SeatKeepKind TryKeep()
    {
        KeepCalls++;
        if (KeepThrows != null)
            throw KeepThrows;
        return KeepResult;
    }

    public SeatTargetRead ReadKept() => Kept;

    public void Unkeep() => Unkeeps++;

    public float RealSeconds()
    {
        RealReads++;
        return Real;
    }

    public SeatTakeResult TryTake()
    {
        TakeCalls++;
        if (TakeThrows != null)
            throw TakeThrows;
        // Mission.TakeControlOfAgent clears fast forward before it sets the controller (Mission.cs:6407-6414).
        if (TakeResult.Outcome == SeatTakeOutcome.Taken)
            FastForward = false;
        return TakeResult;
    }

    // Stage S6: mission time, kept the way the engine keeps it. FastForward, FixedStep and Paused are the engine's flags. SlowRequests holds the
    // requests under the Seat's id: a remove with none present throws (Mission.cs:2115-2125), and an add never replaces (2100-2103).
    internal bool FastForward;
    internal bool FixedStep;
    internal bool Paused;
    internal bool OwnedElsewhere;
    internal readonly List<float> SlowRequests = new();
    internal readonly List<string> TimeWrites = new();
    internal int TimeReads;
    internal int FastApplies;
    internal int Stops;
    internal Exception? StopThrows;
    /// <summary>The flags stay up after a stop: another view that owns the clock writes them again every frame.</summary>
    internal bool StopDoesNotStick;
    internal int StopThrowsTimes = int.MaxValue;
    internal Exception? PausedThrows;
    internal Exception? RemoveThrows;
    internal float LastFastDt;

    public SeatTimeState ReadTime()
    {
        TimeReads++;
        return new SeatTimeState(FastForward, FixedStep, Paused, SlowRequests.Count > 0);
    }

    // FordsMissionSpeed.Set: level 10 raises the flag with no fixed step, 2 and 4 lower the flag and write the fixed step of a frame that has time.
    public void ApplyFastTime(int level, float realDt)
    {
        FastApplies++;
        LastFastDt = realDt;
        TimeWrites.Add("apply:" + level);
        FastForward = level == 10;
        FixedStep = level != 10 && realDt > 0f;
    }

    public void StopFastTime()
    {
        Stops++;
        TimeWrites.Add("stop");
        if (StopThrows != null && StopThrowsTimes-- > 0)
            throw StopThrows;
        if (StopDoesNotStick)
            return;
        FastForward = false;
        FixedStep = false;
    }

    public void AddSlowRequest(float factor)
    {
        TimeWrites.Add("add:" + factor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SlowRequests.Add(factor);
    }

    public void RemoveSlowRequest()
    {
        TimeWrites.Add("remove");
        if (RemoveThrows != null)
            throw RemoveThrows;
        if (SlowRequests.Count == 0)
            throw new ArgumentOutOfRangeException("index", "RemoveTimeSpeedRequest with no request of that id");
        SlowRequests.RemoveAt(SlowRequests.Count - 1);
    }

    public void SetPaused(bool paused)
    {
        TimeWrites.Add(paused ? "pause" : "resume");
        if (PausedThrows != null)
            throw PausedThrows;
        Paused = paused;
    }

    internal int OwnerReads;

    public bool ClockOwnedElsewhere()
    {
        OwnerReads++;
        return OwnedElsewhere;
    }

    // Stage S7: the lift of the third-person view. LiftWrites keeps every offset the Seat wrote, in order.
    internal bool RisePanelOpen;
    internal bool RiseFirstPerson;
    internal float RiseHeight = SeatPanelLift.FieldHeight;
    internal int RiseReads;
    internal int HeightReads;
    internal readonly List<float> LiftWrites = new();
    internal Exception? LiftThrows;

    public SeatRiseRead ReadRise()
    {
        RiseReads++;
        return new SeatRiseRead(RisePanelOpen, RiseFirstPerson, Facts.BattleMode);
    }

    public float LiftHeight()
    {
        HeightReads++;
        return RiseHeight;
    }

    public void SetPanelLift(float metres)
    {
        if (LiftThrows != null)
            throw LiftThrows;
        LiftWrites.Add(metres);
    }
}

internal sealed class FakeSeatHero : ISeatHeroAdapter
{
    internal int StandFasts;
    internal Exception? StandFastThrows;

    // Stage S4: the hand-off. The fake keeps the controller the way the engine does: Begin makes the hero the AI's unless it refuses, End makes him the player's.
    internal SeatFightRefusal BeginResult = SeatFightRefusal.None;
    internal Exception? BeginThrows;
    internal Exception? EndThrows;
    /// <summary>How many hand-backs throw before the hero's controller is written; the rest write it.</summary>
    internal int EndThrowsTimes = int.MaxValue;
    internal bool Active = true;
    internal bool OnAi;
    internal bool RetreatPending;
    internal int Begins;
    internal int Ends;
    internal int RetreatStops;
    internal int OnAiReads;
    internal readonly List<string> Calls = new();

    public void StandFast()
    {
        if (StandFastThrows != null)
            throw StandFastThrows;
        StandFasts++;
    }

    public SeatFightRefusal BeginFighting()
    {
        Calls.Add("begin");
        if (BeginThrows != null)
            throw BeginThrows;
        Begins++;
        if (BeginResult == SeatFightRefusal.None)
            OnAi = true;
        return BeginResult;
    }

    public void EndFighting()
    {
        Calls.Add("end");
        if (EndThrows != null && EndThrowsTimes-- > 0)
            throw EndThrows;
        Ends++;
        OnAi = false;
    }

    public bool HeroActive() => Active;

    public bool HeroOnAi()
    {
        OnAiReads++;
        return Active && OnAi;
    }

    public bool StopHeroRetreat()
    {
        if (!RetreatPending)
            return false;
        RetreatPending = false;
        RetreatStops++;
        return true;
    }
}

/// <summary>The MCM values behind the Seat, set by the test, with a count of the reads.</summary>
internal sealed class FakeSeatSettings : ISeatSettingsProvider
{
    internal int Reads;
    private bool _enabled = true;
    private bool _beginBattlesInSeat;
    private bool _cursorWithPanel = true;
    private bool _keepPanelOpen;
    private bool _heroFightsOn;
    private bool _takeSoldierAfterFall = true;
    private bool _riseWithPanel;

    public bool Enabled
    {
        get
        {
            Reads++;
            return _enabled;
        }
        set => _enabled = value;
    }

    public bool BeginBattlesInSeat
    {
        get
        {
            Reads++;
            return _beginBattlesInSeat;
        }
        set => _beginBattlesInSeat = value;
    }

    public bool CursorWithPanel
    {
        get
        {
            Reads++;
            return _cursorWithPanel;
        }
        set => _cursorWithPanel = value;
    }

    public bool KeepPanelOpen
    {
        get
        {
            Reads++;
            return _keepPanelOpen;
        }
        set => _keepPanelOpen = value;
    }

    public bool HeroFightsOn
    {
        get
        {
            Reads++;
            return _heroFightsOn;
        }
        set => _heroFightsOn = value;
    }

    public bool TakeSoldierAfterFall
    {
        get
        {
            Reads++;
            return _takeSoldierAfterFall;
        }
        set => _takeSoldierAfterFall = value;
    }

    public bool RiseWithPanel
    {
        get
        {
            Reads++;
            return _riseWithPanel;
        }
        set => _riseWithPanel = value;
    }
}

/// <summary>An <see cref="IOutcomeLog"/> that keeps every line, for the checks the manifest reads.</summary>
internal sealed class RecordingOutcomeLog : IOutcomeLog
{
    internal readonly List<(string Result, string Id, string Key, string Detail)> Lines = new();
    private readonly Dictionary<string, OutcomeCounter> _counters = new();

    // The real log writes a line the first time it sees (id, result, key) in the process and only counts after that.
    private readonly HashSet<(string, string, string)> _seen = new();
    private readonly List<(string Result, string Id)> _calls = new();

    public void Pass(string id, string key, string detail) => Write("pass", id, key, detail);

    public void Fail(string id, string key, string detail) => Write("fail", id, key, detail);

    public void Skip(string id, string key, string detail) => Write("skip", id, key, detail);

    private void Write(string result, string id, string key, string detail)
    {
        _calls.Add((result, id));
        if (_seen.Add((id, result, key)))
            Lines.Add((result, id, key, detail));
    }

    internal int Calls(string result, string id) => _calls.Count(c => c.Result == result && c.Id == id);

    public OutcomeCounter Counter(string id)
    {
        if (!_counters.TryGetValue(id, out var counter))
            _counters[id] = counter = new OutcomeCounter(id);
        return counter;
    }

    public void Swallowed(string feature, string site, Exception ex) => Lines.Add(("swallowed", feature + "." + site, "", ex.GetType().Name));

    public void WriteSummary(string reason)
    {
    }

    internal int Count(string result, string id)
    {
        var n = 0;
        foreach (var line in Lines)
            if (line.Result == result && line.Id == id)
                n++;
        return n;
    }
}
