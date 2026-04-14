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

        if (!agent.IsHuman || !agent.IsHero) return;
        var hero = (agent.Character as CharacterObject)?.HeroObject;
        if (hero == null) return;

        if (PassiveService == null) return;

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
        }
    }
}
