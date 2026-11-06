using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The commander side of the run (phase2-plan.md section 2): a commander every few waves who enters through the attackers' spawn queue, abilities on their own clocks (a summon queues
/// soldiers at the head of the queue, a war cry raises every attacker's stats for a while), and a permanent stop when he dies or leaves the field. The clock is the wave's seconds.
/// </summary>
internal sealed partial class SurvivalRun
{
    private CommanderState? _commander;
    private bool _commanderQueued;
    private CommanderAbility? _warCry;
    private float _warCryEnds;
    private bool _warCryEndedOutside;
    private int _summonUses;
    private int _summoned;
    private int _summonSpawned;
    private int _summonKills;
    private int _summonFelled;

    /// <summary>The running wave's commander (kept after he ends, until the next wave begins), or null in a wave with none.</summary>
    public CommanderState? Commander => _commander;

    /// <summary>Seconds of the running wave: the clock the commander's abilities run on.</summary>
    public float WaveSeconds => _waveSeconds;

    private void InitCommander(CommanderSpec? spec)
    {
        _commander = spec == null ? null : new CommanderState(spec);
        _commanderQueued = false;
        _warCry = null;
        _warCryEndedOutside = false;
        _summonUses = 0;
        _summoned = 0;
        _summonSpawned = 0;
        _summonKills = 0;
        _summonFelled = 0;
    }

    /// <summary>The commander goes to the head of the attacker queue, so he is the next soldier to spawn and takes one slot of live room like anyone. He is outside the wave's roster count.</summary>
    private void QueueCommander()
    {
        if (_commander == null || _commanderQueued)
            return;

        _commanderQueued = true;
        _queue.Insert(_queueHead, new WaveSoldier(_commander.Spec.Rank.TroopId, FormationSlot.Infantry, AttackerRole.Commander));
    }

    /// <summary>The engine spawned the commander: his ability clocks start, each cooldown stretched by the Legend boons owned. Ignored when he is not due, is already up or has ended.</summary>
    public void CommanderUp()
    {
        if (_commander is not { Up: false, Ended: false } c || Result != null)
            return;

        c.Up = true;
        c.UpSeconds = _waveSeconds;
        var stretch = RunModifiers.CommanderCooldown(_deck.Owned);
        foreach (var ability in c.Spec.Abilities)
        {
            var cooldown = Math.Max(CommanderPlan.MinCooldownSeconds, ability.CooldownSeconds * stretch);
            c.Clocks.Add((ability, new CommanderClock(_waveSeconds, ability.FirstSeconds, cooldown)));
        }
    }

    /// <summary>
    /// The commander is gone: <paramref name="killed"/> for a kill or a knock-out, false for a removal that is no loss (a rout, a deletion only if native raises a removal for it (risk 2), a spawn that failed). Every clock stops for good, a war
    /// cry in progress ends, and a kill pays his rank's bounty. Soldiers he already summoned still come (assumption A6). Once only.
    /// </summary>
    public void CommanderDown(bool killed) => EndCommander(killed ? CommanderOutcome.Killed : CommanderOutcome.Left);

    private void EndCommander(CommanderOutcome outcome)
    {
        if (_commander is not { Ended: false } c || Result != null)
            return;

        c.End(outcome, _waveSeconds);
        if (_warCry != null)
        {
            _warCry = null;
            _warCryEndedOutside = true;
            _pendingEvents.Add(new RunEvent(RunEventKind.WarCryEnded, _wave, "commander=down"));
        }

        if (outcome == CommanderOutcome.Killed)
            _purse.Earn(PurseSource.Commander, c.Spec.Rank.Bounty);
        _pendingEvents.Add(new RunEvent(RunEventKind.CommanderEnded, _wave, $"outcome={outcome.ToString().ToLowerInvariant()}"));
    }

    /// <summary>Ends a war cry that ran its course, fires every clock that is due (once each), and publishes and refreshes the attackers' stats when a war cry began or ended.</summary>
    private void TickCommander(RunEffects fx)
    {
        var changed = _warCryEndedOutside;
        _warCryEndedOutside = false;
        if (_commander is { Up: true, Ended: false } c)
        {
            if (_warCry != null && _waveSeconds >= _warCryEnds)
            {
                _warCry = null;
                changed = true;
                fx.Events.Add(new RunEvent(RunEventKind.WarCryEnded, _wave, "duration=over"));
            }

            foreach (var (ability, clock) in c.Clocks)
            {
                if (!clock.Tick(_waveSeconds))
                    continue;

                c.Use(ability.Id);
                changed |= Fire(ability, fx);
            }
        }

        if (!changed)
            return;

        fx.PublishStats = StatTableNow();
        fx.RefreshAttackers = true;
    }

    /// <summary>Uses one ability. True when the attackers' stats changed (a war cry began).</summary>
    private bool Fire(CommanderAbility ability, RunEffects fx)
    {
        if (ability.Kind == CommanderAbilityKind.WarCry)
        {
            _warCry = ability;
            _warCryEnds = _waveSeconds + ability.DurationSeconds;
            fx.Events.Add(new RunEvent(RunEventKind.WarCry, _wave, $"ability={ability.Id} seconds={ability.DurationSeconds.ToString("F0", CultureInfo.InvariantCulture)}"));
            return true;
        }

        var use = ++_summonUses;
        var soldiers = WavePlan.Expand(ability.Count, ability.Mix).Select(s => s with { Role = AttackerRole.Summoned, Use = use }).ToList();
        _queue.InsertRange(_queueHead, soldiers);
        _summoned += soldiers.Count;
        fx.Events.Add(new RunEvent(RunEventKind.Summoned, _wave, $"use={use} count={soldiers.Count} at={_waveSeconds.ToString("F1", CultureInfo.InvariantCulture)} ability={ability.Id}"));
        return false;
    }

    private float AttackerHealth(WaveSoldier soldier) =>
        soldier.Role == AttackerRole.Commander && _commander != null ? _healthFactor * _commander.Spec.Health : _healthFactor;

    private WaveRoles WaveRolesNow() => new(_summoned, _summonSpawned, _summonKills, _summonFelled, CommanderRecordNow());

    private CommanderRecord? CommanderRecordNow() => _commander is not { } c
        ? null
        : new CommanderRecord(c.Spec.Rank.TroopId, c.Spec.Rank.Id, c.Spec.Steps, c.Outcome, c.Uses.ToDictionary(u => u.Key, u => u.Value), c.UsesAfterEnd, c.AliveSeconds);

    private CommanderView? CommanderViewNow()
    {
        if (_phase != RunPhase.Wave || _commander is not { } c)
            return null;

        var abilities = c.Spec.Abilities.Select(a =>
        {
            var clock = c.Clocks.Where(x => x.Ability.Id == a.Id).Select(x => x.Clock).FirstOrDefault();
            return new CommanderAbilityView(a.Name, clock?.SecondsLeft(_waveSeconds) ?? (c.Ended ? 0f : a.FirstSeconds), _warCry?.Id == a.Id);
        }).ToList();
        return new CommanderView(c.Spec.Rank.Name, c.Outcome, c.Up, abilities);
    }
}
