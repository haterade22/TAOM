using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Hooks;
using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Features.Spider;
using TaleWorlds.Core;

namespace TAOM.Features.CareerSystem.Models;

// Phase 9b — thin boundary per gamemodels.md rule 4. All branching/stat-mutation logic
// lives in ICareerAgentStatService. This file extracts primitives from the sealed Agent
// at the boundary and delegates. Closes deferred audit-issue #142 inline-logic P2.
//
// This one AgentStatCalculateModel slot carries the career, mount-lock, culture aggression, cavalry charge, race ability
// and creature siege detachment rules; their order and history are in the TaomAgentStatCalculateModel row of
// docs/reference/gamemodel-registry.md. The Rhun war chariot has no mount-lock (upstream-chariot-pack parity, #279).
public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel
{
    private readonly ICareerAgentStatService _agentStatService;
    private readonly IElephantAttackService _elephant;
    private readonly ISpiderAttackService _spider;
    private readonly IMumakilAttackService _mumakil;
    private readonly ICultureAggressionService? _aggression;
    private readonly IChargeDamageService? _chargeDamage;
    private readonly IModLogger? _logger;

    public TaomAgentStatCalculateModel(ICareerAgentStatService agentStatService, IElephantAttackService elephant, ISpiderAttackService spider, IMumakilAttackService mumakil)
        : this(agentStatService, elephant, spider, mumakil, null, null, null)
    {
    }

    public TaomAgentStatCalculateModel(ICareerAgentStatService agentStatService, IElephantAttackService elephant, ISpiderAttackService spider, IMumakilAttackService mumakil, ICultureAggressionService? aggression, IChargeDamageService? chargeDamage = null, IModLogger? logger = null)
    {
        _agentStatService = agentStatService;
        _elephant = elephant;
        _spider = spider;
        _mumakil = mumakil;
        _aggression = aggression;
        _chargeDamage = chargeDamage;
        _logger = logger;
    }

    // The Ammo passive (#613) rides the seam vanilla's own extra-ammo perks use: before the agent is
    // built, raising the stack's max with the amount. Heroes only; CareerAmmoApplier has the evidence.
    public override void InitializeMissionEquipment(Agent agent)
    {
        base.InitializeMissionEquipment(agent);
        if (agent != null && agent.IsHero)
        {
            var heroId = (agent.Character as CharacterObject)?.HeroObject?.StringId;
            CareerAmmoApplier.Apply(agent, heroId, _agentStatService.AmmoBonus(heroId), _logger);
        }
    }

    public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        => _elephant.IsCreatureMonster(targetMount?.Monster?.StringId)
            || _spider.IsSpiderMonster(targetMount?.Monster?.StringId)
            || _mumakil.IsCreatureMonster(targetMount?.Monster?.StringId)
            ? false
            : base.CanAgentRideMount(agent, targetMount);

    // Race Abilities: a live ability's knockdown, knock-back and dismount resistance. The engine floors a
    // soldier when the hit beats HealthLimit x (resistance - penetration) (MissionCombatMechanicsHelper).
    public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid)
        => RaceAbilityHooks.KnockDownResistance(agent, base.GetKnockDownResistance(agent, strikeType));

    public override float GetKnockBackResistance(Agent agent)
        => RaceAbilityHooks.KnockBackResistance(agent, base.GetKnockBackResistance(agent));

    public override float GetDismountResistance(Agent agent)
        => RaceAbilityHooks.DismountResistance(agent, base.GetDismountResistance(agent));

    // Creature Siege Role: +Infinity for a creature of an active wall battle, base (a banner bearer's 10, else 1) for every other
    // agent. The engine reads this from its asynchronous AI thread, so the hook is a lock-free read of one snapshot.
    public override float GetDetachmentCostMultiplierOfAgent(Agent agent, IDetachment detachment)
        => CreatureSiegeHooks.DetachmentCost(agent, base.GetDetachmentCostMultiplierOfAgent(agent, detachment));

    public override float GetEffectiveMaxHealth(Agent agent)
    {
        var baseHealth = base.GetEffectiveMaxHealth(agent);
        // Mount whose rider is a hero → multiplicative MountHealth. The hero's own Health passive is
        // NOT applied here (#394) — base's hero branch returns agent.Character.MaxHitPoints(), which
        // already carries it via TaomCharacterStatsModel. Adding it again doubles the pip in battle.
        // Primitive extraction only; the decision lives in the service (gamemodels.md rule 4).
        var rider = agent.IsMount ? agent.RiderAgent : null;
        var riderHeroId = (rider != null && rider.IsHero)
            ? (rider.Character as CharacterObject)?.HeroObject?.StringId
            : null;
        return _agentStatService.ApplyMountHealthPassives(riderHeroId, baseHealth);
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

        // Creature mount-lock (1-for-1 with ADOD_Beasts): a near-infinite MountDifficulty so non-rider AI can't take it.
        agentDrivenProperties.MountDifficulty = _elephant.IsCreatureMonster(agent?.Monster?.StringId)
            ? ElephantConfig.MountDifficulty
            : _spider.IsSpiderMonster(agent?.Monster?.StringId)
                ? SpiderConfig.MountDifficulty
                : _mumakil.IsCreatureMonster(agent?.Monster?.StringId)
                    ? MumakilConfig.MountDifficulty
                    : agentDrivenProperties.MountDifficulty;

        if (_aggression != null)
            AgentAggressionApplier.Apply(agentDrivenProperties, _aggression.Profile(AgentAggressionApplier.CultureOf(agent)));

        // Race Abilities: a live ability's speeds and AI temperament, after the aggression pass so it scales
        // the culture's values; inert unless the soldier's ability is running.
        RaceAbilityHooks.ApplyStats(agent, agentDrivenProperties);

        // Mount-side career bonuses (#611) and the culture charge multiplier (#610) both ride the
        // MOUNT's properties, after base rewrote them; the rider is the identity (a mount's own
        // Character is null). Non-mounts pass two nulls and the service returns at once.
        var mountRider = agent.IsMount ? agent.RiderAgent : null;
        _agentStatService.ApplyMountStatModifiers(
            riderHeroId: (mountRider != null && mountRider.IsHero) ? (mountRider.Character as CharacterObject)?.HeroObject?.StringId : null,
            riderAgentIndex: mountRider?.Index,
            agentDrivenProperties);

        if (_chargeDamage != null)
            MountChargeDamageApplier.Apply(agent, agentDrivenProperties, _chargeDamage);
    }
}
