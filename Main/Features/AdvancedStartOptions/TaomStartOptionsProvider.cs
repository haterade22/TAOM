using System;
using SandBox.AdvancedStartOptions;
using TaleWorlds.Localization;
using TAOM.Features.CampaignStartOptions;

namespace TAOM.Features.AdvancedStartOptions;

/// <summary>
/// Adjusts v1.5.0's Advanced Starting Options menu for a Middle-earth map.
///
/// <para>
/// ASO's faction pickers are NOT data-driven. <c>SandBoxStartOptionsProvider.GetCultureItems()</c>
/// returns a literal list of the eight vanilla StringIds, so the LOTR kingdoms TAOM adds are
/// invisible to the menu and the eight that do appear resolve to TAOM's renamed vanilla kingdoms.
/// Left alone, the campaign-start screen offers "Western Empire" and drops the player into Gondor.
/// </para>
///
/// <para>
/// The engine's own extension point is a static method carrying
/// <c>[StartOptionsProvider]</c>. <c>AdvancedStartOptionsManager.Initialize()</c> reflects over every
/// active game assembly with <c>BindingFlags.Static | Public | NonPublic</c> and binds any method
/// matching <c>void (AdvancedStartOptions)</c>, so this runs without a Harmony patch.
/// </para>
/// </summary>
public static class TaomStartOptionsProvider
{
    // The kingdoms TAOM adds in taom_spkingdoms.xml (TaomKingdomIds_MatchTaomSpkingdoms pins the list).
    // The eight other playable kingdoms keep vanilla StringIds (renamed in place by spkingdoms.xslt)
    // and are already in ASO's hardcoded list, so adding them again would just overwrite the
    // existing entries.
    internal static readonly string[] TaomKingdomIds =
    {
        "erebor", "rivendell", "mirkwood", "lothlorien", "isengard", "gundabad", "umbar",
        "dolguldur", "shaghana", "abanissa", "goblin", "mistymountainorcs", "lindon", "bluecraig",
        "arthedain", "cardolan", "rhudaur", "eredluin", "anduinvale", "angaladh",
        "dorwinion", "southrhovanion",
    };

    // Every ASO list option that lets the player pick a faction.
    private static readonly string[] FactionPickerKeys =
    {
        "KingdomId", // which faction the King / Vassal / Mercenary start joins
    };

    // What a new campaign gets for each per-campaign option the player leaves alone (MCM, else JSON).
    // The menu runs before any campaign, but IoC.Configure ran in OnSubModuleLoad. A seam for tests.
    internal static Func<StartDefaults> Defaults =
        () => IoC.Resolve<ICampaignStartDefaults>().Current();

    [StartOptionsProvider]
    private static void AddStartOptions(SandBox.AdvancedStartOptions.AdvancedStartOptions options)
    {
        // The scenario list becomes War of the Ring or Sandbox; United Empire is not offered
        // (docs/features/campaign-start-options.md says why).
        CampaignStartOptionsScreen.Add(options, Defaults());
        AddTaomKingdomsToFactionPickers(options);
    }

    private static void AddTaomKingdomsToFactionPickers(SandBox.AdvancedStartOptions.AdvancedStartOptions options)
    {
        foreach (var key in FactionPickerKeys)
        {
            if (!(options.GetOption(key) is ListAdvancedStartOption picker)) continue;

            foreach (var kingdomId in TaomKingdomIds)
                picker.AddItem((kingdomId, NeverDisabled));
        }
    }

    // NOTE THE POLARITY. This delegate answers "is this item DISABLED", not "is it enabled".
    // Vanilla's own always-available helper is named GetNeverDisabledItem and returns FALSE, so
    // returning true here would grey out every TAOM kingdom.
    //
    // Nothing is gated: the engine's fief selection degrades rather than throwing when a kingdom is
    // small. GiveStartingFiefs falls through to FindFallbackStartingTown, which is guarded by
    // `if (list.Count > 0)` at each step. Lindon and Goblin-town own one town and no castle, so a
    // Vassal start there takes that fallback instead of receiving a castle.
    private static bool NeverDisabled(
        SandBox.AdvancedStartOptions.AdvancedStartOptions options, out TextObject disabledText)
    {
        disabledText = null;
        return false;
    }
}
