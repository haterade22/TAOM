using System.Collections.Generic;
using System.Linq;
using SandBox.AdvancedStartOptions;
using TaleWorlds.Localization;
using TAOM.Features.CampaignStartOptions;
using static TAOM.Features.CampaignStartOptions.CampaignStartOptionKeys;
using Options = SandBox.AdvancedStartOptions.AdvancedStartOptions;

namespace TAOM.Features.AdvancedStartOptions;

/// <summary>
/// The per-campaign choices on the Advanced Start Options screen: War of the Ring or Sandbox, its start
/// date, and the Middle-earth rules (Troop Weight, War can be won, Eye of Sauron hunters, Large AI
/// armies and the Alignment rules preset). What the player picks is saved with the
/// campaign by the engine; <see cref="CampaignStartSession"/> pins the values and
/// <see cref="CampaignStartOptionsStore"/> reads them back; this class only builds the
/// screen. The vanilla war scenarios (Last Stand, 2 Faction War, Invasion, Alternative) and United Empire
/// are not offered (owner decision 2026-11-18): the scenario is the War of the Ring or Sandbox.
/// </summary>
internal static class CampaignStartOptionsScreen
{
    private const string WorldCategory = "worldscenarios";

    // Vanilla scenario ids this screen no longer offers. LastStand, twofactionwar, InvasionId and
    // alternativecalradia declare wars at campaign start, which the War of the Ring's phases would end on
    // the first day; unitedempire builds a "Calradian Empire" (see TaomStartOptionsProvider);
    // nordinvasion is NavalDLC's scenario, which is incompatible with TAOM (an absent id is a no-op).
    internal static readonly string[] HiddenScenarioIds =
    {
        "unitedempire", "LastStand", "twofactionwar", "InvasionId", "alternativecalradia", "nordinvasion",
    };

    internal static void Add(Options options, StartDefaults defaults)
    {
        ReplaceScenario(options, defaults);
        AddStartDate(options, defaults);
        AddRules(options, defaults);
    }

    // `Add` with an existing id replaces that option in place, which is how the scenario list gets its new
    // default (an option's default is fixed at construction). The items are copied, never rebuilt, so
    // another mod's scenario that vanilla's provider ran before us survives.
    private static void ReplaceScenario(Options options, StartDefaults defaults)
    {
        var items = new List<(string Identifier, ListAdvancedStartOption.ListItemCondition Condition)>
        {
            (WarOfTheRingScenarioItem, NeverDisabled),
        };
        if (options.GetOption(ScenarioOption) is ListAdvancedStartOption vanilla)
            items.AddRange(vanilla.GetItems().Where(item => !HiddenScenarioIds.Contains(item.Identifier)));
        else
            items.Add((SandboxScenarioItem, NeverDisabled));

        options.Add(new ListAdvancedStartOption(ScenarioOption, WorldCategory, items, null,
            defaults.WarOfTheRing ? WarOfTheRingScenarioItem : SandboxScenarioItem));
    }

    private static void AddStartDate(Options options, StartDefaults defaults)
    {
        var presets = new List<(string Identifier, ListAdvancedStartOption.ListItemCondition Condition)>
        {
            (StartEarlyItem, NeverDisabled),
            (StartStandardItem, NeverDisabled),
            (StartLateItem, NeverDisabled),
            (StartCustomItem, NeverDisabled),
        };
        options.Add(new ListAdvancedStartOption(StartDateOption, WorldCategory, presets,
            o => !IsWarOfTheRing(o), StartStandardItem));

        options.Add(new IntAdvancedStartOption(CustomDay1Option, WorldCategory, MinDay, MaxDay - 1,
            IsCustomHidden, defaults.Phase1Day));
        options.Add(new IntAdvancedStartOption(CustomDay2Option, WorldCategory, MinDay + 1, MaxDay,
            IsCustomHidden, defaults.Phase2Day));
    }

    private static void AddRules(Options options, StartDefaults defaults)
    {
        options.Add(new BooleanAdvancedStartOption(TroopWeightOption, RulesCategory, null, defaults.TroopWeight));
        options.Add(new BooleanAdvancedStartOption(WarCanBeWonOption, RulesCategory, null, defaults.MomentumVictory));
        options.Add(new BooleanAdvancedStartOption(EyeHuntersOption, RulesCategory, null, defaults.EyeOfSauron));
        options.Add(new BooleanAdvancedStartOption(LargeAiArmiesOption, RulesCategory, null, defaults.AiPartyScaling));

        var presets = new List<(string Identifier, ListAdvancedStartOption.ListItemCondition Condition)>
        {
            (AlignmentStandardItem, NeverDisabled),
            (AlignmentStrictItem, NeverDisabled),
            (AlignmentRelaxedItem, NeverDisabled),
        };
        options.Add(new ListAdvancedStartOption(AlignmentPresetOption, RulesCategory, presets, null, AlignmentStandardItem));
    }

    // NOTE THE POLARITY. An option condition answers "is this option HIDDEN" (true hides it).
    private static bool IsCustomHidden(Options o) => !IsWarOfTheRing(o) || CurrentStartDate(o) != StartCustomItem;

    private static bool IsWarOfTheRing(Options o) =>
        o.GetOption(ScenarioOption)?.GetValue<string>() == WarOfTheRingScenarioItem;

    private static string? CurrentStartDate(Options o) => o.GetOption(StartDateOption)?.GetValue<string>();

    // NOTE THE POLARITY. An item condition answers "is this item DISABLED": false keeps it selectable.
    internal static bool NeverDisabled(Options options, out TextObject disabledText)
    {
        disabledText = null!;
        return false;
    }
}
