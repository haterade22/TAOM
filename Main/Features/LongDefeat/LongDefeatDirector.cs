using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using BehaviorTreeWrapper;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// One Long Defeat run inside one mission: turns the <see cref="SurvivalRun"/>'s effects into adapter calls and writes the
/// <c>[Check]</c> lines. An agent removal can arrive off the main thread, so it goes through <see cref="DeferredCallbackQueue.RunOrDefer"/>.
/// </summary>
internal sealed partial class LongDefeatDirector
{
    private const string Tag = "[LongDefeat]";
    private const string RemovedSite = "LongDefeatDirector.OnAgentRemoved";
    private const float KeepReadSeconds = 0.5f;
    private const float StateLogSeconds = 5f;

    private readonly RunPlan _plan;
    private readonly SurvivalRun _run;
    private readonly ILongDefeatBattlefieldAdapter _field;
    private readonly ILongDefeatEffectsAdapter? _effects;
    private readonly LongDefeatSession _session;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _mission;
    private readonly LongDefeatRunRecords? _records;
    private readonly DeferredCallbackQueue _deferred;

    private readonly Dictionary<(bool Attackers, FormationSlot Slot), BatchTally> _tallies = new();

    private LdPoint? _keepAt;
    private float _keepRadius;
    private int _chargedWave;
    private bool _started;
    private DateTime _startedUtc;
    private bool _shutDown;
    private bool _finished;
    private float _keepClock;
    private float _stateClock;
    private float _frameTotal;
    private float _frameMax;
    private int _frames;
    private int _atKeep;
    private int _defendersAtKeep;
    private int _mainBody;
    private int _adoptedBody;
    private bool _deathPending;
    private bool _respawned;
    private RunPhase _botPhase = RunPhase.Ended;
    private float _botClock;
    private int _musterBatchesSpawned;

    /// <param name="effects">The engine writes and reads beyond the field; null runs the wave with no published factors and no <c>scale</c> check (the director's tests).</param>
    public LongDefeatDirector(RunPlan plan, ILongDefeatBattlefieldAdapter field, LongDefeatSession session, IModLogger logger,
        IOutcomeLog outcomes, string missionKey, LongDefeatRunRecords? records = null, ILongDefeatEffectsAdapter? effects = null)
    {
        _plan = plan;
        _run = new SurvivalRun(plan);
        _field = field;
        _effects = effects;
        _session = session;
        _logger = logger;
        _outcomes = outcomes;
        _mission = missionKey;
        _records = records;
        _deferred = new DeferredCallbackQueue(message => _logger.LogWarning(message));
        _keepRadius = plan.Keep?.Radius ?? plan.Rules.KeepRadius;
    }

    public RunResult? Result => _run.Result;

    /// <summary>The JSON report, built when the run ends; empty before that and when the run never started.</summary>
    public string ReportText { get; private set; } = "";

    /// <summary>
    /// Boots the field, opens the garrison and the player, and fills the session. False, with the reason logged and nothing
    /// spawned, when the scene cannot host a run. Main thread, once, from the first mission tick that is ready.
    /// </summary>
    public bool Start()
    {
        MissionThreadGuard.MarkMainThread();
        if (_started || _shutDown)
            return false;

        _startedUtc = DateTime.UtcNow;

        if (!_field.Boot(PlanSizes(), out var problem))
        {
            _logger.LogError($"{Tag}[Boot] the field cannot host a run: {problem}");
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.BootFailed(problem));
            return false;
        }

        var broken = _field.BreakHiddenGates();
        _field.DeployEngines();
        _logger.LogInfo($"{Tag}[Boot] hidden gates broken {broken}");
        _logger.LogInfo($"{Tag}{_field.DescribeEngines()}");

        var recorded = _plan.Keep;
        _keepAt = recorded is { } r ? new LdPoint(r.X, r.Y, r.Z) : _field.KeepFromScene();
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.KeepId, _mission,
            LongDefeatChecks.Keep(_keepAt, recorded != null ? "recorded" : "scene", _keepRadius));
        _session.Fill(_run);
        _started = true;
        Apply(_run.Begin());
        var read = _field.Read(_keepAt, _keepRadius, _plan.Rules.KeepHeightBand, true);
        _mainBody = read.MainBody;
        AdoptIfNew();
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.Boot(_field.BootFacts()));
        _logger.LogInfo($"{Tag}[Boot] run started: fortress {_plan.FortressId}, keep {KeepText()} ({(recorded != null ? "recorded" : "scene")}), autoplay {_plan.Options.Autoplay}");
        return true;
    }

    /// <summary>One mission tick. Main thread. Does nothing before <see cref="Start"/>, after the run ended, or for a bad <paramref name="dt"/>.</summary>
    public void Tick(float dt)
    {
        MissionThreadGuard.MarkMainThread();
        _deferred.Drain();
        if (!_started || _shutDown || _run.Result != null || !(dt > 0f))
            return;

        _session.AddLiveSeconds(dt);
        _keepClock -= dt;
        var countKeep = _keepClock <= 0f;
        if (countKeep)
            _keepClock = KeepReadSeconds;

        var read = _field.Read(_keepAt, _keepRadius, _plan.Rules.KeepHeightBand, countKeep);
        if (countKeep)
        {
            _atKeep = read.AttackersAtKeep;
            _defendersAtKeep = read.DefendersAtKeep;
        }

        _mainBody = read.MainBody;
        AdoptIfNew();
        WatchRoom(read);
        var fx = _run.Tick(new RunSnapshot(dt, read.AttackersStanding, read.AttackersFleeing, read.DefendersStanding, _atKeep,
            _defendersAtKeep, read.SiegeBattleSize));
        Apply(fx);
        if (_run.Result != null)
            return;

        if (_plan.Options.Autoplay)
            PlayShop(dt);
        LogState(dt, read);
    }

    /// <summary>The engine removed an agent (killed, knocked out, routed or deleted). May run off the main thread.</summary>
    public void OnAgentRemoved(object affected, bool loss)
    {
        if (!_started)
            return;

        var removal = _field.Classify(affected, loss);
        if (removal.Kind == LdRemovalKind.None)
            return;

        _deferred.RunOrDefer(RemovedSite, () =>
        {
            switch (removal.Kind)
            {
                case LdRemovalKind.Attacker:
                    _run.AttackerDown(removal.TroopId);
                    break;
                case LdRemovalKind.Defender:
                    _run.DefenderDown();
                    break;
                case LdRemovalKind.Player:
                    _run.PlayerDown();
                    _deathPending = true;
                    break;
            }
        });
    }

    /// <summary>An agent started running away. Main thread only, as the engine raises it.</summary>
    public void OnAgentFleeing(object agent)
    {
        if (_started && _field.IsAttacker(agent))
            _run.AttackerFled();
    }

    /// <summary>
    /// Ends the director's work on every exit: lands what was parked, abandons a run still going, writes the end checks and the
    /// report, and clears the session and the adapter. Safe to call twice.
    /// </summary>
    public void Shutdown()
    {
        if (_shutDown)
            return;

        _deferred.Drain();
        _shutDown = true;
        if (_started)
        {
            _run.Abandon();
            Finish();
        }

        _session.Clear();
        _field.Clear();
        _deferred.Clear();
    }

    private LdPlanSizes PlanSizes()
    {
        var foot = _plan.Garrison.Where(r => r.Slot == FormationSlot.Infantry).Sum(r => _plan.Options.ScaledCount(r.Count)) + 1;
        var ranged = _plan.Garrison.Where(r => r.Slot == FormationSlot.Ranged).Sum(r => _plan.Options.ScaledCount(r.Count));
        var attackerRanged = _plan.Rules.LiveCap / 4;
        return new LdPlanSizes(foot, ranged, _plan.Rules.LiveCap - attackerRanged, attackerRanged);
    }

    private void AdoptIfNew()
    {
        if (_mainBody == 0 || _mainBody == _adoptedBody)
            return;

        _adoptedBody = _mainBody;
        _field.AdoptMain(_plan.Options.Autoplay);
        _logger.LogInfo($"{Tag}[General] {_field.DescribeFormations()}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GeneralId, _mission, LongDefeatChecks.General(_field.BootFacts()));
        if (!_deathPending)
            return;

        _deathPending = false;
        if (_respawned)
            _logger.LogInfo($"{Tag}[Player] respawned {_field.DescribePlayer()}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PlayerDeathId, "d" + _run.Stats.PlayerDeaths,
            LongDefeatChecks.PlayerDeath(true, _respawned ? "respawn" : "takeover"));
        _respawned = false;
    }

    private void PlayShop(float dt)
    {
        var view = _run.GetView();
        if (view.Phase != _botPhase)
        {
            _botPhase = view.Phase;
            _botClock = 0f;
        }

        _botClock += dt;
        var action = ShopBot.Decide(view, _botClock);
        if (action.Kind == BotActionKind.Buy && action.OfferId != null)
            _run.TryBuy(action.OfferId);
        else if (action.Kind == BotActionKind.Horn)
            _run.TryStartWave();
    }

    private void LogState(float dt, LdReading read)
    {
        _stateClock += dt;
        _frameTotal += dt;
        _frameMax = Math.Max(_frameMax, dt);
        _frames++;

        if (_stateClock < StateLogSeconds)
            return;

        _stateClock = 0f;
        var v = _run.GetView();
        _logger.LogInfo($"{Tag}[State] wave {v.Wave} {v.Phase}: attackers {read.AttackersStanding} fleeing {read.AttackersFleeing} high {read.AttackersHigh}, "
            + $"defenders {read.DefendersStanding} fleeing {read.DefendersFleeing} high {read.DefendersHigh} pending {v.DefendersPending}, gold {v.Gold}, "
            + $"keep {F2(v.KeepProgress)} at {_atKeep}/{_defendersAtKeep}; frame mean {F2(_frameTotal / _frames * 1000f)} ms max {F2(_frameMax * 1000f)} ms");
        _frameTotal = 0f;
        _frameMax = 0f;
        _frames = 0;
        SampleLateScale();
        _logger.LogInfo($"{Tag}{_field.DescribeEngines()}");
        _logger.LogInfo($"{Tag}[Keep] attackers={_atKeep} defenders={_defendersAtKeep} progress={F2(v.KeepProgress)} at={KeepText()} positions={SampleText()}");
    }

    private string KeepText() => _keepAt is { } k ? $"({F1(k.X)}, {F1(k.Y)}, {F1(k.Z)}) radius {F1(_keepRadius)}" : "none";

    private string SampleText()
    {
        var sample = _field.AttackersNearest(_keepAt);
        return sample.Count == 0 ? "none" : string.Join(";", sample.Select(p => $"({F1(p.X)},{F1(p.Y)},{F1(p.Z)})"));
    }

    /// <summary>What one engine batch of a formation spawned so far: summed over every tick the batch spans, then judged when it closes.</summary>
    private sealed class BatchTally
    {
        public int Requested;
        public int Spawned;
        public int Far;
        public int OffMesh;
        public int OnPoint;
    }
}
