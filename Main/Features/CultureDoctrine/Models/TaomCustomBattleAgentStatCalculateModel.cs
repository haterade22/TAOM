using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Features.CultureDoctrine.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Models;

/// <summary>
/// Custom Battle: the culture's aggression profile as a post-pass on
/// <c>CustomBattleAgentStatCalculateModel.UpdateAgentStats</c>. The campaign's slot is
/// <c>TaomAgentStatCalculateModel</c> (CareerSystem), which carries the same post-pass beside
/// the career and creature rules; this one exists so the Custom Battle A/B measures the melee
/// tier too. It also carries the per-culture mount charge multiplier (#610), so a Custom Battle
/// cavalry smoke sees the same numbers a campaign battle does.
///
/// The multiplier rides <c>InitializeAgentStats</c>, not <c>UpdateAgentStats</c>: the Custom
/// Battle base writes <c>MountChargeDamage</c> once, at spawn, in <c>InitializeAgentStats</c>
/// (v1.5.3 <c>CustomBattleAgentStatCalculateModel.cs:48</c>) and its <c>UpdateHorseStats</c>
/// never rewrites it, so a multiply on every update would compound. The Sandbox model rewrites
/// the property on every call (<c>SandboxAgentStatCalculateModel.cs:1280</c>), which is why the
/// campaign model multiplies from <c>UpdateAgentStats</c> instead. Consequence here: the factor
/// follows the rider the horse spawned with; a mid-battle remount by another culture keeps it.
/// </summary>
public class TaomCustomBattleAgentStatCalculateModel : CustomBattleAgentStatCalculateModel
{
    private readonly ICultureAggressionService _aggression;
    private readonly IChargeDamageService? _chargeDamage;

    public TaomCustomBattleAgentStatCalculateModel(ICultureAggressionService aggression, IChargeDamageService? chargeDamage = null)
    {
        _aggression = aggression;
        _chargeDamage = chargeDamage;
    }

    public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
    {
        base.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);
        if (_chargeDamage != null)
            MountChargeDamageApplier.Apply(agent, agentDrivenProperties, _chargeDamage);
    }

    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);
        AgentAggressionApplier.Apply(agentDrivenProperties, _aggression.Profile(AgentAggressionApplier.CultureOf(agent)));
        // Race Abilities: the same post-pass as the campaign slot, so a Custom Battle shows the live ability.
        RaceAbilityHooks.ApplyStats(agent, agentDrivenProperties);
    }

    // Race Abilities: a live ability's knockdown, knock-back and dismount resistance, as in the campaign slot.
    public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid)
        => RaceAbilityHooks.KnockDownResistance(agent, base.GetKnockDownResistance(agent, strikeType));

    public override float GetKnockBackResistance(Agent agent)
        => RaceAbilityHooks.KnockBackResistance(agent, base.GetKnockBackResistance(agent));

    public override float GetDismountResistance(Agent agent)
        => RaceAbilityHooks.DismountResistance(agent, base.GetDismountResistance(agent));

    // Creature Bandits (#692): a riderless creature is nobody's mount. AI soldiers look for loose mounts to ride and
    // ask this first (v1.5.3 HumanAIComponent.cs:302). The campaign slot locks creatures by Monster
    // (TaomAgentStatCalculateModel); this slot had no lock, and the creature spikes run in Custom Battle.
    public override bool CanAgentRideMount(Agent agent, Agent targetMount)
        => !CreatureBanditAgents.RefusesRider(targetMount) && base.CanAgentRideMount(agent, targetMount);
}
