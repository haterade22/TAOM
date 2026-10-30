using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

// Source of truth for "which PassiveEffectType values actually do something at runtime."
//
// A career pip whose PassiveEffect type is NOT in this set is a PHANTOM bonus: the UI shows
// it, the player selects it, CareerPassiveService caches its magnitude — and nothing ever
// reads that magnitude, so the pip is inert. Six such types (MountChargeDamage / MountHealth /
// StealthBonus / TroopResistance / Ammo / HeroHealing) shipped as phantoms across ~211
// pips before they were wired up; this set + the CareerConfigProvider load-time warning + the
// CareerChoicesIntegrationTests "no phantom in shipped XML" regression guard exist so a new
// phantom can never ship silently again. (csharp-architecture.md "Config Providers MUST Validate".)
//
// When you add a consumer for a new PassiveEffectType, add it here. When you remove the last
// consumer for a type, remove it here — the regression test will then flag any shipped pip
// still using it.
//
// KNOWN BLIND SPOT (#394, #395, #611): this set answers "is anything reading it", NOT "is it read where
// the player expects it". #611 was the third instance: MountChargeDamage was multiplied on the RIDER's
// driven properties, which no base model writes and the engine never reads (the horse's copy is the
// one AttackInformation takes), so a listed, "consumed" type was a phantom for three months. Membership here is necessary, never sufficient, and it has now hidden the
// same bug TWICE while staying green:
//
//   #394 Health      — mission only. Worked in battle; invisible and inert on the campaign layer
//                      (character screen, Hero.MaxHitPoints, daily heal cap).
//   #395 TroopDamage — campaign only, and the wrong campaign system: its sole consumer was
//                      TaomRaidModel.CalculateHitDamage, i.e. how fast a VILLAGE BURNS. 105 pips
//                      promising "+N% troop damage" did nothing in any battle.
//
// The real test is the pip's WORDING against the consumer's SCOPE. When you add a type here, name
// the consumer AND ask what the description promises the player — if it implies both {mission,
// campaign}, wire both; if the consumer governs a narrower system than the text claims, the pip is
// lying even though this gate is green.
public static class PassiveEffectConsumers
{
    private static readonly HashSet<PassiveEffectType> Consumed = new HashSet<PassiveEffectType>
    {
        // ── Agent-stat / mission path (CareerAgentStatService + TaomAgentStatCalculateModel) ──
        PassiveEffectType.Damage,              // DamageMultiplierBonus (mask-honored on the damage path)
        PassiveEffectType.Resistance,          // CalculateDamageReduction
        PassiveEffectType.ArmorPenetration,    // CalculateDamageAmplification
        PassiveEffectType.SwingSpeed,          // SwingSpeedMultiplier
        PassiveEffectType.MovementSpeed,       // MaxSpeedMultiplier
        PassiveEffectType.ShrugOff,         // DecideAgentShrugOffBlow
        PassiveEffectType.MountChargeDamage,   // mount MountChargeDamage via the rider (ApplyMountStatModifiers, #611)
        PassiveEffectType.MountHealth,         // mount GetEffectiveMaxHealth (multiplicative)
        PassiveEffectType.Ammo,                // OnAgentBuild ammo refill (multiplicative)
        PassiveEffectType.TroopResistance,     // CalculateDamageReduction for the leader's non-hero troops
        PassiveEffectType.TroopDamage,         // CalculateDamageAmplification for the leader's non-hero troops
                                               //   + TaomRaidModel.CalculateHitDamage (settlement raid speed).
                                               //   TWO consumers on purpose (#395) — different systems, not a
                                               //   double-count. Battle was the missing one for 105 pips.

        // ── Troop counters (#769 E10): CareerTroopHooks -> ICareerAgentStatService, leader's non-hero troops ──
        PassiveEffectType.TroopFearResistance,     // the four fear drains (Dread Aura, signature strikes, race ability deaths + auras)
        PassiveEffectType.TroopKnockdownResistance, // TaomAgentStatCalculateModel.GetKnockDownResistance (the engine's weapon, charge and dismount checks) and Hammerhand's bashes and kicks (CombatMechanicsHooks.BuildShieldBashContext); not TAOM's forced knockdowns
        PassiveEffectType.TroopCrushHold,          // TaomCombatMechanicsModel.DecideCrushedThrough (after every other verdict)

        // ── Party / campaign GameModels ──
        PassiveEffectType.Health,              // TaomCharacterStatsModel.MaxHitpoints (flat add)
        PassiveEffectType.PartyMovementSpeed,  // TaomPartySpeedModel
        PassiveEffectType.PartySpottingRange,  // TaomMapVisibilityModel.GetPartySpottingRange
        PassiveEffectType.StealthBonus,        // TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange:
                                               //   READ BY THE WRONG HOOK (#614). That method is the player spotting
                                               //   others; the player's party is never its argument, so the 52 pips
                                               //   are phantoms in practice. Kept here so the shipped-XML gate stays
                                               //   green until the replan; the fourth instance of the blind spot above.
        PassiveEffectType.PartySize,           // TaomPartySizeModel
        PassiveEffectType.CompanionLimit,      // TaomClanTierModel
        PassiveEffectType.TroopMorale,         // TaomPartyMoraleModel
        PassiveEffectType.TroopWages,          // TaomPartyWageModel
        PassiveEffectType.TroopUpgradeCost,    // TaomPartyTroopUpgradeModel
        PassiveEffectType.TroopSurvival,   // TaomPartyHealingModel.GetSurvivalChance
        PassiveEffectType.HeroHealing,  // TaomPartyHealingModel.GetDailyHealingHpForHeroes
        PassiveEffectType.InventoryCapacity,   // TaomInventoryCapacityModel
        PassiveEffectType.RenownGain,    // TaomBattleRewardModel
        PassiveEffectType.SmithingCostReduction, // TaomSmithingModel

        // ── Campaign consumers (#769 E11): one GameModel override each ──
        PassiveEffectType.InfluenceGain,       // TaomBattleRewardModel.CalculateInfluenceGain (a factor; 0 outside a kingdom stays 0)
        PassiveEffectType.RecruitCost,         // TaomPartyWageModel.GetTroopRecruitmentCost, summed in WageModifierService before the 1-gold floor
        PassiveEffectType.PrisonerConversion,  // TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour
        PassiveEffectType.FoodConsumption,     // TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf (the number is negative)
        PassiveEffectType.WoundRecovery,       // TaomPartyHealingModel.GetDailyHealingForRegulars (the leader's party; positive values, non-prisoner call only)
        PassiveEffectType.DailyGold,           // TaomClanFinanceModel.CalculateClanGoldChange + CalculateClanIncome (a count, ApplyFlat)
        PassiveEffectType.LootGold,            // TaomBattleRewardModel.GetLootGoldChances (the winner's own share, not renormalised)
        PassiveEffectType.RaidLoot,            // TaomRaidModel.GetRaidLootMultiplier (village gold, drawn items at 0.5 x, village goods, common loot at 0.25 x)
        PassiveEffectType.TroopTraining,       // TaomPartyTrainingModel.GetEffectiveDailyExperience (a count per troop, class-filtered)
        PassiveEffectType.TradePrices,         // TaomPartyTradeModel.GetTradePenaltyFactor (the raw penalty, both directions)

        // ── Special resources (SpecialResourceService) ──
        PassiveEffectType.SpecialResourceGain,
        PassiveEffectType.SpecialResourceUpkeepModifier,
        PassiveEffectType.SpecialResourceUpgradeCostModifier,
    };

    // The types whose consumer passes the hit's troop class (#769 E9, E10, E11), so a troop_class attribute means
    // something on them: CalculateDamageAmplification / CalculateDamageReduction resolve the struck or
    // striking agent's class and read GetTroopMagnitude, the three troop counters resolve the victim's, and
    // TaomPartyTrainingModel resolves the class of the roster element it is asked about (TroopTraining).
    // On any other type the attribute is ignored with a load warning, because honouring it would pretend to
    // restrict a pip that cannot be restricted.
    private static readonly HashSet<PassiveEffectType> ClassFilteredTypes = new HashSet<PassiveEffectType>
    {
        PassiveEffectType.TroopDamage,
        PassiveEffectType.TroopResistance,
        PassiveEffectType.TroopFearResistance,
        PassiveEffectType.TroopKnockdownResistance,
        PassiveEffectType.TroopCrushHold,
        PassiveEffectType.TroopTraining,
    };

    // The types whose consumer passes the hit (#613, #769 E9), so an attack_type_mask means something on
    // them: the hero's Damage and Resistance and the army-wide TroopDamage and TroopResistance are all read
    // per blow with the blow's mask. The three troop counters read no hit (a fear drain, a knockdown test and
    // a crush have none), so a mask on them, or on any other type, is ignored with a load warning: honouring
    // it would show a restriction the runtime never applies. Not the same set as ClassFiltered: the counters
    // are class-filtered and not mask-gated, the hero pair mask-gated and not class-filtered.
    private static readonly HashSet<PassiveEffectType> MaskGatedTypes = new HashSet<PassiveEffectType>
    {
        PassiveEffectType.Damage,
        PassiveEffectType.Resistance,
        PassiveEffectType.TroopDamage,
        PassiveEffectType.TroopResistance,
    };

    public static bool IsConsumed(PassiveEffectType type) => Consumed.Contains(type);

    public static bool IsClassFiltered(PassiveEffectType type) => ClassFilteredTypes.Contains(type);

    public static IEnumerable<PassiveEffectType> ClassFiltered => ClassFilteredTypes;

    public static bool IsMaskGated(PassiveEffectType type) => MaskGatedTypes.Contains(type);

    public static IEnumerable<PassiveEffectType> MaskGated => MaskGatedTypes;

    public static IEnumerable<PassiveEffectType> All => Consumed;
}
