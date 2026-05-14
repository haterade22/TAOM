using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.Models;

// Phase 9b — thin boundary per gamemodels.md rule 4. All branching/passive lookup +
// active-buff logic lives in ICareerAgentStatService. This file does primitive
// extraction from `AttackInformation` and short-circuits the base shrug-off path.
// Closes deferred audit-issue #142 inline-logic P2.
public class TaomAgentApplyDamageModel : SandboxAgentApplyDamageModel
{
    private readonly ICareerAgentStatService _agentStatService;

    public TaomAgentApplyDamageModel(ICareerAgentStatService agentStatService)
    {
        _agentStatService = agentStatService;
    }

    public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var baseResult = base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);
        return _agentStatService.CalculateDamageAmplification(
            attackerHeroId: GetAttackerHeroId(in attackInformation),
            baseResult: baseResult);
    }

    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var baseResult = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);
        return _agentStatService.CalculateDamageReduction(
            victimHeroId: GetVictimHeroId(in attackInformation),
            victimAgentIndex: GetVictimAgent(in attackInformation)?.Index,
            baseResult: baseResult);
    }

    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
    {
        if (base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)) return true;
        if (!victimAgent.IsHero) return false;
        return _agentStatService.ShouldShrugOffBlow((victimAgent.Character as CharacterObject)?.HeroObject?.StringId);
    }

    // Primitive extractors — pure helpers, no branching beyond null-safe routing. Kept
    // private + static so they don't surface on the boundary.
    private static Agent GetVictimAgent(in AttackInformation info)
        => info.IsVictimAgentMount ? info.VictimAgent?.RiderAgent : info.VictimAgent;

    private static string GetAttackerHeroId(in AttackInformation info)
    {
        if (info.IsAttackerAgentNull) return null;
        var agent = info.IsAttackerAgentMount ? info.AttackerAgent?.RiderAgent : info.AttackerAgent;
        if (agent == null || !agent.IsHero) return null;
        return (agent.Character as CharacterObject)?.HeroObject?.StringId;
    }

    private static string GetVictimHeroId(in AttackInformation info)
    {
        var agent = GetVictimAgent(in info);
        if (agent == null || !agent.IsHero) return null;
        return (agent.Character as CharacterObject)?.HeroObject?.StringId;
    }
}
