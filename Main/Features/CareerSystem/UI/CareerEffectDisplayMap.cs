using System;
using System.Collections.Generic;
using System.Globalization;
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
/// size heuristic misreads both a 1.0 fraction (100%) and a +1 count.
/// </summary>
public static class CareerEffectDisplayMap
{
    private const string FallbackIcon = "22113";

    // Effects whose magnitude is an absolute count rather than a fraction.
    private static readonly HashSet<PassiveEffectType> FlatValueEffects = new HashSet<PassiveEffectType>
    {
        PassiveEffectType.Health,
        PassiveEffectType.PartySize,
        PassiveEffectType.CompanionLimit,
        PassiveEffectType.InventoryCapacity,
        PassiveEffectType.MountHealth,
    };

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
        [PassiveEffectType.HeroHealing] = "27001",
        [PassiveEffectType.RenownGain] = "27500",
        [PassiveEffectType.SmithingCostReduction] = "22105",
        [PassiveEffectType.SpecialResourceGain] = "22113",
        [PassiveEffectType.SpecialResourceUpkeepModifier] = "22113",
        [PassiveEffectType.SpecialResourceUpgradeCostModifier] = "22113",
    };

    private static readonly Dictionary<PassiveEffectType, string> Labels = new Dictionary<PassiveEffectType, string>
    {
        [PassiveEffectType.Special] = "special",
        [PassiveEffectType.Health] = "max health",
        [PassiveEffectType.Damage] = "damage",
        [PassiveEffectType.Resistance] = "resistance",
        [PassiveEffectType.ArmorPenetration] = "armor penetration",
        [PassiveEffectType.SwingSpeed] = "swing speed",
        [PassiveEffectType.MovementSpeed] = "movement speed",
        [PassiveEffectType.Ammo] = "ammo",
        [PassiveEffectType.ShrugOff] = "shrug off",
        [PassiveEffectType.StealthBonus] = "stealth bonus",
        [PassiveEffectType.BuffDuration] = "buff duration",
        [PassiveEffectType.MountHealth] = "mount health",
        [PassiveEffectType.MountChargeDamage] = "mount charge damage",
        [PassiveEffectType.PartyMovementSpeed] = "party speed",
        [PassiveEffectType.PartySpottingRange] = "party spotting range",
        [PassiveEffectType.PartySize] = "party size",
        [PassiveEffectType.CompanionLimit] = "companion limit",
        [PassiveEffectType.InventoryCapacity] = "inventory capacity",
        [PassiveEffectType.TroopDamage] = "troop damage",
        [PassiveEffectType.TroopResistance] = "troop resistance",
        [PassiveEffectType.TroopMorale] = "troop morale",
        [PassiveEffectType.TroopWages] = "troop wages",
        [PassiveEffectType.TroopUpgradeCost] = "troop upgrade cost",
        [PassiveEffectType.TroopSurvival] = "troop survival",
        [PassiveEffectType.HeroHealing] = "healing",
        [PassiveEffectType.RenownGain] = "battle renown gain",
        [PassiveEffectType.SmithingCostReduction] = "smithing cost",
        [PassiveEffectType.SpecialResourceGain] = "resource gain",
        [PassiveEffectType.SpecialResourceUpkeepModifier] = "resource upkeep",
        [PassiveEffectType.SpecialResourceUpgradeCostModifier] = "resource upgrade cost",
    };

    public static string IconFor(PassiveEffectType type)
        => Icons.TryGetValue(type, out var icon) ? icon : FallbackIcon;

    public static string LabelFor(PassiveEffectType type)
        => Labels.TryGetValue(type, out var label) ? label : type.ToString();

    /// <summary>One Active-Effects line, e.g. "+20% damage" or "+25 max health". Empty for a
    /// zero or non-finite magnitude — the panel prints nothing rather than "+NaN%".</summary>
    public static string Format(PassiveEffectType type, float magnitude)
    {
        if (float.IsNaN(magnitude) || float.IsInfinity(magnitude)) return "";

        var label = LabelFor(type);
        if (FlatValueEffects.Contains(type))
        {
            var flat = (int)Math.Round(magnitude);
            if (flat == 0) return "";
            return string.Format(CultureInfo.InvariantCulture, "{0}{1} {2}", flat > 0 ? "+" : "", flat, label);
        }

        var percent = (int)Math.Round(magnitude * 100f);
        if (percent == 0) return "";
        return string.Format(CultureInfo.InvariantCulture, "{0}{1}% {2}", percent > 0 ? "+" : "", percent, label);
    }
}
