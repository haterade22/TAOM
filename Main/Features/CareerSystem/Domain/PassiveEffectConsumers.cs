using System.Collections.Generic;

namespace TAOM.Features.CareerSystem.Domain;

// Source of truth for "which PassiveEffectType values actually do something at runtime."
//
// A career pip whose PassiveEffect type is NOT in this set is a PHANTOM bonus: the UI shows
// it, the player selects it, CareerPassiveService caches its magnitude — and nothing ever
// reads that magnitude, so the pip is inert. Six such types (HorseChargeDamage / HorseHealth /
// StealthBonus / TroopResistance / Ammo / HealthRegeneration) shipped as phantoms across ~211
// pips before they were wired up; this set + the CareerConfigProvider load-time warning + the
// CareerChoicesIntegrationTests "no phantom in shipped XML" regression guard exist so a new
// phantom can never ship silently again. (csharp-architecture.md "Config Providers MUST Validate".)
//
// When you add a consumer for a new PassiveEffectType, add it here. When you remove the last
// consumer for a type, remove it here — the regression test will then flag any shipped pip
// still using it.
public static class PassiveEffectConsumers
{
    private static readonly HashSet<PassiveEffectType> Consumed = new HashSet<PassiveEffectType>
    {
        // ── Agent-stat / mission path (CareerAgentStatService + TaomAgentStatCalculateModel) ──
        PassiveEffectType.Damage,              // DamageMultiplierBonus (mask-honored on the damage path)
        PassiveEffectType.Health,              // GetEffectiveMaxHealth flat add
        PassiveEffectType.Resistance,          // CalculateDamageReduction
        PassiveEffectType.ArmorPenetration,    // CalculateDamageAmplification
        PassiveEffectType.SwingSpeed,          // SwingSpeedMultiplier
        PassiveEffectType.MovementSpeed,       // MaxSpeedMultiplier
        PassiveEffectType.ShruggedOff,         // DecideAgentShrugOffBlow
        PassiveEffectType.HorseChargeDamage,   // MountChargeDamage (rider props)
        PassiveEffectType.HorseHealth,         // mount GetEffectiveMaxHealth (multiplicative)
        PassiveEffectType.Ammo,                // OnAgentBuild ammo refill (multiplicative)
        PassiveEffectType.TroopResistance,     // CalculateDamageReduction for the leader's non-hero troops

        // ── Party / campaign GameModels ──
        PassiveEffectType.PartyMovementSpeed,  // TaomPartySpeedModel
        PassiveEffectType.PartySpottingRange,  // TaomMapVisibilityModel.GetPartySpottingRange
        PassiveEffectType.StealthBonus,        // TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange
        PassiveEffectType.PartySize,           // TaomPartySizeModel
        PassiveEffectType.CompanionLimit,      // TaomClanTierModel
        PassiveEffectType.TroopDamage,         // TaomRaidModel
        PassiveEffectType.TroopMorale,         // TaomPartyMoraleModel
        PassiveEffectType.TroopWages,          // TaomPartyWageModel
        PassiveEffectType.TroopUpgradeCost,    // TaomPartyTroopUpgradeModel
        PassiveEffectType.TroopRegeneration,   // TaomPartyHealingModel.GetSurvivalChance
        PassiveEffectType.HealthRegeneration,  // TaomPartyHealingModel.GetDailyHealingHpForHeroes
        PassiveEffectType.InventoryCapacity,   // TaomInventoryCapacityModel
        PassiveEffectType.BattleRenownGain,    // TaomBattleRewardModel
        PassiveEffectType.EnchantmentCostReduction, // TaomSmithingModel

        // ── Special resources (SpecialResourceService) ──
        PassiveEffectType.CustomResourceGain,
        PassiveEffectType.CustomResourceUpkeepModifier,
        PassiveEffectType.CustomResourceUpgradeCostModifier,
    };

    public static bool IsConsumed(PassiveEffectType type) => Consumed.Contains(type);

    public static IEnumerable<PassiveEffectType> All => Consumed;
}
