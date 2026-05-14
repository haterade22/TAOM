using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Abilities;

// Phase 9b — extracts the inline logic that previously lived in the bodies of
// TaomAgentStatCalculateModel.UpdateAgentStats / TaomAgentApplyDamageModel.{Apply
// DamageAmplifications,ApplyDamageReductions,DecideAgentShrugOffBlow}. The model
// overrides are now thin boundaries that extract primitives from the sealed Agent /
// AttackInformation and delegate here. See `gamemodels.md` rule 4.
//
// Pragmatic acceptance per the task brief: this service intentionally reads the static
// CareerAbilityBuffTracker rather than dragging it behind an adapter. The tracker is a
// pure-data dictionary owned by TAOM (not a sealed TaleWorlds type), and the goal of
// this extraction is to get LOGIC out of the model bodies, not to eliminate the static
// tracker (out of scope, separate refactor).
public class CareerAgentStatService : ICareerAgentStatService
{
    private readonly ICareerPassiveService _passives;

    public CareerAgentStatService(ICareerPassiveService passives)
    {
        _passives = passives;
    }

    public void ApplyAgentStatModifiers(string? heroId, int agentIndex, bool isHuman, bool isHero, AgentDrivenProperties props)
    {
        if (!isHuman) return;

        if (isHero && !string.IsNullOrEmpty(heroId))
        {
            ApplyHeroPassives(heroId!, props);
            ApplyHeroSelfBuff(heroId!, props);
        }

        ApplyAllyBuff(agentIndex, props);
    }

    public float CalculateDamageAmplification(string? attackerHeroId, float baseResult)
    {
        if (string.IsNullOrEmpty(attackerHeroId)) return baseResult;

        var armorPen = _passives.GetPassiveMagnitude(attackerHeroId!, PassiveEffectType.ArmorPenetration);
        return armorPen != 0f ? baseResult * (1f + armorPen) : baseResult;
    }

    public float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, float baseResult)
    {
        var result = baseResult;

        if (!string.IsNullOrEmpty(victimHeroId))
        {
            var resistance = _passives.GetPassiveMagnitude(victimHeroId!, PassiveEffectType.Resistance);
            if (resistance != 0f) result *= (1f - resistance);

            var heroBuff = CareerAbilityBuffTracker.GetBuff(victimHeroId!);
            if (heroBuff != null && heroBuff.DamageReductionBonus != 0f)
                result *= (1f - heroBuff.DamageReductionBonus);
        }

        if (victimAgentIndex.HasValue)
        {
            var allyBuff = CareerAbilityBuffTracker.GetAllyBuff(victimAgentIndex.Value);
            if (allyBuff != null && allyBuff.DamageReductionBonus != 0f)
                result *= (1f - allyBuff.DamageReductionBonus);
        }

        return result;
    }

    public bool ShouldShrugOffBlow(string? victimHeroId)
    {
        if (string.IsNullOrEmpty(victimHeroId)) return false;
        var shrugOff = _passives.GetPassiveMagnitude(victimHeroId!, PassiveEffectType.ShruggedOff);
        return shrugOff > 0f;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // private helpers — keep ApplyAgentStatModifiers itself flat (gamemodels rule 4
    // applies to the model body; the service can decompose freely).
    // ──────────────────────────────────────────────────────────────────────────

    private void ApplyHeroPassives(string heroId, AgentDrivenProperties props)
    {
        var swingBonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.SwingSpeed);
        if (swingBonus != 0f) props.SwingSpeedMultiplier += swingBonus;

        var damageBonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.Damage);
        if (damageBonus != 0f) props.DamageMultiplierBonus += damageBonus;

        var speedBonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.MovementSpeed);
        if (speedBonus != 0f) props.MaxSpeedMultiplier += speedBonus;
    }

    private static void ApplyHeroSelfBuff(string heroId, AgentDrivenProperties props)
    {
        var buffs = CareerAbilityBuffTracker.GetBuff(heroId);
        if (buffs == null) return;

        props.MaxSpeedMultiplier += buffs.SpeedMultiplier;
        props.CombatMaxSpeedMultiplier += buffs.CombatSpeedMultiplier;
        props.DamageMultiplierBonus += buffs.DamageBonus;
        props.ArmorEncumbrance -= buffs.ArmorReduction;
        props.ThrustOrRangedReadySpeedMultiplier += buffs.DrawSpeedBonus;
        // Mount stats: multiplicative scaling — engine values are pre-normalized.
        if (buffs.MountSpeedBonus != 0f)
            props.MountSpeed *= (1f + buffs.MountSpeedBonus);
        if (buffs.ChargeDamageBonus != 0f)
            props.MountChargeDamage *= (1f + buffs.ChargeDamageBonus);
    }

    private static void ApplyAllyBuff(int agentIndex, AgentDrivenProperties props)
    {
        // AoE ally buffs — applied to ALL human agents (set by Infantry ability on nearby
        // troops). Damage-reduction half of the same buff is handled by
        // CalculateDamageReduction on the damage path.
        var allyBuffs = CareerAbilityBuffTracker.GetAllyBuff(agentIndex);
        if (allyBuffs == null) return;

        props.DamageMultiplierBonus += allyBuffs.DamageBonus;
        props.MaxSpeedMultiplier += allyBuffs.SpeedMultiplier;
        props.CombatMaxSpeedMultiplier += allyBuffs.CombatSpeedMultiplier;
        props.ThrustOrRangedReadySpeedMultiplier += allyBuffs.DrawSpeedBonus;
        if (allyBuffs.MountSpeedBonus != 0f)
            props.MountSpeed *= (1f + allyBuffs.MountSpeedBonus);
        if (allyBuffs.ChargeDamageBonus != 0f)
            props.MountChargeDamage *= (1f + allyBuffs.ChargeDamageBonus);
    }
}
