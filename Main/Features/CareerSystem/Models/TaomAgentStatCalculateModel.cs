using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.Elephant;
using TAOM.Features.Spider;

namespace TAOM.Features.CareerSystem.Models;

// Phase 9b — thin boundary per gamemodels.md rule 4. All branching/stat-mutation logic
// lives in ICareerAgentStatService. This file extracts primitives from the sealed Agent
// at the boundary and delegates. Closes deferred audit-issue #142 inline-logic P2.
//
// 2026-06-05: the shared AgentStatCalculateModel slot also carries the war-elephant mount-lock
// (1-for-1 with the upstream beasts pack's agent-stat-calculate-model) — non-rider AI can't take the elephant. The elephant
// id check is delegated to IElephantAttackService; the boundary only applies the result via ternaries.
// 2026-06-10: same lock extended to the ridden giant spider (ISpiderAttackService.IsSpiderMonster).
// 2026-06-12: the Rhûn war chariot (issue #279) deliberately has NO mount-lock — maintainer wants
// chariots remountable mid-battle (upstream-chariot-pack parity; the item's riding difficulty 120 is the only gate).
public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel
{
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerAgentStatService _agentStatService;
    private readonly IElephantAttackService _elephant;
    private readonly ISpiderAttackService _spider;

    public TaomAgentStatCalculateModel(ICareerPassiveService passiveService, ICareerAgentStatService agentStatService, IElephantAttackService elephant, ISpiderAttackService spider)
    {
        _passiveService = passiveService;
        _agentStatService = agentStatService;
        _elephant = elephant;
        _spider = spider;
    }

    public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        => _elephant.IsElephantMonster(targetMount?.Monster?.StringId) || _spider.IsSpiderMonster(targetMount?.Monster?.StringId)
            ? false
            : base.CanAgentRideMount(agent, targetMount);

    public override float GetEffectiveMaxHealth(Agent agent)
    {
        var baseHealth = base.GetEffectiveMaxHealth(agent);
        if (!agent.IsHero) return baseHealth;
        var heroId = (agent.Character as CharacterObject)?.HeroObject?.StringId;
        if (heroId == null) return baseHealth;
        return baseHealth + _passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.Health);
    }

    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);
        _agentStatService.ApplyAgentStatModifiers(
            heroId: (agent.Character as CharacterObject)?.HeroObject?.StringId,
            agentIndex: agent.Index,
            isHuman: agent.IsHuman,
            isHero: agent.IsHero,
            agentDrivenProperties);

        // Creature mount-lock (1-for-1 with the upstream beasts pack): a near-infinite MountDifficulty so non-rider AI can't take it.
        agentDrivenProperties.MountDifficulty = _elephant.IsElephantMonster(agent?.Monster?.StringId)
            ? ElephantConfig.MountDifficulty
            : _spider.IsSpiderMonster(agent?.Monster?.StringId)
                ? SpiderConfig.MountDifficulty
                : agentDrivenProperties.MountDifficulty;
    }
}
