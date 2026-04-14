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

    // Tracks timed buff expiry; entries cleared when restore fires.
    private readonly List<PendingRestore> _pendingRestores = new List<PendingRestore>();
    private readonly MBList<Agent> _nearbyAlliesBuffer = new MBList<Agent>();

    public string HeroStringId { get; }
    public float Duration { get; }
    public float Radius { get; }

    // True when all timed restores have fired — used by CareerPerkMissionBehavior to prune finished contexts.
    public bool IsExpired => _pendingRestores.Count == 0;

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

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.SpeedMultiplier += multiplier - 1f;
        buffs.CombatSpeedMultiplier += multiplier - 1f;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            CareerAbilityBuffTracker.ClearBuff(HeroStringId);
            _agent?.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: SpeedBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyDamageBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.DamageBonus += multiplier - 1f;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            CareerAbilityBuffTracker.ClearBuff(HeroStringId);
            _agent?.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: DamageBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyResistanceBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        // Reduce encumbrance as a proxy for resistance (lighter feel = absorbs more)
        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.ArmorReduction += 1f - (1f / multiplier);
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleRestore(() =>
        {
            CareerAbilityBuffTracker.ClearBuff(HeroStringId);
            _agent?.UpdateAgentProperties();
        }, duration);

        _logger.LogDebug($"CareerSystem: ResistanceBuff x{multiplier} applied to '{HeroStringId}' for {duration}s");
    }

    public void ApplyMoraleBurst(float radius, float magnitude)
    {
        if (_agent == null || _mission == null) return;

        _nearbyAlliesBuffer.Clear();
        _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);
        var allies = _nearbyAlliesBuffer;

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
        for (int i = _pendingRestores.Count - 1; i >= 0; i--)
        {
            if (currentMissionTime >= _pendingRestores[i].ExpiresAt)
            {
                _pendingRestores[i].Restore();
                _pendingRestores.RemoveAt(i);
            }
        }
    }

    private float CurrentTime() => _mission?.CurrentTime ?? 0f;

    private void ScheduleRestore(Action restore, float duration)
    {
        float expiresAt = CurrentTime() + duration;
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
