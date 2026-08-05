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
        ApplyAoeBuff(radius, duration, new ActiveBuffs
        {
            DamageBonus = damageBonusFlat,
            DamageReductionBonus = damageReductionFlat,
        });
    }

    public void ApplyAllyRangedBuff(float speedBonus, float damageBonus, float drawSpeedBonus, float radius, float duration)
    {
        ApplyAoeBuff(radius, duration, new ActiveBuffs
        {
            SpeedMultiplier = speedBonus,
            CombatSpeedMultiplier = speedBonus,
            DamageBonus = damageBonus,
            DrawSpeedBonus = drawSpeedBonus,
        });
    }

    public void ApplyAllyCavalryBuff(float mountSpeedBonus, float chargeDamageBonus, float damageBonus, float radius, float duration)
    {
        ApplyAoeBuff(radius, duration, new ActiveBuffs
        {
            MountSpeedBonus = mountSpeedBonus,
            ChargeDamageBonus = chargeDamageBonus,
            DamageBonus = damageBonus,
        });
    }

    // Shared AoE dispatch — applies the same buffTemplate to:
    //   - the caster via the hero buff dictionary (single entry)
    //   - all nearby allies (excluding the caster) via the ally buff dictionary
    // Multiple concurrent activations from different archetypes ACCUMULATE their field
    // contributions; each scheduled restore subtracts only its own deltas, so overlapping
    // auras from different sources compose instead of stomping each other.
    private void ApplyAoeBuff(float radius, float duration, ActiveBuffs buffTemplate)
    {
        if (_agent == null || _mission == null) return;

        _nearbyAlliesBuffer.Clear();
        // IMPORTANT: Mission.GetNearbyAllyAgents INCLUDES the caller's own agent in the result.
        // Vanilla Bannerlord code post-filters the source agent; we must do the same.
        // See: memory/feedback_collection_api_inclusion.md — Codex caught this as P1 (double-buff).
        _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);

        foreach (var ally in _nearbyAlliesBuffer)
        {
            if (ally == null || !ally.IsHuman || !ally.IsActive()) continue;
            if (ally == _agent) continue; // MUST exclude caster — hero buff path handles them

            // Issue #377 — contribution-counted add/remove: the tracker retires the entry
            // when the last restore fires, so GetAllyBuff means "buffed right now".
            CareerAbilityBuffTracker.AddAllyContribution(ally.Index, buffTemplate);
            ally.UpdateAgentProperties();

            var allyIndex = ally.Index;
            var allyRef = ally;
            var deltasCopy = ActiveBuffsAlgebra.Clone(buffTemplate);
            ScheduleRestore(() =>
            {
                CareerAbilityBuffTracker.RemoveAllyContribution(allyIndex, deltasCopy);
                if (allyRef.IsActive())
                    allyRef.UpdateAgentProperties();
            }, duration);
        }

        // Apply to the caster via the hero buff path (also contribution-counted)
        CareerAbilityBuffTracker.AddContribution(HeroStringId, buffTemplate);
        _agent.UpdateAgentProperties();

        var heroDeltasCopy = ActiveBuffsAlgebra.Clone(buffTemplate);
        ScheduleRestore(() =>
        {
            CareerAbilityBuffTracker.RemoveContribution(HeroStringId, heroDeltasCopy);
            _agent?.UpdateAgentProperties();
        }, duration);
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
