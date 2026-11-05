using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Client.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Features.MarshalsWord;

/// <summary>
/// Marshal's Word for one mission (build-spec section 2), built once by <see cref="MarshalView"/> and finished by both teardown paths, where
/// the screen tick, the mission tick and the capture run in a try whose throw latches <see cref="MarshalHooks.IsLive"/> off, clears the hooks,
/// writes one fail line and makes a capture answer "not queued" so vanilla runs the order.
/// </summary>
internal sealed class MarshalSession : IMarshalHookSink
{
    private const float FlagRefreshSeconds = 0.25f;

    private readonly IMarshalOrderAdapter _orders;
    private readonly IMarshalOutlines _outlines;
    private readonly IMarshalScreenAdapter _screen;
    private readonly IMarshalSettingsProvider _settings;
    private readonly Func<bool> _mayRun;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly MarshalQueueDriver _queue;
    private readonly MarshalMarchDriver _march;
    private readonly MarshalFireDriver _fire;
    private readonly MarshalMarkWatch _marks;
    private readonly MarshalProofWatch _proofs;

    private bool _answerYes;
    private bool _live;
    private bool _finished;
    private bool _flagsShown;
    private int _flagVersion = -1;
    private float _flagDue;
    private bool _showOutlines;

    private bool _agentCleanupOwed;

    // Raised from an engine callback that may not run on the thread that reads it, so it is one volatile bool.
    private volatile bool _decisionDue = true;

    internal MarshalSession(IMarshalOrderAdapter orders, IMarshalFireAdapter fire, IMarshalOutlines outlines, IMarshalScreenAdapter screen,
        IMarshalSettingsProvider settings, Func<bool> mayRun, IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _orders = orders;
        _outlines = outlines;
        _screen = screen;
        _settings = settings;
        _mayRun = mayRun;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
        _queue = new MarshalQueueDriver(orders, outcomes, missionKey);
        _march = new MarshalMarchDriver(orders, outcomes, missionKey);
        _fire = new MarshalFireDriver(orders, fire, outcomes, logger, _screen.Say, missionKey);
        _marks = new MarshalMarkWatch(orders, outcomes, logger, missionKey);
        _proofs = new MarshalProofWatch(orders, outcomes, logger, missionKey);
    }

    internal bool IsLive => _live;

    internal bool Faulted { get; private set; }

    internal MarshalOrderQueue Queue => _queue.Queue;

    /// <summary>Field trial: the archers' driver, for the verdicts it judged and the companies' holding counts.</summary>
    internal MarshalFireDriver Fire => _fire;

    /// <summary>Field trial: the queue driver's counters of finished steps, of steps that patience ended and of steps that ended by arrival.</summary>
    internal (int Finished, int ByPatience, int ByArrival) StepsFinished => (_queue.Finished, _queue.EndedByPatience, _queue.EndedByArrival);

    /// <summary>Field trial: steps the capture stored.</summary>
    internal int StepsCaptured => _queue.Captured;

    /// <summary>Field trial: how the latest march ended, or "none" when none has.</summary>
    internal string MarchConclusion => _march.Conclusion;

    /// <summary>Field trial: how many marches have ended.</summary>
    internal int MarchesConcluded => _march.Concluded;

    /// <summary>Field trial: array commits the takeover laid out (a click or a drag), counted by the commit and not the preview.</summary>
    internal int ArraysSolved { get; private set; }

    /// <summary>Field trial: the latest mark-effect read of a company, or null.</summary>
    internal MarkSample? MarkReadOf(int company) => _marks.LatestOf(company);

    /// <summary>Field trial: the latest mark-effect verdict of a company, the one its check line carries, or null.</summary>
    internal MarkVerdict? MarkJudgedOf(int company) => _marks.JudgedOf(company);

    /// <summary>Field trial: the latest square line written, or null.</summary>
    internal SquareRead? LatestSquare => _proofs.LatestSquare;

    /// <summary>Field trial: how the spacing proof read the company with one rider, or null while it has not.</summary>
    internal SpacingReading? SpacingVerdict => _proofs.SpacingVerdict;

    /// <summary>Field trial: units that wear an outline this session painted.</summary>
    internal int OutlinesPainted => _outlines.PaintedCount;

    /// <summary>The mission changed mode. The kind of mission depends on the mode, so the answer is asked again until it is yes (section 2).</summary>
    internal void OnModeChange() => _decisionDue = true;

    /// <summary>
    /// Once per real frame, from <c>OnMissionScreenTick</c>, while live. The held keys only count with the order panel open. Writes the statics
    /// the patches read, and draws or hides the flags of the road ahead.
    /// </summary>
    internal void OnScreenTick(MarshalScreenInput input)
    {
        if (!_live)
            return;
        try
        {
            MarshalHooks.Arm(input.QueueKeyDown && input.PanelOpen, input.WidthsKeyDown && input.PanelOpen, _settings.KeepArray, _settings.ArriveTogether,
                input.MarkKeyDown && input.PanelOpen, _settings.HollowSquare);
            _showOutlines = _settings.Outlines && (input.PanelOpen || input.IndicatorsDown);
            if (input.FirePressed)
                _fire.PressFire();
            if (input.WordPressed)
                _fire.PressWord();
            if (input.HoldPressed)
                _fire.PressHold();
            RefreshFlags(input);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>From <c>OnMissionTick</c>: pays what a fault owes, goes live when the answer is yes, then runs the queue, march, archers', mark and outline passes (section 2).</summary>
    internal void OnMissionTick()
    {
        if (_agentCleanupOwed)
            PayAgentCleanup();
        if (_finished || Faulted)
            return;
        try
        {
            if (!_live)
                TryBegin();
            if (!_live)
                return;
            _queue.Pass();
            _march.Pass(MarshalHooks.ArriveTogether);
            _fire.Pass();
            _marks.Pass();
            _proofs.Pass();
            _outlines.Pass(_orders.Now, _showOutlines);
        }
        catch (Exception ex)
        {
            Fault(ex);
            PayAgentCleanup();
        }
    }

    /// <summary>
    /// Hands the capture an order of the player's own controller. True means it was stored (or swallowed because the queue is full) and
    /// vanilla must not run it. Any fault answers false, so the order runs.
    /// </summary>
    public bool TryCapture(MarshalOrderPayload payload)
    {
        if (!_live)
            return false;
        try
        {
            if (MarshalHooks.MarkArmed && payload.CanMark)
                return CaptureMark(payload);
            if (!MarshalHooks.QueueArmed && MarshalHooks.MarkArmed)
                return false;
            var result = _queue.Capture(payload.End, payload, payload.Describe());
            if (result == CaptureResult.Full)
                _screen.Say(MarshalMessage.QueueFull);
            return result != CaptureResult.Declined;
        }
        catch (Exception ex)
        {
            Fault(ex);
            return false;
        }
    }

    private bool CaptureMark(MarshalOrderPayload payload)
    {
        var mark = MarshalOrderPayload.ForMark(payload.Target!);
        var result = MarshalHooks.QueueArmed ? _queue.Capture(StepEnd.AtOnce, mark, mark.Describe()) : _queue.CaptureMark(mark);
        if (result == CaptureResult.Declined && MarshalHooks.QueueArmed)
            result = _queue.CaptureMark(mark);
        if (result == CaptureResult.Full)
            _screen.Say(MarshalMessage.QueueFull);
        return result != CaptureResult.Declined;
    }

    public void PanelPress(PanelAction action, IReadOnlyList<int> companies)
    {
        if (!_live)
            return;
        try
        {
            switch (action)
            {
                case PanelAction.Fire:
                    _fire.PressFire(companies);
                    break;
                case PanelAction.Word:
                    _fire.PressWord(companies);
                    break;
                default:
                    _fire.PressHold(companies);
                    break;
            }
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    public void ArraySolved(bool click, int companies)
    {
        if (!_live)
            return;
        ArraysSolved++;
        var kind = click ? "click" : "drag";
        _outcomes.Pass(MarshalChecks.ArrayId, _key + ":" + kind,
            "mission=" + _key + " kind=" + kind + " companies=" + companies.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public void ArrayFault(Exception exception)
    {
        _outcomes.Fail(MarshalChecks.ArrayId, _key + ":fault", "mission=" + _key + " reason=fault error=" + exception.GetType().Name);
        _logger.LogError($"[MarshalsWord] the array layout failed and is off for this mission, vanilla lays out orders: {exception}");
    }

    public void MarchRequested(IReadOnlyList<MarchTarget> targets)
    {
        if (_live)
            _march.Request(targets);
    }

    /// <summary>Both teardown paths call this: it unsubscribes, clears the statics and drops the flags, and makes no native call; whatever a fault still owed is forgotten with the mission.</summary>
    internal void Finish()
    {
        if (_finished)
            return;
        _finished = true;
        _live = false;
        _agentCleanupOwed = false;
        Teardown();
    }

    // Asked at the first tick and again after each mode change until the answer is yes. A mission can make its player team after the first
    // tick with no mode change, so while the answer is yes the player team is tested on every tick.
    private void TryBegin()
    {
        if (_decisionDue)
        {
            _decisionDue = false;
            var enabled = _settings.Enabled;
            _answerYes = enabled && _mayRun();
            if (!_answerYes)
                SkipWhyNotLive(enabled);
        }

        if (_answerYes && _screen.PlayerTeamExists)
            Begin();
    }

    private void SkipWhyNotLive(bool enabled)
    {
        var why = enabled ? "mission-refused" : "setting-off";
        _outcomes.Skip(MarshalChecks.CapturedId, _key + ":" + why, "mission=" + _key + " reason=" + why);
    }

    private void Begin()
    {
        _orders.DirectOrder += _queue.OnDirectOrder;
        _orders.PlayerTeamChanged += OnPlayerTeamChanged;
        _orders.Attach();
        MarshalHooks.Begin(this);
        MarshalHooks.Arm(queue: false, keepWidths: false, _settings.KeepArray, _settings.ArriveTogether, hollowSquare: _settings.HollowSquare);
        _live = true;
    }

    private void OnPlayerTeamChanged()
    {
        _queue.OnPlayerTeamChanged();
        _march.OnPlayerTeamChanged();
        _fire.OnPlayerTeamChanged();
        _marks.OnPlayerTeamChanged();
        _proofs.OnPlayerTeamChanged();
    }

    private void RefreshFlags(MarshalScreenInput input)
    {
        if (!input.PanelOpen)
        {
            if (_flagsShown)
                _screen.HideFlags();
            _flagsShown = false;
            return;
        }

        var version = _queue.Queue.Version;
        if (version == _flagVersion && (input.RealTime < _flagDue || _queue.Queue.IsEmpty))
            return;
        _flagVersion = version;
        _flagDue = input.RealTime + FlagRefreshSeconds;

        var points = new List<MarshalFlagPoint>();
        var seen = new HashSet<long>();
        foreach (var company in _orders.SelectedCompanies())
        {
            foreach (var step in _queue.Queue.StepsOf(company))
            {
                if (step.Payload != null && seen.Add(step.Id) && _orders.TryPoint(step.Payload, out var x, out var y, out var z))
                    points.Add(new MarshalFlagPoint(x, y, z));
            }
        }

        _screen.ShowFlags(points);
        _flagsShown = true;
    }

    private void Fault(Exception ex)
    {
        if (Faulted)
            return;
        Faulted = true;
        _live = false;
        Swallow(_screen.HideFlags);
        _agentCleanupOwed = true;
        Swallow(Teardown);
        Swallow(() => _logger.LogError($"[MarshalsWord] Marshal's Word failed and is off for this mission, orders run as in the base game: {ex}"));
        Swallow(() => _outcomes.Fail(MarshalChecks.FaultId, _key, "mission=" + _key + " error=" + ex.GetType().Name));
        Swallow(() => _screen.Say(MarshalMessage.Faulted));
    }

    private void Teardown()
    {
        var owner = MarshalHooks.IsOwnedBy(this);
        Swallow(() =>
        {
            _orders.DirectOrder -= _queue.OnDirectOrder;
            _orders.PlayerTeamChanged -= OnPlayerTeamChanged;
            _orders.Detach();
        });
        if (owner)
        {
            Swallow(_fire.Summarize);
            MarshalHooks.Clear();
        }

        if (!_agentCleanupOwed)
        {
            Swallow(_fire.Drop);
            Swallow(_outlines.Drop);
        }

        Swallow(_screen.DropFlags);
    }

    /// <summary>What a fault owes the soldiers: every contour cleared and every callback flag turned off. The finish makes no native call, because the scene may be gone by then.</summary>
    private void PayAgentCleanup()
    {
        _agentCleanupOwed = false;
        Swallow(_outlines.ClearAll);
        Swallow(_outlines.Drop);
        Swallow(_fire.ReleaseAll);
        Swallow(_fire.Drop);
    }

    private void Swallow(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed("MarshalsWord", "Teardown", ex);
        }
    }
}
