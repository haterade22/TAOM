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

    public void Say(SeatMessage message) => Messages.Add(message);

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
}

internal sealed class FakeSeatHero : ISeatHeroAdapter
{
    internal int StandFasts;
    internal Exception? StandFastThrows;

    public void StandFast()
    {
        if (StandFastThrows != null)
            throw StandFastThrows;
        StandFasts++;
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
