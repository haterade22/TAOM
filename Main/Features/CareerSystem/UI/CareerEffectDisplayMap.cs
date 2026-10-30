using System;
using System.Collections.Generic;
using System.Globalization;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Diagnostics;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

/// <summary>
/// Issue #388 — presentation data for the diamond career screen: the banner icon a passive
/// choice shows on its diamond, its display label, and how its magnitude reads in the
/// Active Effects panel.
///
/// Banner icons are used because the per-choice sprites authored in taom_career_choices.xml
/// (<c>career_choice_*</c>) were never drawn — zero PNGs, zero atlas entries — which is why
/// <c>IconSprite</c> was dead data bound by no prefab. Banner icons are monochrome
/// silhouettes that tint and scale cleanly, and they are already baked.
///
/// Percent-vs-flat is keyed on effect TYPE, never on magnitude size: TAOM authors Damage /
/// Ammo / MovementSpeed as fractions (0.05) and Health / PartySize as counts (25, 4), so a
/// size heuristic misreads both a 1.0 fraction (100%) and a +1 count. The one table is
/// <see cref="CareerPerkConsumerMap.IsFlat"/>, which the diagnostic report reads too.
///
/// Each label carries its value as a <c>{VALUE}</c> variable, so a language can put the number before or
/// after the noun, and the value itself is one of two keys (<c>{NUMBER}%</c> or <c>{NUMBER}</c>) because the
/// percent sign's spacing and position differ by language.
/// </summary>
public static class CareerEffectDisplayMap
{
    private const string FallbackIcon = "22113";

    private const string PercentValue = "{=taom_career_value_percent}{NUMBER}%";
    private const string FlatValue = "{=taom_career_value_flat}{NUMBER}";

    private static readonly Dictionary<PassiveEffectType, string> Icons = new Dictionary<PassiveEffectType, string>
    {
        [PassiveEffectType.Special] = "22113",
        [PassiveEffectType.Health] = "10001",
        [PassiveEffectType.Damage] = "30012",
        [PassiveEffectType.Resistance] = "28110",
        [PassiveEffectType.ArmorPenetration] = "19014",
        [PassiveEffectType.SwingSpeed] = "10006",
        [PassiveEffectType.MovementSpeed] = "10018",
        [PassiveEffectType.Ammo] = "10011",
        [PassiveEffectType.ShrugOff] = "24029",
        [PassiveEffectType.StealthBonus] = "26109",
        [PassiveEffectType.BuffDuration] = "24501",
        [PassiveEffectType.MountHealth] = "30004",
        [PassiveEffectType.MountChargeDamage] = "26108",
        [PassiveEffectType.PartyMovementSpeed] = "30003",
        [PassiveEffectType.PartySpottingRange] = "19006",
        [PassiveEffectType.PartySize] = "11004",
        [PassiveEffectType.CompanionLimit] = "24006",
        [PassiveEffectType.InventoryCapacity] = "26107",
        [PassiveEffectType.TroopDamage] = "10020",
        [PassiveEffectType.TroopResistance] = "23003",
        [PassiveEffectType.TroopMorale] = "30005",
        [PassiveEffectType.TroopWages] = "24009",
        [PassiveEffectType.TroopUpgradeCost] = "24021",
        [PassiveEffectType.TroopSurvival] = "26102",
        [PassiveEffectType.TroopFearResistance] = "30005",
        [PassiveEffectType.TroopKnockdownResistance] = "23003",
        [PassiveEffectType.TroopCrushHold] = "24029",
        [PassiveEffectType.HeroHealing] = "27001",
        [PassiveEffectType.RenownGain] = "27500",
        [PassiveEffectType.SmithingCostReduction] = "22105",
        [PassiveEffectType.SpecialResourceGain] = "22113",
        [PassiveEffectType.SpecialResourceUpkeepModifier] = "22113",
        [PassiveEffectType.SpecialResourceUpgradeCostModifier] = "22113",
        [PassiveEffectType.InfluenceGain] = "27500",
        [PassiveEffectType.RecruitCost] = "24021",
        [PassiveEffectType.PrisonerConversion] = "24006",
        [PassiveEffectType.FoodConsumption] = "26107",
        [PassiveEffectType.WoundRecovery] = "27001",
        [PassiveEffectType.DailyGold] = "24009",
        [PassiveEffectType.LootGold] = "24009",
        [PassiveEffectType.RaidLoot] = "22105",
        [PassiveEffectType.TroopTraining] = "10020",
        [PassiveEffectType.TradePrices] = "24009",
    };

    private static readonly Dictionary<PassiveEffectType, string> Labels = new Dictionary<PassiveEffectType, string>
    {
        [PassiveEffectType.Special] = "{=taom_career_effect_special}{VALUE} special",
        [PassiveEffectType.Health] = "{=taom_career_effect_health}{VALUE} max health",
        [PassiveEffectType.Damage] = "{=taom_career_effect_damage}{VALUE} damage",
        [PassiveEffectType.Resistance] = "{=taom_career_effect_resistance}{VALUE} resistance",
        [PassiveEffectType.ArmorPenetration] = "{=taom_career_effect_armor_penetration}{VALUE} armor penetration",
        [PassiveEffectType.SwingSpeed] = "{=taom_career_effect_swing_speed}{VALUE} swing speed",
        [PassiveEffectType.MovementSpeed] = "{=taom_career_effect_movement_speed}{VALUE} movement speed",
        [PassiveEffectType.Ammo] = "{=taom_career_effect_ammo}{VALUE} ammo",
        [PassiveEffectType.ShrugOff] = "{=taom_career_effect_shrug_off}{VALUE} shrug off",
        [PassiveEffectType.StealthBonus] = "{=taom_career_effect_stealth_bonus}{VALUE} stealth bonus",
        [PassiveEffectType.BuffDuration] = "{=taom_career_effect_buff_duration}{VALUE} buff duration",
        [PassiveEffectType.MountHealth] = "{=taom_career_effect_mount_health}{VALUE} mount health",
        [PassiveEffectType.MountChargeDamage] = "{=taom_career_effect_mount_charge_damage}{VALUE} mount charge damage",
        [PassiveEffectType.PartyMovementSpeed] = "{=taom_career_effect_party_movement_speed}{VALUE} party speed",
        [PassiveEffectType.PartySpottingRange] = "{=taom_career_effect_party_spotting_range}{VALUE} party spotting range",
        [PassiveEffectType.PartySize] = "{=taom_career_effect_party_size}{VALUE} party size",
        [PassiveEffectType.CompanionLimit] = "{=taom_career_effect_companion_limit}{VALUE} companion limit",
        [PassiveEffectType.InventoryCapacity] = "{=taom_career_effect_inventory_capacity}{VALUE} inventory capacity",
        [PassiveEffectType.TroopDamage] = "{=taom_career_effect_troop_damage}{VALUE} troop damage",
        [PassiveEffectType.TroopResistance] = "{=taom_career_effect_troop_resistance}{VALUE} troop resistance",
        [PassiveEffectType.TroopMorale] = "{=taom_career_effect_troop_morale}{VALUE} troop morale",
        [PassiveEffectType.TroopWages] = "{=taom_career_effect_troop_wages}{VALUE} troop wages",
        [PassiveEffectType.TroopUpgradeCost] = "{=taom_career_effect_troop_upgrade_cost}{VALUE} troop upgrade cost",
        [PassiveEffectType.TroopSurvival] = "{=taom_career_effect_troop_survival}{VALUE} troop survival",
        [PassiveEffectType.TroopFearResistance] = "{=taom_career_effect_troop_fear_resistance}{VALUE} troop fear resistance",
        [PassiveEffectType.TroopKnockdownResistance] = "{=taom_career_effect_troop_knockdown_resistance}{VALUE} troop knockdown resistance",
        [PassiveEffectType.TroopCrushHold] = "{=taom_career_effect_troop_crush_hold}{VALUE} troop crush hold",
        [PassiveEffectType.HeroHealing] = "{=taom_career_effect_hero_healing}{VALUE} healing",
        [PassiveEffectType.RenownGain] = "{=taom_career_effect_renown_gain}{VALUE} battle renown gain",
        [PassiveEffectType.SmithingCostReduction] = "{=taom_career_effect_smithing_cost_reduction}{VALUE} smithing cost",
        [PassiveEffectType.SpecialResourceGain] = "{=taom_career_effect_special_resource_gain}{VALUE} resource gain",
        [PassiveEffectType.SpecialResourceUpkeepModifier] = "{=taom_career_effect_special_resource_upkeep_modifier}{VALUE} resource upkeep",
        [PassiveEffectType.SpecialResourceUpgradeCostModifier] = "{=taom_career_effect_special_resource_upgrade_cost_modifier}{VALUE} resource upgrade cost",
        [PassiveEffectType.InfluenceGain] = "{=taom_career_effect_influence_gain}{VALUE} battle influence gain",
        [PassiveEffectType.RecruitCost] = "{=taom_career_effect_recruit_cost}{VALUE} recruit cost",
        [PassiveEffectType.PrisonerConversion] = "{=taom_career_effect_prisoner_conversion}{VALUE} prisoner conversion speed",
        [PassiveEffectType.FoodConsumption] = "{=taom_career_effect_food_consumption}{VALUE} food consumption",
        [PassiveEffectType.WoundRecovery] = "{=taom_career_effect_wound_recovery}{VALUE} troop wound recovery",
        [PassiveEffectType.DailyGold] = "{=taom_career_effect_daily_gold}{VALUE} gold per day",
        [PassiveEffectType.LootGold] = "{=taom_career_effect_loot_gold}{VALUE} battle loot gold",
        [PassiveEffectType.RaidLoot] = "{=taom_career_effect_raid_loot}{VALUE} raid loot",
        [PassiveEffectType.TroopTraining] = "{=taom_career_effect_troop_training}{VALUE} experience per troop per day",
        [PassiveEffectType.TradePrices] = "{=taom_career_effect_trade_prices}{VALUE} trade penalty",
    };

    public static string IconFor(PassiveEffectType type)
        => Icons.TryGetValue(type, out var icon) ? icon : FallbackIcon;

    /// <summary>One Active-Effects line, e.g. "+20% damage" or "+25 max health". Empty for a
    /// zero or non-finite magnitude — the panel prints nothing rather than "+NaN%". A passive
    /// restricted to some troop classes (#769 E9) carries them: "+10% troop damage for infantry".</summary>
    public static string Format(PassiveEffectType type, float magnitude, TroopClassMask troopClass = TroopClassMask.All)
    {
        var line = FormatValue(type, magnitude);
        return line.Length == 0 || troopClass == TroopClassMask.All ? line : WithClasses(line, troopClass);
    }

    private static string WithClasses(string line, TroopClassMask troopClass)
    {
        var phrase = ClassPhrase(troopClass);
        return phrase == null ? line : new TextObject(phrase).SetTextVariable("EFFECT", line).ToString();
    }

    // One whole phrase per combination, not fragments glued with a join word: word order and agreement differ
    // across the 12 languages, and each translator sees the sentence it is translating. All carries no suffix
    // (Format returns the plain line before it gets here); a mask with no class bit, which only a hand-built
    // passive can produce, keeps the plain line too.
    private static string? ClassPhrase(TroopClassMask troopClass)
    {
        switch (troopClass & TroopClassMask.All)
        {
            case TroopClassMask.Infantry:
                return "{=taom_career_class_line_infantry}{EFFECT} for infantry";
            case TroopClassMask.Ranged:
                return "{=taom_career_class_line_ranged}{EFFECT} for ranged troops";
            case TroopClassMask.Cavalry:
                return "{=taom_career_class_line_cavalry}{EFFECT} for cavalry";
            case TroopClassMask.Infantry | TroopClassMask.Ranged:
                return "{=taom_career_class_line_infantry_ranged}{EFFECT} for infantry and ranged troops";
            case TroopClassMask.Infantry | TroopClassMask.Cavalry:
                return "{=taom_career_class_line_infantry_cavalry}{EFFECT} for infantry and cavalry";
            case TroopClassMask.Ranged | TroopClassMask.Cavalry:
                return "{=taom_career_class_line_ranged_cavalry}{EFFECT} for ranged troops and cavalry";
            default:
                return null;
        }
    }

    private static string FormatValue(PassiveEffectType type, float magnitude)
    {
        var number = SignedNumber(type, magnitude, out var isPercent);
        if (number.Length == 0) return "";

        var value = new TextObject(isPercent ? PercentValue : FlatValue).SetTextVariable("NUMBER", number);
        var label = Labels.TryGetValue(type, out var row) ? row : "{VALUE} " + type;
        return new TextObject(label).SetTextVariable("VALUE", value).ToString();
    }

    /// <summary>The signed whole number a magnitude shows ("+10", "-5"), with <paramref name="isPercent"/> true
    /// when it is a fraction scaled by 100. Empty for a NaN, an infinity or a value that rounds to 0. Pure (no
    /// TextObject), so the numeric decision is tested without the game.</summary>
    internal static string SignedNumber(PassiveEffectType type, float magnitude, out bool isPercent)
    {
        isPercent = !CareerPerkConsumerMap.IsFlat(type);
        if (float.IsNaN(magnitude) || float.IsInfinity(magnitude)) return "";

        var number = (int)Math.Round(isPercent ? magnitude * 100f : magnitude);
        if (number == 0) return "";
        return (number > 0 ? "+" : "") + number.ToString(CultureInfo.InvariantCulture);
    }
}
