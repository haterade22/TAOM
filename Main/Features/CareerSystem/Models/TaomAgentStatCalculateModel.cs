using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomAgentStatCalculateModel : SandboxAgentStatCalculateModel
{
    public override float GetEffectiveMaxHealth(Agent agent)
    {
        var baseHealth = base.GetEffectiveMaxHealth(agent);
        if (!agent.IsHero) return baseHealth;

        var hero = (agent.Character as CharacterObject)?.HeroObject;
        if (hero == null) return baseHealth;

        var passiveService = IoC.Resolve<ICareerPassiveService>();
        if (passiveService == null) return baseHealth;

        var healthBonus = passiveService.GetPassiveMagnitude(hero.StringId, PassiveEffectType.Health);
        return baseHealth + healthBonus;
    }

    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);

        if (!agent.IsHuman || !agent.IsHero) return;
        var hero = (agent.Character as CharacterObject)?.HeroObject;
        if (hero == null) return;

        var passiveService = IoC.Resolve<ICareerPassiveService>();
        if (passiveService == null) return;

        var heroId = hero.StringId;

        var swingBonus = passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.SwingSpeed);
        if (swingBonus != 0f)
            agentDrivenProperties.SwingSpeedMultiplier += swingBonus;

        var damageBonus = passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.Damage);
        if (damageBonus != 0f)
            agentDrivenProperties.DamageMultiplierBonus += damageBonus;

        var speedBonus = passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.MovementSpeed);
        if (speedBonus != 0f)
            agentDrivenProperties.MaxSpeedMultiplier += speedBonus;
    }
}
