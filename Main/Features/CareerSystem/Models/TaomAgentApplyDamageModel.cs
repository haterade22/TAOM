using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

// Phase 9b — thin boundary per gamemodels.md rule 4. All branching/passive lookup +
// active-buff logic lives in ICareerAgentStatService. This file does primitive
// extraction from `AttackInformation` and short-circuits the base shrug-off path.
// Closes deferred audit-issue #142 inline-logic P2.
// Abstract since 2026-07-02: registered only via a derived model (TaomCombatMechanicsModel) —
// the engine holds ONE AgentApplyDamageModel slot, so combat-mechanics overrides layer on top
// of the career passives through inheritance.
public abstract class TaomAgentApplyDamageModel : SandboxAgentApplyDamageModel
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
            hitMask: HitMask(in collisionData),
            baseResult: baseResult);
    }

    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var baseResult = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);
        return _agentStatService.CalculateDamageReduction(
            victimHeroId: GetVictimHeroId(in attackInformation),
            victimAgentIndex: GetVictimAgent(in attackInformation)?.Index,
            troopLeaderHeroId: GetVictimTroopLeaderHeroId(in attackInformation),
            hitMask: HitMask(in collisionData),
            baseResult: baseResult);
    }

    // A missile hit is Ranged; everything else (swing/thrust/charge) is treated as Melee. Drives
    // the attack_type_mask gate on the Damage (attacker) + Resistance (victim) career passives.
    private static AttackTypeMask HitMask(in AttackCollisionData collisionData)
        => collisionData.IsMissile ? AttackTypeMask.Ranged : AttackTypeMask.Melee;

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

    // TroopResistance applies to the victim's party LEADER's non-hero troops. A hero victim is
    // covered by Resistance instead, so return null for heroes. Custom-battle agents use a
    // non-PartyBase origin so the cast yields null and falls through to base.
    private static string GetVictimTroopLeaderHeroId(in AttackInformation info)
    {
        if (info.IsVictimAgentNull) return null;
        var agent = GetVictimAgent(in info);
        if (agent == null || agent.IsHero) return null;
        // GetVictimAgent returns the RIDER for a mount hit, so the leader must resolve off the
        // rider's origin — the struck mount's own Origin is null (horses spawn without one), which
        // would otherwise silently drop TroopResistance on every horse-body hit of cavalry.
        var origin = info.IsVictimAgentMount ? info.VictimRiderAgentOrigin : info.VictimAgentOrigin;
        return (origin?.BattleCombatant as PartyBase)?.LeaderHero?.StringId;
    }
}
