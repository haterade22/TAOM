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
// KNOWN BLIND SPOT (#388) — this set answers "is anything reading it", NOT "is it read where
// the player expects it". Membership here is necessary, never sufficient, and it has now hidden the
// same bug TWICE while staying green:
//
//   #388 Health      — mission only. Worked in battle; invisible and inert on the campaign layer
//                      (character screen, Hero.MaxHitPoints, daily heal cap).
//   #388 TroopDamage — campaign only, and the wrong campaign system: its sole consumer was
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
        PassiveEffectType.MountChargeDamage,   // MountChargeDamage (rider props)
        PassiveEffectType.MountHealth,         // mount GetEffectiveMaxHealth (multiplicative)
        PassiveEffectType.Ammo,                // OnAgentBuild ammo refill (multiplicative)
        PassiveEffectType.TroopResistance,     // CalculateDamageReduction for the leader's non-hero troops
        PassiveEffectType.TroopDamage,         // CalculateDamageAmplification for the leader's non-hero troops
                                               //   + TaomRaidModel.CalculateHitDamage (settlement raid speed).
                                               //   TWO consumers on purpose (#388) — different systems, not a
                                               //   double-count. Battle was the missing one for 105 pips.

        // ── Party / campaign GameModels ──
        PassiveEffectType.Health,              // TaomCharacterStatsModel.MaxHitpoints (flat add)
        PassiveEffectType.PartyMovementSpeed,  // TaomPartySpeedModel
        PassiveEffectType.PartySpottingRange,  // TaomMapVisibilityModel.GetPartySpottingRange
        PassiveEffectType.StealthBonus,        // TaomMapVisibilityModel.GetPartySpottingRatioForMainPartySeeingRange
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

        // ── Special resources (SpecialResourceService) ──
        PassiveEffectType.SpecialResourceGain,
        PassiveEffectType.SpecialResourceUpkeepModifier,
        PassiveEffectType.SpecialResourceUpgradeCostModifier,
    };

    public static bool IsConsumed(PassiveEffectType type) => Consumed.Contains(type);

    public static IEnumerable<PassiveEffectType> All => Consumed;
}
