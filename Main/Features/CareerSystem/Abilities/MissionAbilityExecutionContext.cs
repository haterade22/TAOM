using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CareerSystem.Abilities;

// Boundary adapter — the only class that touches TaleWorlds Agent/Mission types.
// All ability effect logic in executors is expressed via IAbilityExecutionContext,
// keeping services and executors free of sealed TaleWorlds dependencies.
public sealed class MissionAbilityExecutionContext : IAbilityExecutionContext
{
    private readonly Agent _agent;
    private readonly Mission _mission;
    private readonly IModLogger _logger;

    // Tracks timed buff expiry: (property, originalValue, expiresAt)
    // Cleared in OnMissionTick via the behavior.
    private readonly List<PendingRestore> _pendingRestores = new List<PendingRestore>();

    public string HeroStringId { get; }
    public float Duration { get; }
    public float Radius { get; }

    public MissionAbilityExecutionContext(
        string heroStringId,
        float duration,
        float radius,
        Agent agent,
        Mission mission,
        IModLogger logger)
    {
        HeroStringId = heroStringId;
        Duration = duration;
        Radius = radius;
        _agent = agent;
        _mission = mission;
        _logger = logger;
    }

    public void ApplySpeedBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var props = _agent.AgentDrivenProperties;
        float original = props.MaxSpeedMultiplier;
        props.MaxSpeedMultiplier = original * multiplier;
        props.CombatMaxSpeedMultiplier = props.CombatMaxSpeedMultiplier * multiplier;
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            if (_agent?.AgentDrivenProperties == null) return;
            _agent.AgentDrivenProperties.MaxSpeedMultiplier = original;
            _agent.AgentDrivenProperties.CombatMaxSpeedMultiplier =
                _agent.AgentDrivenProperties.CombatMaxSpeedMultiplier / multiplier;
            _agent.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: SpeedBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyDamageBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var props = _agent.AgentDrivenProperties;
        float original = props.DamageMultiplierBonus;
        props.DamageMultiplierBonus = original + (multiplier - 1f);
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            if (_agent?.AgentDrivenProperties == null) return;
            _agent.AgentDrivenProperties.DamageMultiplierBonus = original;
            _agent.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: DamageBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyResistanceBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var props = _agent.AgentDrivenProperties;
        // Reduce encumbrance as a proxy for resistance (lighter feel = absorbs more)
        float original = props.ArmorEncumbrance;
        props.ArmorEncumbrance = original / multiplier;
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            if (_agent?.AgentDrivenProperties == null) return;
            _agent.AgentDrivenProperties.ArmorEncumbrance = original;
            _agent.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: ResistanceBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyMoraleBurst(float radius, float magnitude)
    {
        if (_agent == null || _mission == null) return;

        var allies = new MBList<Agent>();
        _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, allies);

        int boosted = 0;
        foreach (var ally in allies)
        {
            var ai = ally?.GetComponent<CommonAIComponent>();
            if (ai == null) continue;

            ai.Morale = Math.Min(100f, ai.Morale + magnitude);
            boosted++;
        }

        _logger.LogDebug($"CareerSystem: MoraleBurst r={radius} +{magnitude} boosted {boosted} allies for '{HeroStringId}'");
    }

    public void ApplyStealthMode(float duration)
    {
        // Stealth in Bannerlord is controlled by detection radius on the agent.
        // We log the intent; full AI detection integration requires a Harmony prefix
        // on the detection model (out of scope for Phase C, tracked as future work).
        _logger.LogDebug($"CareerSystem: StealthMode for {duration}s applied to '{HeroStringId}' (visual-only Phase C)");
    }

    public void PlaySound(string soundId)
    {
        // Sound playback requires SoundEvent.CreateEvent — deferred to Phase D
        _logger.LogDebug($"CareerSystem: PlaySound '{soundId}' requested for '{HeroStringId}'");
    }

    public void PlayParticle(string particleId)
    {
        // Particle playback requires engine particle handle — deferred to Phase D
        _logger.LogDebug($"CareerSystem: PlayParticle '{particleId}' requested for '{HeroStringId}'");
    }

    // Called by CareerPerkMissionBehavior on each tick to expire timed buffs.
    public void Tick(float currentMissionTime)
    {
        _pendingRestores.RemoveAll(r =>
        {
            if (currentMissionTime >= r.ExpiresAt)
            {
                r.Restore();
                return true;
            }
            return false;
        });
    }

    private void ScheduleRestore(Action restore, float duration)
    {
        float expiresAt = _mission?.CurrentTime + duration ?? duration;
        _pendingRestores.Add(new PendingRestore(restore, expiresAt));
    }

    private sealed class PendingRestore
    {
        private readonly Action _restore;
        public float ExpiresAt { get; }

        public PendingRestore(Action restore, float expiresAt)
        {
            _restore = restore;
            ExpiresAt = expiresAt;
        }

        public void Restore() => _restore();
    }
}
