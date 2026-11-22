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
    private const string HitSite = "LongDefeatDirector.OnHit";
    private const float KeepReadSeconds = 0.5f;
    private const float StateLogSeconds = 5f;

    private readonly RunPlan _plan;

    // The engine's answer, read once in Start after the boot (#1409): the player fights on the attacker team.
    private bool _playerAttacks;
    private readonly SurvivalRun _run;
    private readonly ILongDefeatBattlefieldAdapter _field;
    private readonly ILongDefeatEffectsAdapter? _effects;
    private readonly ILongDefeatFortAdapter? _fort;
    private readonly LongDefeatSession _session;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly string _mission;
    private readonly LongDefeatRunRecords? _records;
    private readonly DeferredCallbackQueue _deferred;

    private readonly Dictionary<(bool Attackers, FormationSlot Slot), BatchTally> _tallies = new();

    private LdPoint? _keepAt;
    private float _keepRadius;

    private FieldPlaces? _fieldStarts;
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
    private RunPhase _botPhase = RunPhase.Ended;
    private float _botClock;
    private int _musterBatchesSpawned;

    /// <param name="effects">The engine writes and reads beyond the field; null runs the wave with no published factors and no <c>scale</c> check (the director's tests).</param>
    /// <param name="fort">The fortification engine work (the gates; later the posts and the volleys), the same object as <paramref name="field"/> in the game; null buys and checks no fortification effect (the director's tests).</param>
    /// <param name="lord">The engine half of the death rule (the rise, the bot's take-over, the general's seat, the forced fall), the same object as <paramref name="field"/> in the game; null keeps the old respawn and serves none of it (the director's tests only: every run in the game passes one, a co-op host's and a story mission's too).</param>
    public LongDefeatDirector(RunPlan plan, ILongDefeatBattlefieldAdapter field, LongDefeatSession session, IModLogger logger,
        IOutcomeLog outcomes, string missionKey, LongDefeatRunRecords? records = null, ILongDefeatEffectsAdapter? effects = null, ILongDefeatFortAdapter? fort = null, ILongDefeatLordAdapter? lord = null)
    {
        _plan = plan;
        _run = new SurvivalRun(plan);
        _field = field;
        _effects = effects;
        _fort = fort;
        _lord = lord;
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

        if (!_field.Boot(LongDefeatPlanSizes.Of(_plan), out var problem))
        {
            _logger.LogError($"{Tag}[Boot] the field cannot host a run: {problem}");
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.BootFailed(problem));
            return false;
        }

        var field = _plan.Field != null;
        if (field)
        {
            var starts = _field.FieldStarts();
            if (starts?.Defenders == null || starts.Attackers == null)
            {
                var missing = $"the field gave no start place for the {(starts?.Defenders == null ? "defenders" : "attackers")}";
                _logger.LogError($"{Tag}[Boot] the field cannot host a run: {missing}");
                LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.BootFailed(missing));
                return false;
            }

            _fieldStarts = starts;
            _logger.LogInfo($"{Tag}[Boot] field run: defenders start ({F1(starts.Defenders.X)}, {F1(starts.Defenders.Y)}, {F1(starts.Defenders.Z)}), attackers start ({F1(starts.Attackers.X)}, {F1(starts.Attackers.Y)}, {F1(starts.Attackers.Z)})");
        }
        else
        {
            var broken = _field.BreakHiddenGates();
            _field.DeployEngines();
            _logger.LogInfo($"{Tag}[Boot] hidden gates broken {broken}");
            _logger.LogInfo($"{Tag}{_field.DescribeEngines()}");
        }

        var recorded = _plan.Keep;
        _keepAt = recorded is { } r ? new LdPoint(r.X, r.Y, r.Z) : field ? null : _field.KeepFromScene();
        ChooseRisePlace();
        if (!field || _keepAt != null)
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.KeepId, _mission,
                LongDefeatChecks.Keep(_keepAt, recorded != null ? "recorded" : "scene", _keepRadius));
        var refusal = PrepareSally();
        if (refusal.Length > 0)
        {
            _logger.LogError($"{Tag}[Boot] {refusal}");
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.BootFailed(refusal));
            return false;
        }

        _session.Fill(_run);
        _started = true;
        Apply(_run.Begin());
        var read = _field.Read(_keepAt, _keepRadius, _plan.Rules.KeepHeightBand, true);
        _mainBody = read.MainBody;
        AdoptIfNew();
        var facts = _field.BootFacts();
        _playerAttacks = facts.PlayerAttacks;
        CheckFortPoints();
        _logger.LogInfo($"{Tag}[Player] start {_field.DescribePlayer()}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.BootId, _mission, LongDefeatChecks.Boot(facts, field));
        LogArcherPositions();
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

        _tickNo++;
        DrainBodyEvents();

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
        JudgeRise();
        JudgeFieldCare(read.DefenderHealthMean);
        ReadPostsTick();
        WatchRoom(read);
        var fx = _run.Tick(new RunSnapshot(dt, read.AttackersStanding, read.AttackersFleeing, read.DefendersStanding, _atKeep,
            _defendersAtKeep, read.SiegeBattleSize, read.Gates, _field.ReadLord(false), read.DefenderHealthMean));
        Apply(fx);
        if (_run.Result != null)
            return;

        JudgeGeneralDown();
        TickWaitingHeroes(dt);
        TickStanding(dt, fx);
        TickSplit(dt, fx.DefenderBatches.Count > 0);
        TickVolleyWatch(dt);
        TickPosts(dt);
        TickHunters();
        if (_plan.Options.Autoplay)
            PlayShop(dt);
        TickSally(dt);
        LogState(dt, read);
    }

    /// <summary>The engine removed an agent (killed, knocked out, routed or deleted). May run off the main thread.</summary>
    public void OnAgentRemoved(object affected, bool loss)
    {
        if (!_started)
            return;

        var removal = _field.Classify(affected, loss);
        if (removal.Kind == LdRemovalKind.None && !removal.HasKey)
            return;

        _deferred.RunOrDefer(RemovedSite, () =>
        {
            // Every agent the registry held leaves it at its removal, and the run forgets its key: a later hit that names it is dropped and its dose ends.
            if (removal.HasKey)
                _run.Forget(removal.Key);
            switch (removal.Kind)
            {
                case LdRemovalKind.Attacker:
                    if (removal.Role == AttackerRole.Commander && !removal.Loss)
                        _run.CommanderDown(false);
                    else
                        _run.AttackerDown(removal.TroopId, removal.Role, removal.Felled);
                    break;
                case LdRemovalKind.Defender:
                    _run.DefenderDown();
                    break;
                case LdRemovalKind.Player:
                    LordFell(removal.TroopId, removal.Forced);
                    break;
                case LdRemovalKind.Hero:
                    HeroLeft(removal.TroopId, removal.Loss);
                    if (removal.PlayerBody && removal.Loss)
                        LordFell(HeroTroop(removal.TroopId), false);
                    break;
            }
        });
    }

    /// <summary>The troop of a hero the field names by his id (a hero's removal carries the id his row and his spawn use), so a death row names the body as a takeover's reading does. An id no hero of the plan has is returned as it is.</summary>
    private string HeroTroop(string heroId) =>
        _plan.Shop.Heroes.FirstOrDefault(h => h.Id == heroId)?.TroopId ?? _plan.FreeHeroes.FirstOrDefault(h => h.Id == heroId)?.TroopId ?? heroId;

    /// <summary>
    /// A body the player controlled fell: the lord's, a soldier's or a hero's, named by its troop. <paramref name="forced"/> is the field's fact that it is the removal the <c>lord_falls</c> blow produced. Main thread only,
    /// called from the deferred removal, so it only notes the death: the seat and its check come on the next tick.
    /// </summary>
    private void LordFell(string troopId, bool forced)
    {
        _run.PlayerDown(troopId, forced);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.LordFellId, _mission + "-f" + _run.Stats.PlayerDeaths, LongDefeatChecks.LordFell(_run.Stats.PlayerDeaths, _playerAttacks));
        _deathPending = true;
        NoteLordFall(troopId, forced);
        NoteEngineTookSplit("the lord fell");
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
        _poisonHits = false;
        if (_started)
        {
            _run.Abandon();
            Finish();
        }

        _sallyHits = false;
        _sally = null;
        ReleaseLordHolds();
        ReleaseErrandWalk("run-ended");

        _session.Clear();
        _field.Clear();
        _deferred.Clear();
    }

    private void AdoptIfNew()
    {
        if (_mainBody == 0 || _mainBody == _adoptedBody)
            return;

        _adoptedBody = _mainBody;
        var heldHero = _field.AdoptMain(_plan.Options.Autoplay);
        NoteEngineTookSplit("a new body");
        DressNewBody(heldHero);
        _logger.LogInfo($"{Tag}[General] {_field.DescribeFormations()}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.GeneralId, _mission, LongDefeatChecks.General(_field.BootFacts()));
        LogBotTakeoverAfter();
        var via = TakeRiseVia();
        if (!_deathPending)
            return;

        _deathPending = false;
        if (via.Length > 0)
            _logger.LogInfo($"{Tag}[Player] respawned {_field.DescribePlayer()}");
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PlayerDeathId, "d" + _run.Stats.PlayerDeaths,
            via.Length > 0 ? LongDefeatChecks.PlayerDeath(true, via) : LongDefeatChecks.PlayerDeath(true, "takeover", _plan.Options.Autoplay ? "bot" : "person"));
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
        else if (action.Kind == BotActionKind.Upgrade && action.OfferId != null)
            _run.TryUpgrade(action.OfferId);
        else if (action.Kind == BotActionKind.Hero && action.OfferId != null)
            _run.TryBuyHero(action.OfferId);
        else if (action.Kind == BotActionKind.Pick && action.OfferId != null)
            _run.TryPickBoon(action.OfferId);
        else if (action.Kind == BotActionKind.Reroll)
            _run.TryReroll();
        else if (action.Kind == BotActionKind.Horn)
            _run.TryStartWave();
        else if (!PlayPlace(action))
            BuyFortification(action.Kind);
    }

    /// <summary>The bot's fortification buys, and its fire in a wave. The view only shows a row the run would sell, so a refusal here means the run moved on between the view and the call.</summary>
    private void BuyFortification(BotActionKind kind)
    {
        switch (kind)
        {
            case BotActionKind.GateLevel: _run.TryBuyGateLevel(); break;
            case BotActionKind.GateRepair: _run.TryRepairGates(); break;
            case BotActionKind.TowerLevel: _run.TryBuyTowerLevel(); break;
            case BotActionKind.FieldCare: _run.TryFieldCare(); break;
            case BotActionKind.VolleyLevel: _run.TryBuyVolleyLevel(); break;
            case BotActionKind.Volley: ArmVolleyForBot(); break;
        }
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
        if (v.Phase == RunPhase.Wave)
        {
            SampleGateWatch(read.AttackersHigh > 0);
            LogArcherPositionsInWave();
        }
        _logger.LogInfo($"{Tag}[State] wave {v.Wave} {v.Phase}: attackers {read.AttackersStanding} fleeing {read.AttackersFleeing} high {read.AttackersHigh}, "
            + $"defenders {read.DefendersStanding} fleeing {read.DefendersFleeing} high {read.DefendersHigh} pending {v.DefendersPending}, gold {v.Gold}, "
            + $"keep {F2(v.KeepProgress)} at {_atKeep}/{_defendersAtKeep}; frame mean {F2(_frameTotal / _frames * 1000f)} ms max {F2(_frameMax * 1000f)} ms");
        _frameTotal = 0f;
        _frameMax = 0f;
        _frames = 0;
        SampleLateScale();
        LogCommander();
        if (_plan.Field == null)
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
