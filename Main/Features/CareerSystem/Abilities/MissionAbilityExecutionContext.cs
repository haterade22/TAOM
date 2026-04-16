using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
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

        ScheduleHeroRestore(duration);
    }

    public void ApplyDamageBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.DamageBonus += multiplier - 1f;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void ApplyResistanceBuff(float multiplier, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.ArmorReduction += 1f - (1f / multiplier);
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void ApplyMoraleBurst(float radius, float magnitude)
    {
        if (_agent == null || _mission == null) return;

        _nearbyAlliesBuffer.Clear();
        _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);

        int boosted = 0;
        foreach (var ally in _nearbyAlliesBuffer)
        {
            var ai = ally?.GetComponent<CommonAIComponent>();
            if (ai == null) continue;

            ai.Morale = Math.Min(100f, ai.Morale + magnitude);
            boosted++;
        }
    }

    public void ApplyStealthMode(float duration)
    {
        // Stealth in Bannerlord is controlled by detection radius on the agent.
        // Full AI detection integration requires a Harmony prefix on the detection model.
        _logger.LogDebug($"CareerSystem: StealthMode for {duration}s applied to '{HeroStringId}' (visual-only)");
    }

    public void ApplyAllyBuff(float damageBonusFlat, float damageReductionFlat, float radius, float duration)
    {
        if (_agent == null || _mission == null) return;

        _nearbyAlliesBuffer.Clear();
        _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);

        int buffed = 0;
        foreach (var ally in _nearbyAlliesBuffer)
        {
            if (ally == null || !ally.IsHuman || !ally.IsActive()) continue;

            var allyBuffs = new ActiveBuffs
            {
                DamageBonus = damageBonusFlat,
                DamageReductionBonus = damageReductionFlat,
                ExpiresAt = CurrentTime() + duration
            };

            CareerAbilityBuffTracker.SetAllyBuff(ally.Index, allyBuffs);
            ally.UpdateAgentProperties();
            buffed++;

            var allyIndex = ally.Index;
            var allyRef = ally;
            var expectedExpiry = CurrentTime() + duration;
            ScheduleRestore(() =>
            {
                // Only clear if the buff hasn't been replaced (guards against reactivation/index reuse)
                var current = CareerAbilityBuffTracker.GetAllyBuff(allyIndex);
                if (current != null && current.ExpiresAt <= expectedExpiry)
                {
                    CareerAbilityBuffTracker.ClearAllyBuff(allyIndex);
                    if (allyRef.IsActive())
                        allyRef.UpdateAgentProperties();
                }
            }, duration);
        }

        // Also buff the hero
        var heroBuff = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        heroBuff.DamageBonus += damageBonusFlat;
        heroBuff.DamageReductionBonus += damageReductionFlat;
        heroBuff.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, heroBuff);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void ApplyDrawSpeedBuff(float bonus, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.DrawSpeedBonus += bonus;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void ApplyMountSpeedBuff(float bonus, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.MountSpeedBonus += bonus;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void ApplyChargeDamageBuff(float bonus, float duration)
    {
        if (_agent?.AgentDrivenProperties == null) return;

        var buffs = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
        buffs.ChargeDamageBonus += bonus;
        buffs.ExpiresAt = CurrentTime() + duration;
        CareerAbilityBuffTracker.SetBuff(HeroStringId, buffs);
        _agent.UpdateAgentProperties();

        ScheduleHeroRestore(duration);
    }

    public void PlaySound(string soundId)
    {
        if (string.IsNullOrEmpty(soundId)) return;
        var eventId = SoundEvent.GetEventIdFromString(soundId);
        if (eventId >= 0)
            SoundEvent.PlaySound2D(eventId);
    }

    public void PlayParticle(string particleId)
    {
        // Particle playback requires registered particle definitions (asset files).
        // Guard against missing assets gracefully.
        if (string.IsNullOrEmpty(particleId)) return;
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

    // Hero buff restore with expiry guard: only clears if buff hasn't been replaced by a reactivation.
    private void ScheduleHeroRestore(float duration)
    {
        var expectedExpiry = CurrentTime() + duration;
        ScheduleRestore(() =>
        {
            var current = CareerAbilityBuffTracker.GetBuff(HeroStringId);
            if (current != null && current.ExpiresAt <= expectedExpiry)
            {
                CareerAbilityBuffTracker.ClearBuff(HeroStringId);
                _agent?.UpdateAgentProperties();
            }
        }, duration);
    }

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
