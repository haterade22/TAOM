using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
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
    private readonly IModLogger? _logger;

    // #613 [CareerPerks] diagnostics: the hero's own stat application is logged once per distinct
    // set of applied values (a stat update runs per spawn, mount change, weapon change and every
    // arrow shot; the values only change when a buff lands or ends). Keyed by hero id; the two
    // dictionaries hold one entry per career hero, i.e. one.
    private readonly Dictionary<string, string> _lastStatLog = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _lastMountLog = new Dictionary<string, string>();
    // A hero's stat update can arrive on the AI thread (a formation order writes Defensiveness)
    // while a spawn runs on the main thread; the dedupe state takes a lock like the buff tracker.
    private readonly object _logGate = new object();

    public CareerAgentStatService(ICareerPassiveService passives, IModLogger? logger = null)
    {
        _passives = passives;
        _logger = logger;
    }

    public void ApplyAgentStatModifiers(string? heroId, int agentIndex, bool isHuman, bool isHero, AgentDrivenProperties props)
    {
        if (!isHuman) return;

        if (isHero && !string.IsNullOrEmpty(heroId))
        {
            ApplyHeroPassives(heroId!, props);
            ApplyHeroSelfBuff(heroId!, props);
            LogStatApplication(heroId!, agentIndex);
        }

        ApplyAllyBuff(agentIndex, props);
    }

    private void LogStatApplication(string heroId, int agentIndex)
    {
        if (_logger == null) return;
        var parts = new List<string>(4);
        var swing = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.SwingSpeed);
        if (swing != 0f) parts.Add("SwingSpeed " + Pct(swing));
        var speed = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.MovementSpeed);
        if (speed != 0f) parts.Add("MovementSpeed " + Pct(speed));
        var self = CareerAbilityBuffTracker.GetBuff(heroId);
        if (self != null) parts.Add("self buff " + DescribeBuff(self));
        var ally = CareerAbilityBuffTracker.GetAllyBuff(agentIndex);
        if (ally != null) parts.Add("ally buff " + DescribeBuff(ally));
        LogOnChange(_lastStatLog, heroId, parts.Count == 0 ? "none" : string.Join(", ", parts),
            $"[CareerPerks] agent stats for '{heroId}': ");
    }

    public void ResetDiagnostics()
    {
        lock (_logGate)
        {
            _lastStatLog.Clear();
            _lastMountLog.Clear();
        }
    }

    private void LogOnChange(Dictionary<string, string> last, string heroId, string signature, string prefix)
    {
        lock (_logGate)
        {
            if (last.TryGetValue(heroId, out var previous) && previous == signature) return;
            last[heroId] = signature;
        }
        _logger!.LogInfo(prefix + signature);
    }

    private static string DescribeBuff(ActiveBuffs b) => ActiveBuffsFormat.Describe(b);

    private static string Pct(float magnitude) => ActiveBuffsFormat.Pct(magnitude);

    private static string Num(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    // #394 — the hero `Health` passive is deliberately ABSENT here. It is applied campaign-side by
    // TaomCharacterStatsModel.MaxHitpoints, and SandboxAgentStatCalculateModel.GetEffectiveMaxHealth
    // starts with `if (agent.IsHero) return agent.Character.MaxHitPoints();` — which routes through
    // that same model. Re-adding it on this path double-counts (a +75 pip becomes +150 in battle).
    // Mounts have no CharacterStatsModel route, so MountHealth stays here.
    public float ApplyMountHealthPassives(string? mountRiderHeroId, float baseHealth)
    {
        if (string.IsNullOrEmpty(mountRiderHeroId)) return baseHealth;

        var mountHealth = _passives.GetPassiveMagnitude(mountRiderHeroId!, PassiveEffectType.MountHealth);
        return mountHealth != 0f ? baseHealth * (1f + mountHealth) : baseHealth;
    }

    // #611: the mount-side bonuses. Until 2026-09-17 these three multiplies sat on the HUMAN path
    // (ApplyHeroPassives / ApplyHeroSelfBuff / ApplyAllyBuff) and scaled the rider's own copies of
    // MountChargeDamage and MountSpeed, which no base model writes and no engine path reads; the
    // horse's copies are the ones that matter. The rider's ids come from the mount's RiderAgent.
    public void ApplyMountStatModifiers(string? riderHeroId, int? riderAgentIndex, AgentDrivenProperties mountProps)
    {
        var charge = MountChargeMultiplier(riderHeroId, riderAgentIndex);
        if (charge != 1f) mountProps.MountChargeDamage *= charge;

        if (!string.IsNullOrEmpty(riderHeroId))
            ApplyMountSpeedBuff(CareerAbilityBuffTracker.GetBuff(riderHeroId!), mountProps);

        if (riderAgentIndex.HasValue)
            ApplyMountSpeedBuff(CareerAbilityBuffTracker.GetAllyBuff(riderAgentIndex.Value), mountProps);

        if (_logger != null && !string.IsNullOrEmpty(riderHeroId))
            LogMountApplication(riderHeroId!, riderAgentIndex);
    }

    // The charge half of the mount bonuses as one product. ApplyMountStatModifiers scales the mount's
    // MountChargeDamage by it; the elk's antler charge reads it when it fires (#636), so the two agree for any product
    // the antler accepts, (0, 10] (outside it the antler lands unscaled while the mount still applies it).
    public float MountChargeMultiplier(string? riderHeroId, int? riderAgentIndex)
    {
        var multiplier = 1f;
        if (!string.IsNullOrEmpty(riderHeroId))
        {
            var passive = _passives.GetPassiveMagnitude(riderHeroId!, PassiveEffectType.MountChargeDamage);
            if (passive != 0f) multiplier *= 1f + passive;

            var self = CareerAbilityBuffTracker.GetBuff(riderHeroId!);
            if (self != null && self.ChargeDamageBonus != 0f) multiplier *= 1f + self.ChargeDamageBonus;
        }

        if (riderAgentIndex.HasValue)
        {
            var ally = CareerAbilityBuffTracker.GetAllyBuff(riderAgentIndex.Value);
            if (ally != null && ally.ChargeDamageBonus != 0f) multiplier *= 1f + ally.ChargeDamageBonus;
        }

        // The loader already rejects NaN and infinity (CareerConfigProvider.ParseFloat), but huge finite magnitudes can
        // still overflow the product; a non-finite product scales nothing, for either consumer.
        return FiniteFloatValidator.IsFinite(multiplier) ? multiplier : 1f;
    }

    private void LogMountApplication(string riderHeroId, int? riderAgentIndex)
    {
        var parts = new List<string>(3);
        var charge = _passives.GetPassiveMagnitude(riderHeroId, PassiveEffectType.MountChargeDamage);
        if (charge != 0f) parts.Add("MountChargeDamage " + Pct(charge));
        var self = CareerAbilityBuffTracker.GetBuff(riderHeroId);
        if (self != null && (self.MountSpeedBonus != 0f || self.ChargeDamageBonus != 0f)) parts.Add("self buff " + DescribeBuff(self));
        var ally = riderAgentIndex.HasValue ? CareerAbilityBuffTracker.GetAllyBuff(riderAgentIndex.Value) : null;
        if (ally != null && (ally.MountSpeedBonus != 0f || ally.ChargeDamageBonus != 0f)) parts.Add("ally buff " + DescribeBuff(ally));
        LogOnChange(_lastMountLog, riderHeroId, parts.Count == 0 ? "none" : string.Join(", ", parts),
            $"[CareerPerks] mount stats for rider '{riderHeroId}': ");
    }

    public float AmmoBonus(string? heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return 0f;
        var bonus = _passives.GetPassiveMagnitude(heroId!, PassiveEffectType.Ammo);
        return bonus > 0f ? bonus : 0f;
    }

    public float CalculateDamageAmplification(string? attackerHeroId, string? attackerTroopLeaderHeroId, AttackTypeMask hitMask, float baseResult)
    {
        var result = baseResult;
        string? terms = null;

        if (!string.IsNullOrEmpty(attackerHeroId))
        {
            var armorPen = _passives.GetPassiveMagnitude(attackerHeroId!, PassiveEffectType.ArmorPenetration);
            if (armorPen != 0f) { result *= (1f + armorPen); terms += " ArmorPenetration " + Pct(armorPen); }

            // Damage is attack-type-specific (a melee or ranged pip), so it applies here on the hit
            // path (gated by hitMask) rather than as a flat DamageMultiplierBonus.
            var damage = _passives.GetMaskedMagnitude(attackerHeroId!, PassiveEffectType.Damage, hitMask);
            if (damage != 0f) { result *= (1f + damage); terms += " Damage " + Pct(damage); }
        }

        // TroopDamage — the attacker is a non-hero troop whose party leader took the passive. The
        // exact mirror of TroopResistance on the reduction path, and mutually exclusive with the
        // hero Damage above (the boundary returns null here for a hero attacker). NOT mask-gated:
        // the shipped pips carry no attack_type_mask, so it is a flat army-wide multiplier.
        //
        // #395 — before this, TroopDamage's only consumer was TaomRaidModel.CalculateHitDamage,
        // i.e. how fast a village burns, so 105 pips promising "+N% troop damage" were inert in
        // every battle. The raid consumer is deliberately KEPT; these are different systems, not a
        // double-count.
        if (!string.IsNullOrEmpty(attackerTroopLeaderHeroId))
        {
            var troopDamage = _passives.GetPassiveMagnitude(attackerTroopLeaderHeroId!, PassiveEffectType.TroopDamage);
            if (troopDamage != 0f) { result *= (1f + troopDamage); terms += " TroopDamage " + Pct(troopDamage); }
        }

        // #613: the per-hit evidence, on the async DEBUG lane; only when a passive moved the number.
        if (terms != null && _logger != null)
            _logger.LogDebug($"[CareerPerks] hit amp for '{attackerHeroId ?? attackerTroopLeaderHeroId}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({terms.TrimStart()})");

        return result;
    }

    public float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, string? troopLeaderHeroId, AttackTypeMask hitMask, float baseResult)
    {
        var result = baseResult;
        string? terms = null;

        if (!string.IsNullOrEmpty(victimHeroId))
        {
            var resistance = _passives.GetMaskedMagnitude(victimHeroId!, PassiveEffectType.Resistance, hitMask);
            if (resistance != 0f) { result *= (1f - resistance); terms += " Resistance " + Pct(resistance); }

            var heroBuff = CareerAbilityBuffTracker.GetBuff(victimHeroId!);
            if (heroBuff != null && heroBuff.DamageReductionBonus != 0f)
            {
                result *= (1f - heroBuff.DamageReductionBonus);
                terms += " self buff reduction " + Pct(heroBuff.DamageReductionBonus);
            }
        }

        // TroopResistance — the victim is a non-hero troop whose party leader took the passive.
        // Mutually exclusive with the hero Resistance above (a hero victim has no troopLeaderHeroId).
        if (!string.IsNullOrEmpty(troopLeaderHeroId))
        {
            var troopResistance = _passives.GetPassiveMagnitude(troopLeaderHeroId!, PassiveEffectType.TroopResistance);
            if (troopResistance != 0f) { result *= (1f - troopResistance); terms += " TroopResistance " + Pct(troopResistance); }
        }

        if (victimAgentIndex.HasValue)
        {
            var allyBuff = CareerAbilityBuffTracker.GetAllyBuff(victimAgentIndex.Value);
            if (allyBuff != null && allyBuff.DamageReductionBonus != 0f)
            {
                result *= (1f - allyBuff.DamageReductionBonus);
                terms += " ally buff reduction " + Pct(allyBuff.DamageReductionBonus);
            }
        }

        if (terms != null && _logger != null)
            _logger.LogDebug($"[CareerPerks] hit reduction for '{victimHeroId ?? troopLeaderHeroId ?? victimAgentIndex?.ToString()}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({terms.TrimStart()})");

        return result;
    }

    public bool ShouldShrugOffBlow(string? victimHeroId)
    {
        if (string.IsNullOrEmpty(victimHeroId)) return false;
        var shrugOff = _passives.GetPassiveMagnitude(victimHeroId!, PassiveEffectType.ShrugOff);
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

        // NOTE: the Damage passive is NOT applied here. It is attack-type-specific (a melee or
        // ranged pip via attack_type_mask) and so is applied on the per-hit damage path
        // (CalculateDamageAmplification), where the hit's delivery type is known. Applying it as a
        // flat DamageMultiplierBonus here would ignore the mask and boost every hit.

        var speedBonus = _passives.GetPassiveMagnitude(heroId, PassiveEffectType.MovementSpeed);
        if (speedBonus != 0f) props.MaxSpeedMultiplier += speedBonus;

        // The MountChargeDamage passive is a MOUNT property: ApplyMountStatModifiers (#611).
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
        // MountSpeedBonus / ChargeDamageBonus are MOUNT properties: ApplyMountStatModifiers (#611).
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
        // MountSpeedBonus / ChargeDamageBonus are MOUNT properties: ApplyMountStatModifiers (#611).
    }

    // Mount speed: multiplicative scaling, the engine values are pre-normalized. The buff's charge half is
    // MountChargeMultiplier's.
    private static void ApplyMountSpeedBuff(ActiveBuffs? buffs, AgentDrivenProperties mountProps)
    {
        if (buffs == null) return;
        if (buffs.MountSpeedBonus != 0f)
            mountProps.MountSpeed *= (1f + buffs.MountSpeedBonus);
    }
}
