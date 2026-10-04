using System.Collections.Generic;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DreadAura.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// The race abilities' mission tick: ages every ability through its phases and refreshes whoever changed
/// (settling a burnt-out frenzy's morale price), then every half second tops up morale floors, pulses fear
/// auras (strongest aura per enemy, scaled through the registered morale model as the Dread Aura does) and
/// repaints the outlines, posts the message-log waves, and writes the battle report every 30 s of activity.
/// Main thread.
/// Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilityTicker
{
    private const float PulseSeconds = 0.5f;

    private readonly RaceAbilityRuntime _runtime;
    private readonly List<RaceAbilityTransition<Agent>> _transitions = new List<RaceAbilityTransition<Agent>>();
    private readonly RaceAbilityAuraLedger<Agent> _aura = new RaceAbilityAuraLedger<Agent>();
    private readonly List<RaceAbilityWave> _due = new List<RaceAbilityWave>();
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private readonly RaceAbilityReportClock _clock = new RaceAbilityReportClock();
    private float _nextPulse;

    public RaceAbilityTicker(RaceAbilityRuntime runtime) => _runtime = runtime;

    public void Tick(float now)
    {
        MissionThreadGuard.NoteCall("RaceAbilityTicker.Tick", _runtime.Warn);
        _transitions.Clear();
        _runtime.Store.Advance(now, _transitions);
        foreach (var transition in _transitions)
            Settle(transition, now);

        if (now >= _nextPulse)
        {
            _nextPulse = now + PulseSeconds;
            HoldMoraleFloors();
            PulseAuras();
            _runtime.Fallen.Forget(now);
            _runtime.Visuals.Refresh();
        }

        _due.Clear();
        _runtime.Waves.Flush(now, _due);
        if (_due.Count > 0 && _runtime.Settings.Messages)
            foreach (var wave in _due)
                InformationManager.DisplayMessage(new InformationMessage(
                    RaceAbilityNames.Wave(wave.AbilityId, wave.Soldiers, wave.PlayerSide).ToString(),
                    wave.PlayerSide ? Colors.Green : Colors.Red));

        // IsDue first: counting the activations walks the whole telemetry, and the answer is almost always "not yet".
        if (_clock.IsDue(now) && _clock.Due(now, _runtime.Telemetry.Total(RaceAbilityStat.Activations)))
            _runtime.LogReport($"Battle so far ({now:0} s)");
    }

    public void Clear()
    {
        _transitions.Clear();
        _aura.Clear();
        _due.Clear();
        _scratch.Clear();
        _clock.Reset();
        _nextPulse = 0f;
    }

    private void Settle(RaceAbilityTransition<Agent> transition, float now)
    {
        var agent = transition.Key;
        var abilityId = transition.Before.Profile.AbilityId;
        var live = agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);
        if (transition.Before.Phase == RaceAbilityPhase.Active)
            _runtime.Telemetry.Add(abilityId, RaceAbilityStat.Ended);
        var price = _runtime.Service.MoraleOnEnd(transition.Before);
        if (live && price != 0f)
            agent.ChangeMorale(price);
        if (live)
        {
            agent.UpdateAgentProperties();
            agent.MountAgent?.UpdateAgentProperties();
        }
        // The outline ends with the window, not on the next pulse.
        if (transition.Before.Phase == RaceAbilityPhase.Active)
            _runtime.Visuals.Forget(agent);
        if (_runtime.Settings.DebugLog)
            _runtime.Logger.LogInfo($"[RaceAbilities] {abilityId} on {agent.Name}: {transition.Before.Phase} -> {transition.After.Phase} at {now:0.0} s");
    }

    private void HoldMoraleFloors()
    {
        foreach (var pair in _runtime.Store.Entries)
        {
            var floor = pair.Value.CurrentEffects?.MoraleFloor ?? 0f;
            if (floor > 0f && pair.Key.IsActive() && AgentSlotIdentity.IsCurrentOccupant(pair.Key))
                RaceAbilityActivator.TopUpMorale(_runtime, pair.Key, floor, pair.Value.Profile.AbilityId);
        }
    }

    private void PulseAuras()
    {
        var moraleModel = MissionGameModels.Current?.BattleMoraleModel;
        if (moraleModel == null)
            return;
        _aura.Clear();
        foreach (var pair in _runtime.Store.Entries)
        {
            var effects = pair.Value.CurrentEffects;
            var source = pair.Key;
            if (effects == null || !(effects.FearAuraRadius > 0f) || !(effects.FearAuraMoralePerSecond > 0f)
                || source.Team == null || !source.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(source))
                continue;
            var drain = _runtime.Service.AuraDrain(effects.FearAuraMoralePerSecond, PulseSeconds);
            _scratch.Clear();
            Mission.Current.GetNearbyEnemyAgents(source.Position.AsVec2, effects.FearAuraRadius, source.Team, _scratch);
            foreach (var victim in _scratch)
                if (DreadAgentGate.CanAffect(victim))
                    _aura.Offer(victim, moraleModel.CalculateMoraleChangeToCharacter(victim, drain), pair.Value.Profile.AbilityId);
        }
        foreach (var pair in _aura.Entries)
        {
            pair.Key.ChangeMorale(-pair.Value.drain);
            _runtime.Telemetry.Add(pair.Value.abilityId, RaceAbilityStat.Frightened);
        }
        _aura.Clear();
    }
}
