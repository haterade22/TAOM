using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;
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
// 2026-06-29: same lock extended to the ridden Mûmakil (IMumakilAttackService.IsMumakilMonster) — scaled-up elephant.
public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel
{
    private readonly ICareerAgentStatService _agentStatService;
    private readonly IElephantAttackService _elephant;
    private readonly ISpiderAttackService _spider;
    private readonly IMumakilAttackService _mumakil;

    public TaomAgentStatCalculateModel(ICareerAgentStatService agentStatService, IElephantAttackService elephant, ISpiderAttackService spider, IMumakilAttackService mumakil)
    {
        _agentStatService = agentStatService;
        _elephant = elephant;
        _spider = spider;
        _mumakil = mumakil;
    }

    public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        => _elephant.IsElephantMonster(targetMount?.Monster?.StringId)
            || _spider.IsSpiderMonster(targetMount?.Monster?.StringId)
            || _mumakil.IsMumakilMonster(targetMount?.Monster?.StringId)
            ? false
            : base.CanAgentRideMount(agent, targetMount);

    public override float GetEffectiveMaxHealth(Agent agent)
    {
        var baseHealth = base.GetEffectiveMaxHealth(agent);
        // Hero → flat Health passive; mount whose rider is a hero → multiplicative HorseHealth.
        // Primitive extraction only; the decision lives in the service (gamemodels.md rule 4).
        var heroId = agent.IsHero ? (agent.Character as CharacterObject)?.HeroObject?.StringId : null;
        var rider = agent.IsMount ? agent.RiderAgent : null;
        var riderHeroId = (rider != null && rider.IsHero)
            ? (rider.Character as CharacterObject)?.HeroObject?.StringId
            : null;
        return _agentStatService.ApplyMaxHealthPassives(heroId, riderHeroId, baseHealth);
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
                : _mumakil.IsMumakilMonster(agent?.Monster?.StringId)
                    ? MumakilConfig.MountDifficulty
                    : agentDrivenProperties.MountDifficulty;
    }
}
