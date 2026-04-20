using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel
{
    private ICareerPassiveService _passiveService;
    private ICareerPassiveService PassiveService =>
        _passiveService ??= IoC.Resolve<ICareerPassiveService>();

    public override float GetEffectiveMaxHealth(Agent agent)
    {
        var baseHealth = base.GetEffectiveMaxHealth(agent);
        if (!agent.IsHero) return baseHealth;

        var hero = (agent.Character as CharacterObject)?.HeroObject;
        if (hero == null) return baseHealth;

        if (PassiveService == null) return baseHealth;

        var healthBonus = PassiveService.GetPassiveMagnitude(hero.StringId, PassiveEffectType.Health);
        return baseHealth + healthBonus;
    }

    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);

        if (!agent.IsHuman) return;

        // Hero-specific: passive service bonuses + active ability buffs
        if (agent.IsHero)
        {
            var hero = (agent.Character as CharacterObject)?.HeroObject;
            if (hero != null && PassiveService != null)
            {
                var heroId = hero.StringId;

                var swingBonus = PassiveService.GetPassiveMagnitude(heroId, PassiveEffectType.SwingSpeed);
                if (swingBonus != 0f)
                    agentDrivenProperties.SwingSpeedMultiplier += swingBonus;

                var damageBonus = PassiveService.GetPassiveMagnitude(heroId, PassiveEffectType.Damage);
                if (damageBonus != 0f)
                    agentDrivenProperties.DamageMultiplierBonus += damageBonus;

                var speedBonus = PassiveService.GetPassiveMagnitude(heroId, PassiveEffectType.MovementSpeed);
                if (speedBonus != 0f)
                    agentDrivenProperties.MaxSpeedMultiplier += speedBonus;

                // Apply active ability buffs AFTER base recalc so they are not overwritten.
                var buffs = CareerAbilityBuffTracker.GetBuff(heroId);
                if (buffs != null)
                {
                    agentDrivenProperties.MaxSpeedMultiplier += buffs.SpeedMultiplier;
                    agentDrivenProperties.CombatMaxSpeedMultiplier += buffs.CombatSpeedMultiplier;
                    agentDrivenProperties.DamageMultiplierBonus += buffs.DamageBonus;
                    agentDrivenProperties.ArmorEncumbrance -= buffs.ArmorReduction;
                    agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += buffs.DrawSpeedBonus;
                    // Mount stats: multiplicative scaling — engine values are pre-normalized
                    if (buffs.MountSpeedBonus != 0f)
                        agentDrivenProperties.MountSpeed *= (1f + buffs.MountSpeedBonus);
                    if (buffs.ChargeDamageBonus != 0f)
                        agentDrivenProperties.MountChargeDamage *= (1f + buffs.ChargeDamageBonus);
                }
            }
        }

        // AoE ally buffs — applied to ALL human agents (set by Infantry ability on nearby troops)
        // AoE ally buffs — all archetypes can apply these on nearby troops
        // (damage reduction is handled by TaomAgentApplyDamageModel's damage path)
        var allyBuffs = CareerAbilityBuffTracker.GetAllyBuff(agent.Index);
        if (allyBuffs != null)
        {
            agentDrivenProperties.DamageMultiplierBonus += allyBuffs.DamageBonus;
            agentDrivenProperties.MaxSpeedMultiplier += allyBuffs.SpeedMultiplier;
            agentDrivenProperties.CombatMaxSpeedMultiplier += allyBuffs.CombatSpeedMultiplier;
            agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += allyBuffs.DrawSpeedBonus;
            if (allyBuffs.MountSpeedBonus != 0f)
                agentDrivenProperties.MountSpeed *= (1f + allyBuffs.MountSpeedBonus);
            if (allyBuffs.ChargeDamageBonus != 0f)
                agentDrivenProperties.MountChargeDamage *= (1f + allyBuffs.ChargeDamageBonus);
        }
    }
}
