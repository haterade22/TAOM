using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// Where each passive is consumed and where its evidence shows up (#613). The
/// <see cref="PassiveEffectConsumers"/> set says "something reads it"; this says what and where,
/// so a tester knows which log line, tooltip or probe to look at. Flat types (a count, not a
/// factor) are listed in <see cref="IsFlat"/> so the report prints "+6", not "+600%". The career screen's
/// Active Effects panel (<c>CareerEffectDisplayMap</c>) reads the same table, so the two never disagree.
/// </summary>
public static class CareerPerkConsumerMap
{
    public static bool IsFlat(PassiveEffectType type)
        => type == PassiveEffectType.Health || type == PassiveEffectType.PartySize || type == PassiveEffectType.CompanionLimit
        || type == PassiveEffectType.DailyGold || type == PassiveEffectType.TroopTraining;

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
            case PassiveEffectType.TroopResistance: return "per hit on the leader's non-hero troops: CalculateDamageReduction by mask and troop class; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.TroopDamage: return "per hit by the leader's non-hero troops by mask and troop class + TaomRaidModel raid speed; [CareerPerks] hit lines (DEBUG)";
            case PassiveEffectType.TroopFearResistance: return "CareerTroopHooks.ScaleFearDrain at the four fear drains (Dread Aura, signature strikes, race ability deaths and auras), per victim troop by class; the morale model itself is not touched; mission-end [CareerPerks] troop counter summary";
            case PassiveEffectType.TroopKnockdownResistance: return "CareerTroopHooks.KnockDownResistance in TaomAgentStatCalculateModel.GetKnockDownResistance: the engine's weapon, charge and dismount knockdown checks, per struck troop by class; Hammerhand's bashes and kicks through CombatMechanicsHooks.BuildShieldBashContext; not TAOM's forced knockdowns (Sauron's overhead slam, the elephant-like creature attacks: elephant, mumakil trample and tusk swing, war ram, great elk, Animalia elk and moose; the troll brute-force ring), which never ask for a resistance; the heavy-charge Branch A ignores it; mission-end [CareerPerks] troop counter summary";
            case PassiveEffectType.TroopCrushHold: return "CareerTroopHooks.ApplyCrushHold in TaomCombatMechanicsModel.DecideCrushedThrough, after every other verdict, per defending troop by class; mission-end [CareerPerks] troop counter summary";
            case PassiveEffectType.Health: return "TaomCharacterStatsModel.MaxHitpoints (flat); probe below";
            case PassiveEffectType.PartyMovementSpeed: return "TaomPartySpeedModel.CalculateFinalSpeed; probe below (Career line)";
            case PassiveEffectType.PartySpottingRange: return "TaomMapVisibilityModel.GetPartySpottingRange; probe below (Career line)";
            case PassiveEffectType.StealthBonus: return "NOT WIRED (#614): the hook is the player spotting others";
            case PassiveEffectType.PartySize: return "TaomPartySizeModel (flat); probe below (Career line)";
            case PassiveEffectType.CompanionLimit: return "TaomClanTierModel.GetCompanionLimit (flat); probe below";
            case PassiveEffectType.TroopMorale: return "TaomPartyMoraleModel; probe below (Career line)";
            case PassiveEffectType.TroopWages: return "TaomPartyWageModel.GetTotalWage; probe below (Career line)";
            case PassiveEffectType.TroopUpgradeCost: return "TaomPartyTroopUpgradeModel at upgrade time; [CareerPerks] event line";
            case PassiveEffectType.TroopSurvival: return "TaomPartyHealingModel.GetSurvivalChance per casualty at battle end (ScaleByPassive, capped at 1); no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.HeroHealing: return "TaomPartyHealingModel.GetDailyHealingHpForHeroes daily; [CareerPerks] event line; probe below (Career line)";
            case PassiveEffectType.InventoryCapacity: return "TaomInventoryCapacityModel; probe below (Career line)";
            case PassiveEffectType.RenownGain: return "TaomBattleRewardModel.CalculateRenownGain at battle end; [CareerPerks] event line";
            case PassiveEffectType.SmithingCostReduction: return "TaomSmithingModel energy costs; [CareerPerks] event line";
            case PassiveEffectType.SpecialResourceGain: return "SpecialResourceService.ScaleEarned on every earning source (daily, battle, raid, siege, prisoners, tournament, hideout)";
            case PassiveEffectType.SpecialResourceUpkeepModifier: return "SpecialResourceService upkeep";
            case PassiveEffectType.SpecialResourceUpgradeCostModifier: return "SpecialResourceService upgrade cost";
            case PassiveEffectType.InfluenceGain: return "TaomBattleRewardModel.CalculateInfluenceGain at battle end, kingdom members only (vanilla pays 0 outside a kingdom and the factor keeps 0); [CareerPerks] InfluenceGain event line, none while the gain is 0";
            case PassiveEffectType.RecruitCost: return "TaomPartyWageModel.GetTroopRecruitmentCost: summed with the buyer perk discounts in WageModifierService before the 1-gold floor, so the recruit screen's price is the price charged; the player's garrison auto-recruit bill and the SupplyLines troop prices use the player as buyer and get the cut too; a null buyer (ransom value, upgrade price) gets none; no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.PrisonerConversion: return "TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour: the recruiting party's prisoner experience per hour, one prisoner stack per hour; no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.FoodConsumption: return "TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf on the party the leader leads (negative = eats less, the base's -0.01 ceiling stays); no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.WoundRecovery: return "TaomPartyHealingModel.GetDailyHealingForRegulars on the party the leader leads, positive healing on the non-prisoner call only (a starving party, a raft party whose -0.25 per regular outweighs its healing, and the captor's prisoner healing are untouched); no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.DailyGold: return "TaomClanFinanceModel.CalculateClanGoldChange (the daily payment when applyWithdrawals is true, and the tooltip) and CalculateClanIncome (the income tile), a flat count; no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.LootGold: return "TaomBattleRewardModel.GetLootGoldChances at battle end: the winner's own share of the plundered gold, not renormalised; [CareerPerks] LootGold event line (the share before -> after)";
            case PassiveEffectType.RaidLoot: return "TaomRaidModel.GetRaidLootMultiplier, read four ways: the village gold, once per drawn item (kept with chance 0.5 x the multiplier, so a total of +100% makes every drawn item certain), the village production goods, and the common-loot roll (at 0.25 x the multiplier); no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.TroopTraining: return "TaomPartyTrainingModel.GetEffectiveDailyExperience: a flat count of experience per troop per day by troop class, added to each non-hero stack on the daily tick; no runtime line; taom.career_perks shows the value (army-wide and per class)";
            case PassiveEffectType.TradePrices: return "TaomPartyTradeModel.GetTradePenaltyFactor: the raw trade penalty behind every buy and sell price of the party's leader (negative = a smaller penalty, better prices both ways); no runtime line; taom.career_perks shows the value";
            case PassiveEffectType.BuffDuration: return "reserved, deliberately unconsumed";
            default: return "no consumer (parse fallback)";
        }
    }
}
