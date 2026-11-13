using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

internal sealed partial class HeroDefenceRun
{
    /// <summary>A team has 8 regular formations, and one pack fills one formation.</summary>
    private const int PackSlots = 8;

    private readonly Dictionary<AgentKey, (CreepSpec Creep, int Slot, int PackId)> _alive = new();
    private readonly int[] _slotCounts = new int[PackSlots];

    /// <summary>The pack last released into each slot.</summary>
    private readonly int[] _slotPack = new int[PackSlots];

    private WaveBrief _brief = new(0, Array.Empty<CreepPack>());
    private int _lastPackId;
    private int _packNext;
    private float _packClock;
    private float _waveSeconds;
    private float _waveLimit;
    private int _waveSpawned;
    private int _waveLeaked;
    private bool _waveTimedOut;

    private bool QueueDone => _packNext >= _brief.Packs.Count;

    private bool AnyHeroStanding => _roster.Any(hero => hero.State == DefenceHeroState.Placed);

    /// <summary>The director calls this for each creep that it spawned from a batch. A key that is already alive is ignored, so a repeat cannot count a creep twice.</summary>
    public void CreepSpawned(AgentKey key, CreepSpec creep, int slot)
    {
        if (_alive.ContainsKey(key))
            return;

        _alive[key] = (creep, slot, _slotPack[slot]);
        _slotCounts[slot]++;
        _waveSpawned++;
    }

    /// <summary>A creep left the field. It counts once per key, so a late engine callback for a removed creep changes nothing.</summary>
    public void CreepGone(AgentKey key, CreepEnd end)
    {
        if (Result != null || !Unregister(key, out var creep))
            return;

        switch (end)
        {
            case CreepEnd.Killed:
                Kills++;
                AddGold(Bounty(creep));
                break;
            case CreepEnd.Leaked:
                _pending.Removals.Add(key);
                LoseLives(creep);
                break;
            case CreepEnd.Culled:
                _pending.Removals.Add(key);
                break;
        }
    }

    /// <summary>
    /// The director calls this with a batch's <see cref="CreepBatch.PackId"/> when that pack's <see cref="PackStallClock"/> runs out. The
    /// pack ends by the wave-limit split: with a hero standing it goes for free, with none it leaks. Outside a wave, or for a pack with
    /// no live creep (an old pack whose slot a new pack took), nothing changes.
    /// </summary>
    public void PackStalled(int packId)
    {
        if (_phase != DefencePhase.Wave || Result != null)
            return;

        var end = AnyHeroStanding ? CreepEnd.Culled : CreepEnd.Leaked;
        EndGroup(_alive.Where(entry => entry.Value.PackId == packId).Select(entry => entry.Key), end);
    }

    /// <summary>Removes a creep from the books. False for a key that is not alive.</summary>
    private bool Unregister(AgentKey key, out CreepSpec creep)
    {
        if (!_alive.TryGetValue(key, out var entry))
        {
            creep = null!;
            return false;
        }

        _alive.Remove(key);
        _slotCounts[entry.Slot]--;
        creep = entry.Creep;
        return true;
    }

    /// <summary>Culls or leaks a group of creeps. If a leak takes the last life, the rest of the group still leaves the field, with no score.</summary>
    private void EndGroup(IEnumerable<AgentKey> keys, CreepEnd end)
    {
        foreach (var key in keys.ToList())
        {
            if (Result == null)
                CreepGone(key, end);
            else if (Unregister(key, out _))
                _pending.Removals.Add(key);
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
        _waveTimedOut = false;
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

    /// <summary>
    /// A NaN or infinite position fails the distance test, so a bad reading never costs a life. Every creep at the exit leaves, also
    /// after one of them takes the last life.
    /// </summary>
    private void ObserveLeaks(FieldReading? field)
    {
        if (field == null)
            return;

        EndGroup(field.Creeps.Where(sample => sample.Position.Distance2D(_plan.Map.Exit) < _r.ExitRadiusMetres).Select(sample => sample.Key), CreepEnd.Leaked);
    }

    private void ReleaseNextPack(float dt)
    {
        _packClock -= dt;
        if (QueueDone || _packClock > 0f)
            return;

        var slot = Array.FindIndex(_slotCounts, count => count == 0);
        if (slot < 0)
            return;

        _lastPackId++;
        _slotPack[slot] = _lastPackId;
        _pending.CreepBatches.Add(new CreepBatch(_lastPackId, slot, _brief.Packs[_packNext]));
        _packNext++;
        _packClock = _r.PackGapSeconds;
    }

    /// <summary>
    /// The hard limit of a wave. With a hero standing the rest goes for free. With no hero standing the rest counts as leaks, so the
    /// limit cannot turn a loss into a clear. A wave that reaches the limit pays no clear bonus: the limit is a backstop for when the
    /// stall clocks did not end the wave, not a clean clear (Mike, 2026-11-13).
    /// </summary>
    private void ExpireWave()
    {
        _waveTimedOut = true;
        var end = AnyHeroStanding ? CreepEnd.Culled : CreepEnd.Leaked;
        EndGroup(_alive.Keys, end);

        if (end == CreepEnd.Leaked)
        {
            foreach (var creep in _brief.Packs.Skip(_packNext).SelectMany(pack => pack.Creeps))
            {
                if (Result != null)
                    break;

                LoseLives(creep);
            }
        }

        _packNext = _brief.Packs.Count;
    }

    private void ClearWave()
    {
        WavesCleared++;
        if (!_waveTimedOut)
        {
            var leakedShare = _waveSpawned > 0 ? (double)_waveLeaked / _waveSpawned : 0d;
            var bonus = ((double)_r.WaveBonusBase + (double)_r.WaveBonusPerWave * Wave) * (1d - leakedShare);
            AddGold(Math.Round(bonus, MidpointRounding.AwayFromZero));
        }

        _phase = DefencePhase.Pause;
        _left = _r.PauseSeconds;

        foreach (var hero in _roster)
        {
            if (hero.State == DefenceHeroState.Placed)
            {
                Need(hero, HeroWork.Reset);
            }
            else if (hero.State == DefenceHeroState.Fallen)
            {
                hero.State = DefenceHeroState.Placed;
                Need(hero, HeroWork.Spawn);
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
