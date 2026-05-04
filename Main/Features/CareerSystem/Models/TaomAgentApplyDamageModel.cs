using SandBox.GameComponents;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

public class TaomAgentApplyDamageModel : SandboxAgentApplyDamageModel
{
    private readonly ICareerPassiveService _passiveService;

    public TaomAgentApplyDamageModel(ICareerPassiveService passiveService)
    {
        _passiveService = passiveService;
    }

    public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);

        var heroId = GetAttackerHeroId(in attackInformation);
        if (heroId == null) return result;
        if (_passiveService == null) return result;

        var armorPen = _passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.ArmorPenetration);
        if (armorPen != 0f)
            result *= (1f + armorPen);

        return result;
    }

    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        // Hero passive resistance
        var heroId = GetVictimHeroId(in attackInformation);
        if (heroId != null)
        {
            if (_passiveService != null)
            {
                var resistance = _passiveService.GetPassiveMagnitude(heroId, PassiveEffectType.Resistance);
                if (resistance != 0f)
                    result *= (1f - resistance);
            }

            // Hero active ability damage reduction (Infantry self-buff)
            var heroBuff = CareerAbilityBuffTracker.GetBuff(heroId);
            if (heroBuff != null && heroBuff.DamageReductionBonus != 0f)
                result *= (1f - heroBuff.DamageReductionBonus);
        }

        // Ally active ability damage reduction (Infantry AoE buff on non-hero troops)
        var victimAgent = GetVictimAgent(in attackInformation);
        if (victimAgent != null)
        {
            var allyBuff = CareerAbilityBuffTracker.GetAllyBuff(victimAgent.Index);
            if (allyBuff != null && allyBuff.DamageReductionBonus != 0f)
                result *= (1f - allyBuff.DamageReductionBonus);
        }

        return result;
    }

    private static Agent GetVictimAgent(in AttackInformation info)
    {
        return info.IsVictimAgentMount ? info.VictimAgent?.RiderAgent : info.VictimAgent;
    }

    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
    {
        var baseResult = base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow);
        if (baseResult) return true;

        if (!victimAgent.IsHero) return false;
        var hero = (victimAgent.Character as CharacterObject)?.HeroObject;
        if (hero == null) return false;
        if (_passiveService == null) return false;

        var shrugOff = _passiveService.GetPassiveMagnitude(hero.StringId, PassiveEffectType.ShruggedOff);
        return shrugOff > 0f;
    }

    private static string GetAttackerHeroId(in AttackInformation info)
    {
        if (info.IsAttackerAgentNull) return null;
        var agent = info.IsAttackerAgentMount ? info.AttackerAgent?.RiderAgent : info.AttackerAgent;
        if (agent == null || !agent.IsHero) return null;
        return (agent.Character as CharacterObject)?.HeroObject?.StringId;
    }

    private static string GetVictimHeroId(in AttackInformation info)
    {
        var agent = info.IsVictimAgentMount ? info.VictimAgent?.RiderAgent : info.VictimAgent;
        if (agent == null || !agent.IsHero) return null;
        return (agent.Character as CharacterObject)?.HeroObject?.StringId;
    }
}
