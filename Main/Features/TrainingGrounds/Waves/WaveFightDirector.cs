using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// The mission side of the wave fight (docs/features/training-waves.md): once per mission tick it starts a due wave, keeps the field
/// at the wave's at-once limit, settles the wave when the player goes down or the last fighter falls, and takes the player back to the
/// master. Every gold move goes through <see cref="WaveBetService"/>; every engine touch goes through <see cref="IArenaWaveArena"/>.
/// Counts are polled here, never taken from an engine callback, because those can run off the main thread. One instance for each mission.
/// </summary>
internal sealed class WaveFightDirector
{
    private const string Tag = "[TrainingWaves]";
    private const int MinFrames = 2;
    private const int VanillaStoodDown = 30;

    private readonly WaveBetService _bets;
    private readonly IArenaWaveArena _arena;
    private readonly ITrainingWavesConfigProvider _config;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly Func<int, int> _below;
    private readonly Queue<string> _queue = new();

    private int _stage;
    private int _count;
    private int _atOnce;
    private int _spawned;
    private int _failedSpawns;
    private int _peak;
    private float _nextSpawn;
    private float? _returnAt;
    private bool _standingDown;
    private bool _faulted;

    /// <param name="below">A draw from 0 to n - 1 for the n it is given (the engine's random source in the game).</param>
    public WaveFightDirector(
        WaveBetService bets,
        IArenaWaveArena arena,
        ITrainingWavesConfigProvider config,
        IOutcomeLog outcomes,
        IModLogger logger,
        Func<int, int> below)
    {
        _bets = bets;
        _arena = arena;
        _config = config;
        _outcomes = outcomes;
        _logger = logger;
        _below = below;
    }

    /// <summary>A fight is on: the player has a stake in it, so leaving the yard now forfeits it.</summary>
    public bool FightInProgress => _bets.RunState == WaveRunState.Fighting;

    /// <summary>
    /// Opens the offer for this yard. A scene with fewer than two spawn frames is left detached, so the offer stays hidden: the
    /// fighters need a frame far from the player, and vanilla's own pick fails on a lone one.
    /// </summary>
    public void Attach()
    {
        var frames = _arena.FrameCount;
        if (frames < MinFrames)
        {
            _logger.LogWarning($"{Tag} The yard has {frames} spawn frame{(frames == 1 ? "" : "s")} and the wave fight needs {MinFrames}: the offer stays hidden");
            return;
        }

        _bets.AttachArena();
    }

    public void Tick(float now)
    {
        if (_faulted || !_bets.ArenaAttached || _arena.MissionEnded)
            return;

        var run = _bets.Run;
        if (run == null)
            return;

        if (run.State == WaveRunState.WaveDue && !_arena.InConversation)
            StartWave(run, now);
        if (run.State == WaveRunState.Fighting)
            TickFight(run, now);
        if (_returnAt is { } at && now >= at)
            ReturnAfterWave();
    }

    /// <summary>
    /// The mission is over. A wave that was due is refunded, a fight left unfinished forfeits its stake to the town (the player left it),
    /// a run paused between waves ends with its winnings.
    /// </summary>
    public void MissionEnding()
    {
        switch (_bets.RunState)
        {
            case WaveRunState.WaveDue:
                var due = _bets.Run?.Wave ?? 0;
                AnnounceRefund(due, _bets.Refund());
                break;
            case WaveRunState.Fighting:
                _bets.Forfeit();
                break;
            case WaveRunState.Idle:
            case WaveRunState.BetweenWaves:
                _bets.Withdraw();
                break;
        }

        Detach();
    }

    /// <summary>Lets go of the yard. Safe to call twice; whatever run is still alive is settled by the service as a last resort.</summary>
    public void Detach()
    {
        _returnAt = null;
        _standingDown = false;
        _queue.Clear();
        _bets.DetachArena();
    }

    /// <summary>
    /// The tick threw. The player must never be left in a fight with his stake held: a fight that the fault ended gives the stake back
    /// (it is not the player's loss), a due wave is refunded, a pause ends with the winnings, the offer is hidden for the rest of the
    /// mission, and the player is taken back to the master when vanilla's yard had been stood down. The stand-down flag goes up before the call that
    /// fades the fighters, so a throw from inside that call still counts as stood down. Runs once.
    /// </summary>
    public void Fault()
    {
        if (_faulted)
            return;

        _faulted = true;
        var standingDown = _standingDown;
        _returnAt = null;
        _standingDown = false;
        _queue.Clear();
        var wave = _bets.Run?.Wave ?? 0;
        var back = _bets.AbortFight();
        if (back == 0)
            back = _bets.Refund();
        _bets.DetachArena();
        AnnounceRefund(wave, back);
        if (standingDown && !_arena.MissionEnded && !_arena.InConversation)
            Return();
    }

    private void StartWave(WaveRun run, float now)
    {
        var row = run.Row;
        var roster = WaveRoster.Build(_arena.TownTroops(), row.TierWeights, row.Count, _below);
        if (roster.Count == 0)
        {
            _logger.LogWarning($"{Tag} The town has no troop for wave {run.Wave}: the stake is refunded");
            RefundDueWave(run);
            return;
        }

        _standingDown = true;
        if (!_arena.StandVanillaDown(out var faded))
        {
            _logger.LogWarning($"{Tag} Vanilla's practice could not be stood down: the stake is refunded");
            _standingDown = false;
            RefundDueWave(run);
            return;
        }

        var vanilla = _arena.VanillaSpawnedCount;
        var detail = $"faded={faded} vanilla_spawned={vanilla}";
        if (vanilla == VanillaStoodDown)
            _outcomes.Pass(TrainingWavesChecks.StandDown, "", detail);
        else
            _outcomes.Fail(TrainingWavesChecks.StandDown, "", detail);

        if (!_arena.SpawnPlayer(row.WeaponStage))
        {
            _logger.LogWarning($"{Tag} The player's body for wave {run.Wave} could not be built: the stake is refunded");
            RefundDueWave(run);
            Return();
            return;
        }

        run.BeginFight();
        _queue.Clear();
        foreach (var troop in roster)
            _queue.Enqueue(troop);
        _stage = row.WeaponStage;
        _count = row.Count;
        _atOnce = row.AtOnce;
        _spawned = 0;
        _failedSpawns = 0;
        _peak = 0;
        _nextSpawn = now;
        _arena.Announce(WaveAnnouncement.WaveStart, run.Wave, row.Count);
    }

    /// <summary>A wave that could not start: the stake goes back and the player is told so.</summary>
    private void RefundDueWave(WaveRun run)
    {
        var wave = run.Wave;
        AnnounceRefund(wave, _bets.Refund());
    }

    /// <summary>
    /// Tells the player that a stake came back; nothing is shown when none did. Every refund goes through here, and it runs before any
    /// attempt to take the player back to the master, so a return that throws cannot hide it.
    /// </summary>
    private void AnnounceRefund(int wave, int back)
    {
        if (back > 0)
            _arena.Announce(WaveAnnouncement.Refunded, wave, back);
    }

    private void TickFight(WaveRun run, float now)
    {
        if (_arena.PlayerDown)
        {
            EndWaveLost(run, now);
            return;
        }

        var active = _arena.ActiveFighters;
        if (active > _peak)
            _peak = active;

        if (_queue.Count == 0 && active == 0)
            EndWaveCleared(run, now);
        else if (_queue.Count > 0 && active < _atOnce && (_spawned < _atOnce || now >= _nextSpawn))
            SpawnOne(now);
    }

    private void SpawnOne(float now)
    {
        if (!_arena.AgentRoom)
            return;

        var troop = _queue.Dequeue();
        if (_arena.SpawnFighter(troop, _stage))
        {
            _spawned++;
            _nextSpawn = now + _config.GetTable().Rules.RefillSeconds;
            _peak = Math.Max(_peak, _arena.ActiveFighters);
            return;
        }

        _failedSpawns++;
        if (_failedSpawns == 1)
            _logger.LogWarning($"{Tag} A fighter ('{troop}') could not be spawned and was dropped from the wave");
    }

    private void EndWaveCleared(WaveRun run, float now)
    {
        var wave = run.Wave;
        if (_spawned == 0)
        {
            _logger.LogWarning($"{Tag} Wave {wave} spawned no fighter at all: the stake is refunded");
            AnnounceRefund(wave, _bets.AbortFight());
            _queue.Clear();
            Return();
            return;
        }

        var payout = _bets.PayClearedWave();
        LogWave(wave, cleared: true);
        _arena.Announce(WaveAnnouncement.WaveCleared, wave, payout);
        _returnAt = now + _config.GetTable().Rules.ReturnSeconds;
    }

    private void EndWaveLost(WaveRun run, float now)
    {
        var wave = run.Wave;
        var stake = _bets.LoseWave();
        LogWave(wave, cleared: false);
        _queue.Clear();
        _arena.Announce(WaveAnnouncement.WaveLost, wave, stake);
        _returnAt = now + _config.GetTable().Rules.ReturnSeconds;
    }

    private void LogWave(int wave, bool cleared)
    {
        var ok = _peak <= _atOnce && (!cleared || _spawned == _count);
        var detail = $"wave={wave} spawned={_spawned} count={_count} peak={_peak} at_once={_atOnce} cleared={Flag(cleared)}";
        if (ok)
            _outcomes.Pass(TrainingWavesChecks.Wave, "", detail);
        else
            _outcomes.Fail(TrainingWavesChecks.Wave, "", detail);
    }

    /// <summary>The wave is settled and the return time is up: the master owes the debrief, so the flag is set before he speaks.</summary>
    private void ReturnAfterWave()
    {
        _returnAt = null;
        var run = _bets.Run;
        _bets.MarkDebrief();
        if (!Return() && run?.State == WaveRunState.BetweenWaves)
            _bets.Withdraw();
    }

    private static string Flag(bool value) => value ? "true" : "false";

    /// <summary>
    /// Puts the yard back, stands the player at the master and opens the conversation; logs the <c>return</c> check. The stand-down flag
    /// falls only when the return came back: if it throws part way, the fault path still sees a yard to restore and tries once more.
    /// </summary>
    private bool Return()
    {
        var opened = _arena.ReturnToMaster();
        _standingDown = false;
        var detail = $"opened={Flag(opened)} in_conversation={Flag(_arena.InConversation)} player_down={Flag(_arena.PlayerDown)}";
        if (opened && _arena.InConversation && !_arena.PlayerDown)
            _outcomes.Pass(TrainingWavesChecks.Return, "", detail);
        else
            _outcomes.Fail(TrainingWavesChecks.Return, "", detail);
        return opened;
    }
}
