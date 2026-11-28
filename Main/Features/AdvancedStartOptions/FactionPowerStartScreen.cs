using System.Collections.Generic;
using SandBox.AdvancedStartOptions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.FactionPower;
using TAOM.Features.LocalizationOverride;
using Options = SandBox.AdvancedStartOptions.AdvancedStartOptions;

namespace TAOM.Features.AdvancedStartOptions;

/// <summary>
/// The "Realm power" category of the Advanced Start Options screen: a preset list (Standard, Evil +50%, Free peoples +50%, Custom)
/// and one 50 to 200 slider per realm culture of factions.json, 100 meaning no change. The sliders show only under Custom (an option condition answers "is this hidden", so true hides).
/// <see cref="FactionPowerSession"/> turns what the player picked into the campaign's stored percents.
/// The slider labels come from the catalog, so a new factions.json entry needs no row: the screen reads each option's name and
/// description from the main-menu text manager, and this class applies one row per slider to that manager when it builds
/// the screen (<see cref="GlobalStringsOverrides"/>, the route SubModule uses for global_strings.xml). The category and preset rows
/// are fixed and live in global_strings.xml (#604).
/// </summary>
internal static class FactionPowerStartScreen
{
    internal const string NameText = "str_campaign_starting_options_name";
    internal const string DescriptionText = "str_campaign_starting_options_description";

    private const string SliderDescription =
        "{=taom_fp_slider_desc}How strong this realm is, in percent of its usual strength: party size of its lords, garrisons, militia, volunteers and auto-resolved fights. 100 changes nothing. Your own clan is never changed.";

    internal static void Add(Options options, IReadOnlyList<FactionPowerEntry> catalog, GameTextManager? labels)
    {
        if (catalog.Count == 0) return;

        var presets = new List<(string Identifier, ListAdvancedStartOption.ListItemCondition Condition)>();
        foreach (var item in FactionPowerScreenKeys.PresetItems)
            presets.Add((item, CampaignStartOptionsScreen.NeverDisabled));
        options.Add(new ListAdvancedStartOption(
            FactionPowerScreenKeys.PresetOption, FactionPowerScreenKeys.Category, presets, null, FactionPowerScreenKeys.StandardItem));

        foreach (var entry in catalog)
            options.Add(new IntAdvancedStartOption(
                FactionPowerScreenKeys.SliderKey(entry.CultureId), FactionPowerScreenKeys.Category,
                FactionPowerMath.MinPercent, FactionPowerMath.MaxPercent, IsSliderHidden, FactionPowerMath.NeutralPercent));

        if (labels != null) GlobalStringsOverrides.Apply(labels, LabelRows(catalog));
    }

    /// <summary>One name row and one description row for each slider, keyed by the option id the screen asks with.</summary>
    internal static IReadOnlyList<(string Id, string Variation, string Text)> LabelRows(IReadOnlyList<FactionPowerEntry> catalog)
    {
        var rows = new List<(string Id, string Variation, string Text)>();
        foreach (var entry in catalog)
        {
            var key = FactionPowerScreenKeys.SliderKey(entry.CultureId);
            rows.Add((NameText, key, entry.Name));
            rows.Add((DescriptionText, key, SliderDescription));
        }
        return rows;
    }

    private static bool IsSliderHidden(Options o) =>
        o.GetOption(FactionPowerScreenKeys.PresetOption)?.GetValue<string>() != FactionPowerScreenKeys.CustomItem;
}
