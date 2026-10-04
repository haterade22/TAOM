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
    // dictionaries hold one entry per career hero, i.e. one. The per-hit lines use a third table
    // below, one entry per hit combination per mission.
    private readonly Dictionary<string, string> _lastStatLog = new Dictionary<string, string>();
    private readonly Dictionary<string, string> _lastMountLog = new Dictionary<string, string>();
    // A hero's stat update can arrive on the AI thread (a formation order writes Defensiveness)
    // while a spawn runs on the main thread; the dedupe state takes a lock like the buff tracker.
    private readonly object _logGate = new object();
    // #613 per-hit evidence: one DEBUG line per mission for each (direction, subject, hit mask, terms that fired),
    // plus a numbers-only tally of every hit of that combination, which ResetDiagnostics writes as one INFO summary
    // line each at mission end (plan 030, DECISIONS D6: the lines after the first are aggregated, never dropped).
    // The subject is a hero or party-leader id: on the reduction path the victim's hero id, else its party leader's;
    // on the amplification path the attacker's hero id, else its troop's party leader's. A reduction victim with
    // neither is known only by Agent.Index, which recycles, so those hits share the null subject. Guarded by
    // _logGate (the damage path can run off the main thread); written and cleared by ResetDiagnostics, which the mission
    // behavior calls from OnEndMission and from OnRemoveBehavior (every way a mission can end), so only a crash or a
    // process kill mid-battle keeps the first-hit DEBUG lines but loses these counts.
    private readonly Dictionary<(bool Reduction, string? Subject, AttackTypeMask Mask, int Terms), HitTally> _hitTallies = new();

    private sealed class HitTally
    {
        public int Hits;
        public int NonFinite;
        public float Min = float.MaxValue;
        public float Max = float.MinValue;
        public double MultiplierSum;
        public double BaseSum;
        public double ResultSum;
    }

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
        List<string>? summary = null;
        lock (_logGate)
        {
            _lastStatLog.Clear();
            _lastMountLog.Clear();
            if (_hitTallies.Count > 0 && _logger != null) summary = HitSummaryLines();
            _hitTallies.Clear();
        }
        if (summary == null) return;
        foreach (var line in summary) _logger!.LogInfo(line);
    }

    // Called under _logGate. Amplification lines first, then reduction; within each by subject, mask and terms, so
    // two runs of the same battle read the same.
    private List<string> HitSummaryLines()
    {
        var keys = new List<(bool Reduction, string? Subject, AttackTypeMask Mask, int Terms)>(_hitTallies.Keys);
        keys.Sort((a, b) =>
        {
            int c = a.Reduction.CompareTo(b.Reduction);
            if (c == 0) c = string.CompareOrdinal(a.Subject, b.Subject);
            if (c == 0) c = ((int)a.Mask).CompareTo((int)b.Mask);
            if (c == 0) c = a.Terms.CompareTo(b.Terms);
            return c;
        });
        int hits = 0;
        foreach (var t in _hitTallies.Values) hits += t.Hits;
        var lines = new List<string>(keys.Count + 1)
        {
            $"[CareerPerks] hit summary for this mission: {keys.Count} combination(s), {hits} hit(s)"
        };
        foreach (var k in keys)
        {
            var t = _hitTallies[k];
            int finite = t.Hits - t.NonFinite;
            var range = finite > 0
                ? $"multiplier min={Mul(t.Min)} avg={Mul((float)(t.MultiplierSum / finite))} max={Mul(t.Max)}"
                : "multiplier n/a";
            var nonFinite = t.NonFinite > 0 ? $" nonFinite={t.NonFinite}" : "";
            var damage = finite > 0 ? $"damage {Num((float)t.BaseSum)} -> {Num((float)t.ResultSum)}" : "damage n/a";
            lines.Add($"[CareerPerks] hit {(k.Reduction ? "reduction" : "amp")} summary for '{k.Subject ?? "(ally-buffed agents)"}' " +
                $"[{k.Mask}] ({TermNames(k.Reduction, k.Terms)}): hits={t.Hits} {range}{nonFinite} {damage}");
        }
        return lines;
    }

    private static string TermNames(bool reduction, int terms)
    {
        var names = reduction
            ? new[] { "Resistance", "self buff reduction", "TroopResistance", "ally buff reduction" }
            : new[] { "ArmorPenetration", "Damage", "TroopDamage" };
        var parts = new List<string>(names.Length);
        for (int i = 0; i < names.Length; i++)
            if ((terms & (1 << i)) != 0) parts.Add(names[i]);
        return string.Join(", ", parts);
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

    // Counts the hit in its combination's tally (numbers only, no string) and returns true for the combination's
    // first hit this mission, the one whose DEBUG line is written in full.
    private bool TallyHit(bool reduction, string? subject, AttackTypeMask hitMask, int terms, float multiplier, float baseResult, float result)
    {
        lock (_logGate)
        {
            var key = (reduction, subject, hitMask, terms);
            bool first = !_hitTallies.TryGetValue(key, out var tally);
            if (first) _hitTallies[key] = tally = new HitTally();
            tally!.Hits++;
            // A hit with any non-finite number is counted in NonFinite and kept out of every sum, so one bad hit
            // cannot turn the combination's totals into NaN (the summary's "finite" divisor is Hits - NonFinite).
            if (FiniteFloatValidator.IsFinite(multiplier) && FiniteFloatValidator.IsFinite(baseResult) && FiniteFloatValidator.IsFinite(result))
            {
                tally.BaseSum += baseResult;
                tally.ResultSum += result;
                tally.MultiplierSum += multiplier;
                if (multiplier < tally.Min) tally.Min = multiplier;
                if (multiplier > tally.Max) tally.Max = multiplier;
            }
            else
            {
                tally.NonFinite++;
            }
            return first;
        }
    }

    private static string AmpTerms(float armorPen, float damage, float troopDamage)
    {
        string? terms = null;
        if (armorPen != 0f) terms += " ArmorPenetration " + Pct(armorPen);
        if (damage != 0f) terms += " Damage " + Pct(damage);
        if (troopDamage != 0f) terms += " TroopDamage " + Pct(troopDamage);
        return terms!.TrimStart();
    }

    private static float ReductionMultiplier(float resistance, float selfReduction, float troopResistance, float allyReduction) =>
        (1f - resistance) * (1f - selfReduction) * (1f - troopResistance) * (1f - allyReduction);   // an unset term is 0, so 1

    private static string ReductionTerms(float resistance, float selfReduction, float troopResistance, float allyReduction)
    {
        string? terms = null;
        if (resistance != 0f) terms += " Resistance " + Pct(resistance);
        if (selfReduction != 0f) terms += " self buff reduction " + Pct(selfReduction);
        if (troopResistance != 0f) terms += " TroopResistance " + Pct(troopResistance);
        if (allyReduction != 0f) terms += " ally buff reduction " + Pct(allyReduction);
        return terms!.TrimStart();
    }

    private static string DescribeBuff(ActiveBuffs b) => ActiveBuffsFormat.Describe(b);

    private static string Pct(float magnitude) => ActiveBuffsFormat.Pct(magnitude);

    private static string Num(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Mul(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

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
    // MountChargeDamage by it; the antler attacks read it when they fire (the great elk, #636; the Animalia elk and moose, #646), so the two agree for any product
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
        float armorPen = 0f, damage = 0f, troopDamage = 0f;

        if (!string.IsNullOrEmpty(attackerHeroId))
        {
            armorPen = _passives.GetPassiveMagnitude(attackerHeroId!, PassiveEffectType.ArmorPenetration);
            if (armorPen != 0f) result *= (1f + armorPen);

            // Damage is attack-type-specific (a melee or ranged pip), so it applies here on the hit
            // path (gated by hitMask) rather than as a flat DamageMultiplierBonus.
            damage = _passives.GetMaskedMagnitude(attackerHeroId!, PassiveEffectType.Damage, hitMask);
            if (damage != 0f) result *= (1f + damage);
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
            troopDamage = _passives.GetPassiveMagnitude(attackerTroopLeaderHeroId!, PassiveEffectType.TroopDamage);
            if (troopDamage != 0f) result *= (1f + troopDamage);
        }

        // #613: the per-hit evidence, on the async DEBUG lane, once per battle for each subject, hit mask and set of
        // terms; a combination already logged formats nothing and is only counted, for the mission-end summary.
        int terms = (armorPen != 0f ? 1 : 0) | (damage != 0f ? 2 : 0) | (troopDamage != 0f ? 4 : 0);
        if (terms != 0 && _logger != null)
        {
            var subject = attackerHeroId ?? attackerTroopLeaderHeroId;
            float multiplier = (1f + armorPen) * (1f + damage) * (1f + troopDamage);   // an unset term is 0, so 1
            if (TallyHit(reduction: false, subject, hitMask, terms, multiplier, baseResult, result))
                _logger.LogDebug($"[CareerPerks] hit amp for '{subject}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({AmpTerms(armorPen, damage, troopDamage)})");
        }

        return result;
    }

    public float CalculateDamageReduction(string? victimHeroId, int? victimAgentIndex, string? troopLeaderHeroId, AttackTypeMask hitMask, float baseResult)
    {
        var result = baseResult;
        float resistance = 0f, selfReduction = 0f, troopResistance = 0f, allyReduction = 0f;

        if (!string.IsNullOrEmpty(victimHeroId))
        {
            resistance = _passives.GetMaskedMagnitude(victimHeroId!, PassiveEffectType.Resistance, hitMask);
            if (resistance != 0f) result *= (1f - resistance);

            var heroBuff = CareerAbilityBuffTracker.GetBuff(victimHeroId!);
            if (heroBuff != null && heroBuff.DamageReductionBonus != 0f)
            {
                selfReduction = heroBuff.DamageReductionBonus;
                result *= (1f - selfReduction);
            }
        }

        // TroopResistance — the victim is a non-hero troop whose party leader took the passive.
        // Mutually exclusive with the hero Resistance above (a hero victim has no troopLeaderHeroId).
        if (!string.IsNullOrEmpty(troopLeaderHeroId))
        {
            troopResistance = _passives.GetPassiveMagnitude(troopLeaderHeroId!, PassiveEffectType.TroopResistance);
            if (troopResistance != 0f) result *= (1f - troopResistance);
        }

        if (victimAgentIndex.HasValue)
        {
            var allyBuff = CareerAbilityBuffTracker.GetAllyBuff(victimAgentIndex.Value);
            if (allyBuff != null && allyBuff.DamageReductionBonus != 0f)
            {
                allyReduction = allyBuff.DamageReductionBonus;
                result *= (1f - allyReduction);
            }
        }

        int terms = (resistance != 0f ? 1 : 0) | (selfReduction != 0f ? 2 : 0)
            | (troopResistance != 0f ? 4 : 0) | (allyReduction != 0f ? 8 : 0);
        if (terms != 0 && _logger != null && TallyHit(reduction: true, victimHeroId ?? troopLeaderHeroId, hitMask, terms,
                ReductionMultiplier(resistance, selfReduction, troopResistance, allyReduction), baseResult, result))
            _logger.LogDebug($"[CareerPerks] hit reduction for '{victimHeroId ?? troopLeaderHeroId ?? victimAgentIndex?.ToString()}' [{hitMask}]: {Num(baseResult)} -> {Num(result)} ({ReductionTerms(resistance, selfReduction, troopResistance, allyReduction)})");

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
