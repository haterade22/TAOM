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
    private readonly IMarshalScreenAdapter _screen;
    private readonly IMarshalSettingsProvider _settings;
    private readonly Func<bool> _mayRun;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly MarshalQueueDriver _queue;
    private readonly MarshalMarchDriver _march;

    private bool _answerYes;
    private bool _live;
    private bool _finished;
    private bool _flagsShown;
    private int _flagVersion = -1;
    private float _flagDue;

    // Raised from an engine callback that may not run on the thread that reads it, so it is one volatile bool.
    private volatile bool _decisionDue = true;

    internal MarshalSession(IMarshalOrderAdapter orders, IMarshalScreenAdapter screen, IMarshalSettingsProvider settings, Func<bool> mayRun,
        IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _orders = orders;
        _screen = screen;
        _settings = settings;
        _mayRun = mayRun;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
        _queue = new MarshalQueueDriver(orders, outcomes, missionKey);
        _march = new MarshalMarchDriver(orders, outcomes, missionKey);
    }

    internal bool IsLive => _live;

    internal bool Faulted { get; private set; }

    internal MarshalOrderQueue Queue => _queue.Queue;

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
            MarshalHooks.Arm(input.QueueKeyDown && input.PanelOpen, input.WidthsKeyDown && input.PanelOpen, _settings.KeepArray, _settings.ArriveTogether);
            RefreshFlags(input);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>From <c>OnMissionTick</c>: goes live when the answer is yes, then runs the queue pass and the march pass (section 2).</summary>
    internal void OnMissionTick()
    {
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
        }
        catch (Exception ex)
        {
            Fault(ex);
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

    public void ArraySolved(bool click, int companies)
    {
        if (!_live)
            return;
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

    /// <summary>Both teardown paths call this: it unsubscribes, clears the statics and drops the flags, and makes no native call.</summary>
    internal void Finish()
    {
        if (_finished)
            return;
        _finished = true;
        _live = false;
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
        MarshalHooks.Arm(queue: false, keepWidths: false, _settings.KeepArray, _settings.ArriveTogether);
        _live = true;
    }

    private void OnPlayerTeamChanged()
    {
        _queue.OnPlayerTeamChanged();
        _march.OnPlayerTeamChanged();
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
        // A fault comes with the mission still running, so the flags shown at that moment would stand in the scene until it ends. The finish
        // makes no native call (the scene may be gone by then); a fault is the one teardown that can still reach it.
        Swallow(_screen.HideFlags);
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
            MarshalHooks.Clear();

        Swallow(_screen.DropFlags);
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
