using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// Deep-review 2026-08-05 — the "is this agent the career hero" predicate guards three
/// mission boundaries (behavior tick, AgentStatus HUD mixin, hit attribution). One
/// definition stops drift: the OnScoreHit copy had already silently dropped the
/// IsActive() conjunct the other two shared.
/// </summary>
public static class CareerHeroIdentityGate
{
    public static bool IsCareerHeroAgent(Agent agent, Hero hero)
        => agent != null
        && hero != null
        && agent.IsActive()
        && agent.Character == hero.CharacterObject;
}
