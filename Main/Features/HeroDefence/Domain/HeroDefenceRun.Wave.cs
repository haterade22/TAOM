using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

internal sealed partial class HeroDefenceRun
{
    /// <summary>A team has 8 regular formations, and one pack fills one formation.</summary>
    private const int PackSlots = 8;

    private readonly Dictionary<AgentKey, (CreepSpec Creep, int Slot)> _alive = new();
    private readonly int[] _slotCounts = new int[PackSlots];

    private WaveBrief _brief = new(0, Array.Empty<CreepPack>());
    private int _packNext;
    private float _packClock;
    private float _waveSeconds;
    private float _waveLimit;
    private int _waveSpawned;
    private int _waveLeaked;

    private bool QueueDone => _packNext >= _brief.Packs.Count;

    private bool AnyHeroStanding => _roster.Any(hero => hero.State == DefenceHeroState.Placed);

    /// <summary>The director calls this for each creep that it spawned from a batch. A key that is already alive is ignored, so a repeat cannot count a creep twice.</summary>
    public void CreepSpawned(AgentKey key, CreepSpec creep, int slot)
    {
        if (_alive.ContainsKey(key))
            return;

        _alive[key] = (creep, slot);
        _slotCounts[slot]++;
        _waveSpawned++;
    }

    /// <summary>A creep left the field. It counts once per key, so a late engine callback for a removed creep changes nothing.</summary>
    public void CreepGone(AgentKey key, CreepEnd end)
    {
        if (Result != null || !_alive.TryGetValue(key, out var entry))
            return;

        _alive.Remove(key);
        _slotCounts[entry.Slot]--;
        switch (end)
        {
            case CreepEnd.Killed:
                Kills++;
                AddGold(Bounty(entry.Creep));
                break;
            case CreepEnd.Leaked:
                _pending.Removals.Add(key);
                LoseLives(entry.Creep);
                break;
            case CreepEnd.Culled:
                _pending.Removals.Add(key);
                break;
        }
    }

    private void StartWave()
    {
        Wave++;
        _phase = DefencePhase.Wave;
        _brief = _plan.Briefs(Wave);
        _packNext = 0;
        _packClock = 0f;
        _waveSeconds = 0f;
        _waveSpawned = 0;
        _waveLeaked = 0;
        _waveLimit = _r.WaveLimitBaseSeconds + _r.WaveLimitPerCreepSeconds * _brief.Packs.Sum(pack => pack.Creeps.Count);
        _pending.WaveBegan = _brief;
    }

    /// <summary>
    /// The clear test comes first: a pack released in this tick has no registered creeps until the director applies the tick's effects.
    /// The wave limit comes before the release, so that no pack spawns after the limit removed its wave.
    /// </summary>
    private void TickWave(DefenceSnapshot s)
    {
        _waveSeconds += s.Dt;
        if (QueueDone && _alive.Count == 0)
        {
            ClearWave();
            return;
        }

        ObserveLeaks(s.Field);
        if (Result != null)
            return;

        if (_waveSeconds >= _waveLimit)
        {
            ExpireWave();
            return;
        }

        ReleaseNextPack(s.Dt);
    }

    /// <summary>A NaN or infinite position fails the distance test, so a bad reading never costs a life.</summary>
    private void ObserveLeaks(FieldReading? field)
    {
        if (field == null)
            return;

        foreach (var sample in field.Creeps)
        {
            if (sample.Position.Distance2D(_plan.Map.Exit) < _r.ExitRadiusMetres)
                CreepGone(sample.Key, CreepEnd.Leaked);
        }
    }

    private void ReleaseNextPack(float dt)
    {
        _packClock -= dt;
        if (QueueDone || _packClock > 0f)
            return;

        var slot = Array.FindIndex(_slotCounts, count => count == 0);
        if (slot < 0)
            return;

        _pending.CreepBatches.Add(new CreepBatch(slot, _brief.Packs[_packNext]));
        _packNext++;
        _packClock = _r.PackGapSeconds;
    }

    /// <summary>
    /// The hard limit of a wave. With a hero standing the rest goes for free. With no hero standing the rest counts as leaks, so the
    /// limit cannot turn a loss into a clear. An unreleased creep enters the leak share only when it leaks, because a free drop never took the field.
    /// </summary>
    private void ExpireWave()
    {
        var end = AnyHeroStanding ? CreepEnd.Culled : CreepEnd.Leaked;
        foreach (var key in _alive.Keys.ToList())
            CreepGone(key, end);

        if (end == CreepEnd.Leaked)
        {
            foreach (var creep in _brief.Packs.Skip(_packNext).SelectMany(pack => pack.Creeps))
            {
                if (Result != null)
                    break;

                _waveSpawned++;
                LoseLives(creep);
            }
        }

        _packNext = _brief.Packs.Count;
    }

    private void ClearWave()
    {
        WavesCleared++;
        var leakedShare = _waveSpawned > 0 ? (double)_waveLeaked / _waveSpawned : 0d;
        var bonus = ((double)_r.WaveBonusBase + (double)_r.WaveBonusPerWave * Wave) * (1d - leakedShare);
        AddGold(Math.Round(bonus, MidpointRounding.AwayFromZero));
        _phase = DefencePhase.Pause;
        _left = _r.PauseSeconds;

        foreach (var hero in _roster)
        {
            if (hero.State == DefenceHeroState.Placed)
            {
                _pending.HeroResets.Add(hero.Order);
            }
            else if (hero.State == DefenceHeroState.Fallen)
            {
                hero.State = DefenceHeroState.Placed;
                _pending.HeroSpawns.Add(hero.Order);
            }
        }
    }

    private void LoseLives(CreepSpec creep)
    {
        _waveLeaked++;
        Lives = Math.Max(0, Lives - LifeCost(creep.Rank));
        if (Lives == 0)
            End();
    }

    private int LifeCost(CreepRank rank) => rank switch
    {
        CreepRank.Captain => _r.CaptainLifeCost,
        CreepRank.Lieutenant => _r.LieutenantLifeCost,
        CreepRank.General => _r.GeneralLifeCost,
        _ => _r.SoldierLifeCost,
    };

    private double Bounty(CreepSpec creep)
    {
        switch (creep.Rank)
        {
            case CreepRank.Captain:
                return _r.CaptainBounty;
            case CreepRank.Lieutenant:
                return _r.LieutenantBounty;
            case CreepRank.General:
                return _r.GeneralBounty;
        }

        var bounty = (double)_r.BountyBase + _r.BountyPerLevel * creep.Level;
        if (creep.Mounted)
            bounty *= 1d + _r.BountyMountedPercent / 100d;
        return Math.Round(bounty, MidpointRounding.AwayFromZero);
    }
}
