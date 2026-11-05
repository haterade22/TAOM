using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The field trial's driver (build-spec section 12): it runs the steps of <see cref="SeatTrialSteps"/> one at a time from the screen tick and the mission tick, and writes one <c>[SeatTrial] step=...</c> line per step.</summary>
internal sealed partial class CommandTrialScript
{
    private static readonly Dictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> Handlers = new();

    private readonly ICommandTrialHost _host;
    private readonly IReadOnlyList<SeatTrialStep> _steps;
    private readonly IReadOnlyDictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>> _handlers;
    private readonly IModLogger _logger;
    private readonly Action<string> _finished;
    private readonly Action<string>? _stepBegan;
    private readonly int _refuseIndex;

    private int _index;
    private IEnumerator<Verdict>? _running;
    private bool _awaitsMissionTick;
    // Real seconds, summed from the screen tick's dt, which runs under a pause. Mission time is only ever a measured fact.
    private float _now;
    private float _stepStart;
    private int _passed;
    private int _failed;

    // The hero, sampled in the mission tick, which never overlaps the async agent tick (Mission.cs:3542 waits for it, :3780 starts the next).
    private Vec3f? _heroAt;
    private float _heroTime = float.NaN;

    static CommandTrialScript()
    {
        SeatSteps(Handlers);
        MarshalSteps(Handlers);
    }

    internal CommandTrialScript(ICommandTrialHost host, IReadOnlyList<SeatTrialStep> steps, IModLogger logger, Action<string> finished,
        Action<string>? stepBegan = null, IReadOnlyDictionary<string, Func<CommandTrialScript, IEnumerable<Verdict>>>? handlers = null)
    {
        _handlers = handlers ?? Handlers;
        var unhandled = steps.Where(s => !_handlers.ContainsKey(s.Name)).Select(s => s.Name).ToList();
        if (unhandled.Count > 0)
            throw new ArgumentException("no handler for step: " + string.Join(", ", unhandled), nameof(steps));

        _host = host;
        _steps = steps;
        _logger = logger;
        _finished = finished;
        _stepBegan = stepBegan;
        _refuseIndex = steps.ToList().FindIndex(s => s.Name == RefuseStep);
    }

    /// <summary>The names of the steps this script can run; a test ties it to the table.</summary>
    internal static IEnumerable<string> HandlerNames => Handlers.Keys;

    /// <summary>True once the run has ended, as final or as a script failure.</summary>
    internal bool IsDone { get; private set; }

    /// <summary>Once per real frame, from <c>SeatView.OnMissionScreenTick</c>: the clock, the steps and the deployment.</summary>
    internal void OnScreenTick(float dt)
    {
        if (IsDone)
            return;
        try
        {
            if (FiniteFloatValidator.IsFinite(dt) && dt > 0f)
                _now += dt;
            FinishDeploymentWhenDue();
            if (_running == null)
                Begin();

            Settle(_awaitsMissionTick ? Verdict.InMissionTick : Next());
        }
        catch (Exception ex)
        {
            Abort(ex);
        }
    }

    /// <summary>From <c>SeatView.OnMissionTick</c>: samples the hero's position and the mission time, then resumes a step parked on <see cref="Verdict.InMissionTick"/>.</summary>
    internal void OnMissionTick()
    {
        if (IsDone)
            return;
        try
        {
            _heroAt = _host.HeroPosition();
            _heroTime = _host.MissionTime;
            if (_awaitsMissionTick)
                Settle(Next());
        }
        catch (Exception ex)
        {
            Abort(ex);
        }
    }

    // FinishDeployment removes two mission behaviours, so it runs here, from the screen tick, and never from the mission tick.
    private void FinishDeploymentWhenDue()
    {
        if (!_host.InDeployment || _now < SeatTrialRules.DeploymentFinishSeconds)
            return;
        if (_refuseIndex >= 0 && _index <= _refuseIndex)
            return;

        _host.FinishDeployment();
        _logger.LogInfo("[SeatTrial] deployment finished at real=" + Num(_now));
    }

    private void Begin()
    {
        var step = _steps[_index];
        _stepStart = _now;
        _running = _handlers[step.Name](this).GetEnumerator();
        _stepBegan?.Invoke(step.Name);
    }

    private Verdict Next() => _running!.MoveNext() ? _running.Current : Verdict.Failed("reason=no-verdict");

    private void Settle(Verdict verdict)
    {
        var step = _steps[_index];
        _awaitsMissionTick = verdict.IsMissionTick;
        if (!verdict.Done && _now - _stepStart > step.BudgetSeconds)
            verdict = Verdict.Failed("reason=timeout waiting=" + verdict.Text);
        if (!verdict.Done)
            return;
        // A step that ends, by a verdict or by a failure deep inside a wait, leaves no key held for the next one.
        _host.MarshalReleaseKeys();
        if (!verdict.Pass)
            ReleaseAfterFailure();

        Report(step, verdict);
        _running!.Dispose();
        _running = null;
        _awaitsMissionTick = false;
        _index++;
        if (_index >= _steps.Count)
            Complete();
    }

    private void ReleaseAfterFailure()
    {
        _host.SetInput(NoInput);
        _host.ReleaseTime();
        if (_host.PanelOpen)
            _host.ClosePanel(asPlayer: true);
    }

    private void Report(SeatTrialStep step, Verdict verdict)
    {
        var line = SeatTrialRules.Line(step.Name, verdict.Pass, verdict.Text);
        if (verdict.Pass)
        {
            _passed++;
            _logger.LogInfo(line);
        }
        else
        {
            _failed++;
            _logger.LogWarning(line);
        }
    }

    private void Complete()
    {
        IsDone = true;
        _host.SetInput(null);
        _logger.LogInfo("[SeatTrial] done steps=" + _steps.Count.ToString(CultureInfo.InvariantCulture) + " pass=" + _passed.ToString(CultureInfo.InvariantCulture)
            + " fail=" + _failed.ToString(CultureInfo.InvariantCulture));
        _finished("final");
    }

    private void Abort(Exception ex)
    {
        IsDone = true;
        _host.SetInput(null);
        var name = _index < _steps.Count ? _steps[_index].Name : "none";
        _logger.LogError($"[SeatTrial] the script failed in step {name}: {ex}");
        _finished("script_failed");
    }

    private static string Bit(bool value) => value ? "1" : "0";

    private static string Num(float value) => SeatTrialRules.Num(value);

    /// <summary>What a step yields: waiting, a verdict that ends it, or the marker that runs the code after it in the mission tick. A step that ends without a verdict fails, and the driver never resumes one after a verdict.</summary>
    internal readonly struct Verdict
    {
        private enum Kind { Waiting, MissionTick, Passed, Failed }

        private readonly Kind _kind;

        private Verdict(Kind kind, string text)
        {
            _kind = kind;
            Text = text;
        }

        /// <summary>What a waiting step waits for, or a done step's facts.</summary>
        internal string Text { get; }

        internal bool Done => _kind >= Kind.Passed;

        internal bool Pass => _kind == Kind.Passed;

        internal bool IsMissionTick => _kind == Kind.MissionTick;

        /// <summary>The code after this marker runs inside <c>OnMissionTick</c>, and the screen tick does not resume it.</summary>
        internal static Verdict InMissionTick => new(Kind.MissionTick, "mission-tick");

        internal static Verdict Waiting(string what) => new(Kind.Waiting, what);

        internal static Verdict Passed(string facts) => new(Kind.Passed, facts);

        internal static Verdict Failed(string facts) => new(Kind.Failed, facts);
    }
}
