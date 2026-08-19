using SandBox.AdvancedStartOptions;
using TaleWorlds.Localization;

namespace TAOM.Features.AdvancedStartOptions;

/// <summary>
/// Adjusts v1.5.0's Advanced Starting Options menu for a Middle-earth map.
///
/// <para>
/// ASO's faction pickers are NOT data-driven. <c>SandBoxStartOptionsProvider.GetCultureItems()</c>
/// returns a literal list of the eight vanilla StringIds, so TAOM's fourteen LOTR kingdoms are
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
    // The fourteen kingdoms TAOM adds in taom_spkingdoms.xml. The other eight playable kingdoms keep
    // vanilla StringIds (renamed in place by spkingdoms.xslt) and are already in ASO's hardcoded
    // list, so adding them again would just overwrite the existing entries.
    private static readonly string[] TaomKingdomIds =
    {
        "erebor", "rivendell", "mirkwood", "lothlorien", "isengard", "gundabad", "umbar",
        "dolguldur", "shaghana", "abanissa", "goblin", "mistymountainorcs", "lindon", "bluecraig",
    };

    // Every ASO list option that lets the player pick a faction.
    private static readonly string[] FactionPickerKeys =
    {
        "KingdomId",                 // which faction the King / Vassal / Mercenary start joins
        "LastStandKingdomId",        // the faction reduced to one town
        "InvasionScenarioFactionId", // the faction that expands aggressively
        "TwoFactionWarFaction1Id",
        "TwoFactionWarFaction2Id",
    };

    [StartOptionsProvider]
    private static void AddStartOptions(SandBox.AdvancedStartOptions.AdvancedStartOptions options)
    {
        RemoveUnitedEmpireScenario(options);
        AddTaomKingdomsToFactionPickers(options);
    }

    // The United Empire scenario is incoherent on this map and half-broken besides.
    //
    // It merges whichever factions hold the three imperial StringIds, which in TAOM are Dunland,
    // Gondor and Mordor, into a new kingdom built from a hardcoded TextObject literally named
    // "Calradian Empire" with StringId "calradian_empire".
    //
    // Worse, two of its three unifier choices leave the campaign in a broken state. Both
    // HandleKingdomCleanup and ResolveKingdom branch on `Culture.StringId == "empire"`, which in
    // vanilla is true for all three imperial kingdoms but in TAOM is true only for Dunland, because
    // spkingdoms.xslt gives empire_w and empire_s the gondor and mordor cultures. Picking Gondor or
    // Mordor as the unifier therefore skips both the deactivation and the redirect, and the player
    // ends up ruling an empty, fief-less shell beside the real merged kingdom.
    private static void RemoveUnitedEmpireScenario(SandBox.AdvancedStartOptions.AdvancedStartOptions options)
    {
        if (options.GetOption("Scenario") is ListAdvancedStartOption scenarios)
            scenarios.RemoveItem("unitedempire");
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
