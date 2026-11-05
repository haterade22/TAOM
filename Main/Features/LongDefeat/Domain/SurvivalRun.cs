using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One Long Defeat run: preparation, then waves with an intermission between, until the defenders die, the keep falls, a fight
/// stalls or the target is reached. Plain state, touched only on the main thread; the director turns each call's
/// <see cref="RunEffects"/> into engine work. Every random draw comes from <see cref="RunRandom"/> over the plan's seed (none yet).
/// </summary>
internal sealed partial class SurvivalRun
{
    private const int LiveFloor = 101;

    private readonly RunPlan _plan;
    private readonly RunRules _r;
    private readonly Purse _purse;
    private readonly RunStats _stats = new();
    private readonly List<RunEvent> _pendingEvents = new();
    private readonly List<ShopOffer> _packs = new();
    private readonly bool[] _defenderPlaced = new bool[2];
    private readonly int[] _bought = new int[2];

    private bool _begun;
    private RunPhase _phase = RunPhase.Preparation;
    private float _left;
    private int _wave;
    private int _wavesCleared;
    private int _lastDefenders;
    private int _lastAttackers;

    private IReadOnlyList<WaveSoldier> _musterFoot = Array.Empty<WaveSoldier>();
    private IReadOnlyList<MusterBatch> _musterBatches = Array.Empty<MusterBatch>();
    private int _musterNext;
    private int _musterEmitted;
    private float _musterClock;

    private bool _waveInit;
    private WaveStage _stage;
    private List<WaveSoldier> _queue = new();
    private int _queueHead;
    private int _waveCount;
    private int _firstBatch;
    private int _firstSpawned;
    private readonly int[] _firstTotals = new int[2];
    private readonly int[] _firstIndex = new int[2];
    private readonly bool[] _firstPlaced = new bool[2];
    private float _waveSeconds;
    private int _waveFelled;
    private float _healthFactor = 1f;
    private float _damageFactor = 1f;
    private StalemateState _stale;
    private float _keep;
    private int _fellPending;
    private bool _lossThisTick;

    public SurvivalRun(RunPlan plan)
    {
        _plan = plan;
        _r = plan.Rules;
        _purse = new Purse(plan.Shop.StartingGold);
        _left = _r.PreparationSeconds;
        _wave = Math.Max(1, plan.Options.StartWave) - 1;
    }

    public RunPhase Phase => _phase;

    /// <summary>0 in Preparation of a board run; one less than <see cref="RunOptions.StartWave"/> otherwise.</summary>
    public int Wave => _wave;

    public int WavesCleared => _wavesCleared;
    public int Gold => _purse.Balance;
    public RunResult? Result { get; private set; }
    public Purse Purse => _purse;
    public RunStats Stats => _stats;

    private int PendingDefenders => _musterFoot.Count - _musterEmitted + _packs.Sum(p => p.PackSize);

    private int QueueLeft => _queue.Count - _queueHead;

    private bool BetweenWaves => _phase is RunPhase.Preparation or RunPhase.Intermission;

    /// <summary>How many foot batches the garrison musters in, the first included; 0 before <see cref="Begin"/>.</summary>
    public int MusterBatchCount => _musterBatches.Count;

    /// <summary>Once: the archers, the first foot batch, the player and the end of the initial spawn.</summary>
    public RunEffects Begin()
    {
        var fx = new RunEffects();
        if (_begun || Result != null)
            return fx;

        _begun = true;
        var ranged = new List<WaveSoldier>();
        var foot = new List<WaveSoldier>();
        foreach (var row in _plan.Garrison)
        {
            var soldiers = Enumerable.Repeat(new WaveSoldier(row.TroopId, row.Slot), _plan.Options.ScaledCount(row.Count));
            (row.Slot == FormationSlot.Ranged ? ranged : foot).AddRange(soldiers);
        }

        _musterFoot = foot;
        _musterBatches = MusterPlan.Split(foot.Count, _r.MusterBatch, _r.MusterBatchSeconds);
        var opening = ranged.Concat(TakeMusterBatch(0)).ToList();
        _musterNext = 1;
        fx.DefenderBatches.AddRange(Batches(opening, new int[2], CountBySlot(opening), false, _defenderPlaced));
        fx.SpawnPlayer = true;
        fx.FinishInitialSpawn = true;
        return fx;
    }

    /// <summary>Advances the run by one engine tick. A bad <c>Dt</c> (zero, negative, NaN, infinite) changes nothing.</summary>
    public RunEffects Tick(RunSnapshot s)
    {
        var fx = new RunEffects();
        if (!_begun || Result != null || !(s.Dt > 0f) || float.IsInfinity(s.Dt))
            return fx;

        _lastDefenders = s.DefendersStanding;
        _lastAttackers = s.AttackersStanding;
        _stats.ObserveFleeing(s.AttackersFleeing);
        fx.Events.AddRange(_pendingEvents);
        _pendingEvents.Clear();
        var loss = _lossThisTick;
        _lossThisTick = false;

        if (s.DefendersStanding <= 0 && PendingDefenders == 0)
        {
            End(RunEndReason.DefendersDead);
            return fx;
        }

        EmitMuster(s.Dt, fx);
        EmitPacks(fx);

        if (BetweenWaves)
        {
            _left -= s.Dt;
            if (!(_left > 0f))
                BeginWave();
        }

        if (_phase == RunPhase.Wave)
            RunWave(s, loss, fx);
        return fx;
    }

    public void AttackerDown(string troopId)
    {
        if (Result != null)
            return;

        if (_fellPending > 0)
        {
            _fellPending--;
            return;
        }

        _stats.AttackerKilled();
        _lossThisTick = true;
        if (_plan.Bounties.TryGetValue(troopId, out var gold))
            _purse.Earn(PurseSource.Bounty, gold);
    }

    public void AttackerFled()
    {
        if (Result == null)
            _stats.AttackerFled();
    }

    public void DefenderDown()
    {
        if (Result != null)
            return;

        _stats.DefenderLost();
        _lossThisTick = true;
    }

    public void PlayerDown()
    {
        if (Result == null)
            _stats.PlayerDied();
    }

    public void Abandon()
    {
        if (Result == null)
            End(RunEndReason.Abandoned);
    }

    public RunView GetView()
    {
        // The next wave's preview is shown only between waves, so a wave in progress (most of a run) builds none of it.
        var next = BetweenWaves ? NextSpec : null;
        return new(
            _phase,
            _wave,
            _wavesCleared,
            _purse.Balance,
            BetweenWaves ? Math.Max(0f, _left) : 0f,
            _lastDefenders,
            PendingDefenders,
            _lastAttackers,
            _keep,
            _plan.Shop.Offers,
            _bought[(int)FormationSlot.Infantry],
            _bought[(int)FormationSlot.Ranged])
        {
            WaveAffixes = WaveLabels,
            NextAffixes = next != null ? _plan.Affixes.Labels(NextAffixes) : Array.Empty<AffixLabel>(),
            NextHealthFactor = next?.HealthFactor ?? 1f,
            NextDamageFactor = next?.DamageFactor ?? 1f,
        };
    }

    private void RunWave(RunSnapshot s, bool loss, RunEffects fx)
    {
        if (_waveInit)
            InitWave(s, fx);

        _waveSeconds += s.Dt;
        _keep = KeepHoldRule.Step(_keep, Math.Min(s.AttackersAtKeep, s.AttackersStanding), s.DefendersAtKeep, s.Dt, _r);
        if (_keep >= 1f)
        {
            End(RunEndReason.KeepLost);
            return;
        }

        var spawned = SpawnAttackers(s, fx);
        if (_stage != WaveStage.Fighting)
            return;

        if (spawned == 0 && QueueLeft == 0 && s.AttackersStanding <= 0)
        {
            ClearWave(fx);
            return;
        }

        var decision = StalemateRule.Step(ref _stale, s.Dt, loss, s.AttackersStanding + QueueLeft, s.DefendersStanding, _waveCount, _r);
        switch (decision.Action)
        {
            case StalemateAction.Charge:
                fx.OrderCharge = true;
                fx.Events.Add(new RunEvent(RunEventKind.Charge, _wave, string.Empty));
                break;
            case StalemateAction.FellStragglers:
                var felled = Math.Min(decision.Count, s.AttackersStanding);
                if (felled > 0)
                {
                    fx.FellAttackers = felled;
                    _fellPending += felled;
                    _waveFelled += felled;
                    fx.Events.Add(new RunEvent(RunEventKind.Felled, _wave, "count=" + felled));
                }

                break;
            case StalemateAction.NoResult:
                // The last few defenders nobody reaches have lost the run, as the attackers' last few lose their wave (Mike, 2026-11-04).
                var lastFew = s.DefendersStanding <= _r.StragglerCount && s.DefendersStanding < s.AttackersStanding + QueueLeft;
                End(lastFew ? RunEndReason.DefendersDead : RunEndReason.Stalemate);
                break;
        }
    }

    private void BeginWave()
    {
        _wave++;
        _phase = RunPhase.Wave;
        _left = 0f;
        _waveInit = true;
        _waveAffixes = _plan.Affixes.Roll(Seed, _wave);
    }

    private void InitWave(RunSnapshot s, RunEffects fx)
    {
        _waveInit = false;
        var spec = _plan.Waves.ForWave(_wave);
        _queue = WavePlan.Expand(spec.Count, spec.Mix).ToList();
        _healthFactor = spec.HealthFactor;
        _damageFactor = spec.DamageFactor;
        _queueHead = 0;
        _waveCount = _queue.Count;
        _firstBatch = Math.Min(_queue.Count, LiveCap(s));
        var first = _queue.Take(_firstBatch).ToList();
        CountBySlot(first).CopyTo(_firstTotals, 0);
        Array.Clear(_firstIndex, 0, 2);
        Array.Clear(_firstPlaced, 0, 2);
        _firstSpawned = 0;
        _stage = _firstBatch > 0 ? WaveStage.Spawning : WaveStage.Fighting;
        _waveSeconds = 0f;
        _waveFelled = 0;
        _stale = default;
        fx.StartWave = new WaveStart(_wave, _firstTotals[(int)FormationSlot.Infantry], _firstTotals[(int)FormationSlot.Ranged]);
        fx.PublishStats = RunModifiers.StatTable(_plan.Affixes, _waveAffixes, _damageFactor);
    }

    /// <summary>The attackers the field may hold. The floor keeps siege engines in use: at 100 or fewer the breach tactic stops every machine.</summary>
    private int LiveCap(RunSnapshot s) => LiveRoom(_r, s.SiegeBattleSize, s.DefendersStanding);

    /// <summary>The attackers the field may hold at once for a battle size and a defender count; the director's scale check reads the same rule.</summary>
    internal static int LiveRoom(RunRules rules, int siegeBattleSize, int defendersStanding) =>
        Math.Max(LiveFloor, Math.Min(rules.LiveCap, siegeBattleSize - defendersStanding));

    private int SpawnAttackers(RunSnapshot s, RunEffects fx)
    {
        var n = Math.Min(_r.SpawnsPerTick, Math.Min(LiveCap(s) - s.AttackersStanding, QueueLeft));
        var opening = _stage == WaveStage.Spawning;
        if (opening)
            n = Math.Min(n, _firstBatch - _firstSpawned);
        if (n <= 0)
            return 0;

        var take = _queue.GetRange(_queueHead, n);
        _queueHead += n;
        if (!opening)
        {
            fx.AttackerBatches.AddRange(Batches(take, new int[2], CountBySlot(take), true, new[] { true, true }, _healthFactor));
            return n;
        }

        fx.AttackerBatches.AddRange(Batches(take, _firstIndex, _firstTotals, true, _firstPlaced, _healthFactor));
        _firstSpawned += n;
        if (_firstSpawned >= _firstBatch)
        {
            fx.RefreshAttackerTactic = true;
            _stage = WaveStage.Fighting;
        }

        return n;
    }

    private void ClearWave(RunEffects fx)
    {
        var bonus = _r.WaveBonusBase + _r.WaveBonusPerWave * _wave;
        _purse.Earn(PurseSource.WaveBonus, bonus);
        _stats.CloseWave(_wave, _waveSeconds, _waveFelled, _healthFactor, _damageFactor, _waveAffixes);
        _wavesCleared++;
        fx.Events.Add(new RunEvent(RunEventKind.WaveCleared, _wave, "bonus=" + bonus));
        if (_stale.Charging)
            fx.RestoreDefenderTactic = true;

        _keep = 0f;
        _fellPending = 0;
        _stale = default;
        if (_plan.Options.StopAfterWaves is int target && _wavesCleared >= target)
        {
            End(RunEndReason.TargetReached);
            return;
        }

        fx.RestShare = _r.RestShare;
        _phase = RunPhase.Intermission;
        _left = _r.IntermissionSeconds;
    }

    private void End(RunEndReason reason)
    {
        Result = new RunResult(reason, _wave, _wavesCleared, _stats.TotalKills);
        _phase = RunPhase.Ended;
    }

    private void EmitMuster(float dt, RunEffects fx)
    {
        _musterClock += dt;
        if (_musterNext >= _musterBatches.Count || _musterClock < _musterBatches[_musterNext].DelaySeconds)
            return;

        var due = new List<WaveSoldier>();
        while (_musterNext < _musterBatches.Count && _musterClock >= _musterBatches[_musterNext].DelaySeconds)
            due.AddRange(TakeMusterBatch(_musterNext++));
        fx.DefenderBatches.AddRange(Batches(due, new int[2], CountBySlot(due), false, _defenderPlaced));
    }

    private void EmitPacks(RunEffects fx)
    {
        foreach (var pack in _packs)
        {
            var soldiers = Enumerable.Repeat(new WaveSoldier(pack.TroopId, pack.Slot), pack.PackSize).ToList();
            fx.DefenderBatches.AddRange(Batches(soldiers, new int[2], CountBySlot(soldiers), true, _defenderPlaced));
        }

        _packs.Clear();
    }

    private IReadOnlyList<WaveSoldier> TakeMusterBatch(int index)
    {
        if (index >= _musterBatches.Count)
            return Array.Empty<WaveSoldier>();

        var start = _musterEmitted;
        var batch = _musterFoot.Skip(start).Take(_musterBatches[index].Count).ToList();
        _musterEmitted += batch.Count;
        return batch;
    }

    private static int[] CountBySlot(IEnumerable<WaveSoldier> soldiers)
    {
        var counts = new int[2];
        foreach (var soldier in soldiers)
            counts[(int)soldier.Slot]++;
        return counts;
    }

    /// <summary>
    /// One batch per run of same-troop soldiers in a class. <paramref name="nextIndex"/> carries each class's running index and
    /// <paramref name="placed"/> whether the class has had its placing batch: the first batch of a class places it, none after.
    /// </summary>
    private static IEnumerable<SoldierBatch> Batches(IReadOnlyList<WaveSoldier> soldiers, int[] nextIndex, int[] totals,
        bool reinforcement, bool[] placed, float healthFactor = 1f)
    {
        var batches = new List<SoldierBatch>();
        for (var i = 0; i < soldiers.Count;)
        {
            var head = soldiers[i];
            var count = 0;
            while (i + count < soldiers.Count && soldiers[i + count] == head)
                count++;

            var slot = (int)head.Slot;
            batches.Add(new SoldierBatch(head.TroopId, head.Slot, count, nextIndex[slot], totals[slot], reinforcement, !placed[slot], healthFactor));
            nextIndex[slot] += count;
            placed[slot] = true;
            i += count;
        }

        return batches;
    }
}
