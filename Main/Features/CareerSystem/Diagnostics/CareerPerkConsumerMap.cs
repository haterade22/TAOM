using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// Where each passive is consumed and where its evidence shows up (#613). The
/// <see cref="PassiveEffectConsumers"/> set says "something reads it"; this says what and where,
/// so a tester knows which log line, tooltip or probe to look at. Flat types (a count, not a
/// factor) are listed in <see cref="IsFlat"/> so the report prints "+6", not "+600%".
/// </summary>
public static class CareerPerkConsumerMap
{
    public static bool IsFlat(PassiveEffectType type)
        => type == PassiveEffectType.Health || type == PassiveEffectType.PartySize || type == PassiveEffectType.CompanionLimit;

    public static string Describe(PassiveEffectType type)
    {
        switch (type)
        {
            case PassiveEffectType.Damage: return "per hit, attacker: CalculateDamageAmplification by mask; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.Resistance: return "per hit, victim: CalculateDamageReduction by mask; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.ArmorPenetration: return "per hit, attacker: CalculateDamageAmplification; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.SwingSpeed: return "agent SwingSpeedMultiplier after base; [CareerPerks] stat lines + mission block";
            case PassiveEffectType.MovementSpeed: return "agent MaxSpeedMultiplier after base; [CareerPerks] stat lines + mission block";
            case PassiveEffectType.ShrugOff: return "DecideAgentShrugOffBlow for the hero victim";
            case PassiveEffectType.MountChargeDamage: return "the MOUNT's MountChargeDamage via the rider; [CareerPerks] mount lines + mission block";
            case PassiveEffectType.MountHealth: return "the MOUNT's max health via the rider (GetEffectiveMaxHealth); mission block";
            case PassiveEffectType.Ammo: return "spawn-time refill of every consumable slot (arrows, bolts, thrown); [CareerPerks] ammo line";
            case PassiveEffectType.TroopResistance: return "per hit on the leader's non-hero troops: CalculateDamageReduction; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.TroopDamage: return "per hit by the leader's non-hero troops + TaomRaidModel raid speed; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.Health: return "TaomCharacterStatsModel.MaxHitpoints (flat); probe below";
            case PassiveEffectType.PartyMovementSpeed: return "TaomPartySpeedModel.CalculateFinalSpeed; probe below (Career line)";
            case PassiveEffectType.PartySpottingRange: return "TaomMapVisibilityModel.GetPartySpottingRange; probe below (Career line)";
            case PassiveEffectType.StealthBonus: return "NOT WIRED (#614): the hook is the player spotting others";
            case PassiveEffectType.PartySize: return "TaomPartySizeModel (flat); probe below (Career line)";
            case PassiveEffectType.CompanionLimit: return "TaomClanTierModel.GetCompanionLimit (flat); probe below";
            case PassiveEffectType.TroopMorale: return "TaomPartyMoraleModel; probe below (Career line)";
            case PassiveEffectType.TroopWages: return "TaomPartyWageModel.GetTotalWage; probe below (Career line)";
            case PassiveEffectType.TroopUpgradeCost: return "TaomPartyTroopUpgradeModel at upgrade time; [CareerPerks] event line";
            case PassiveEffectType.TroopSurvival: return "TaomPartyHealingModel.GetSurvivalChance at battle end; [CareerPerks] event line";
            case PassiveEffectType.HeroHealing: return "TaomPartyHealingModel.GetDailyHealingHpForHeroes daily; probe below (Career line)";
            case PassiveEffectType.InventoryCapacity: return "TaomInventoryCapacityModel; probe below (Career line)";
            case PassiveEffectType.RenownGain: return "TaomBattleRewardModel.CalculateRenownGain at battle end; [CareerPerks] event line";
            case PassiveEffectType.SmithingCostReduction: return "TaomSmithingModel energy costs; [CareerPerks] event line";
            case PassiveEffectType.SpecialResourceGain: return "SpecialResourceService daily gain";
            case PassiveEffectType.SpecialResourceUpkeepModifier: return "SpecialResourceService upkeep";
            case PassiveEffectType.SpecialResourceUpgradeCostModifier: return "SpecialResourceService upgrade cost";
            case PassiveEffectType.BuffDuration: return "reserved, deliberately unconsumed";
            default: return "no consumer (parse fallback)";
        }
    }
}
