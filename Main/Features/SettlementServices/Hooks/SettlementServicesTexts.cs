using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.SettlementServices.Hooks;

/// <summary>
/// Every TextObject of Watch-Silver and Tidings of the Lists (#859), as inline <c>{=taom_wsilver_*}</c> and <c>{=taom_tidings_*}</c>
/// literals so the harvester registers them, plus the coin icon. The dialogue lines and the arena option are literals in their own
/// files because the engine takes them as plain strings. The vanilla reasons are reused by key, never copied: the key
/// <c>d0kbtGYn</c> is vanilla's own "You don't have enough gold." No line has a predicate adjective, so each reads the same for any
/// speaker or player gender.
/// </summary>
internal static class SettlementServicesTexts
{
    private const string CoinIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    /// <summary>
    /// Vanilla sets {GOLD_ICON} just before the text that shows it (its guard's bribe does), because it is not a reliable global.
    /// So does every TAOM text that shows it: call this in the condition that builds the line.
    /// </summary>
    internal static void SetGoldIcon() => MBTextManager.SetTextVariable("GOLD_ICON", "{=!}" + CoinIcon, false);

    /// <summary>One Watch-Silver offer: the loyalty it adds and its price.</summary>
    internal static TextObject Tier(int points, int price) =>
        new TextObject("{=taom_wsilver_tier}Raise loyalty by {POINTS} for {COST}{GOLD_ICON}.")
            .SetTextVariable("POINTS", points).SetTextVariable("COST", price).SetTextVariable("GOLD_ICON", CoinIcon);

    internal static TextObject AtCeiling() =>
        new("{=taom_wsilver_at_ceiling}Silver cannot steady this place beyond what it holds now.");

    internal static TextObject NotEnoughGold() => new("{=d0kbtGYn}You don't have enough gold.");

    /// <summary>The fee shown inside the arena option's text.</summary>
    internal static void SetTidingsFee(int fee)
    {
        MBTextManager.SetTextVariable("TAOM_TIDINGS_FEE", fee);
        SetGoldIcon();
    }

    internal static string TidingsTitle() => new TextObject("{=taom_tidings_title}Tidings of the Lists").ToString();

    internal static string TidingsNone() =>
        new TextObject("{=taom_tidings_none}The training master knows of no tournament in a friendly town. You keep your coin.").ToString();

    /// <summary>Shown when asking failed for a reason the player cannot act on, or a gate closed since the menu was built.</summary>
    internal static string TidingsRefused() => new TextObject("{=taom_tidings_refused}The training master has no answer for you now.").ToString();

    /// <summary>The arena master's answer: one "Town (Realm)" line per row, nearest first.</summary>
    internal static string TidingsBody(IReadOnlyList<TidingsRow> rows)
    {
        var lines = new List<string>(rows.Count);
        foreach (var row in rows)
            lines.Add(new TextObject("{=taom_tidings_row}{TOWN} ({REALM})")
                .SetTextVariable("TOWN", row.Town).SetTextVariable("REALM", row.Realm).ToString());
        return new TextObject("{=taom_tidings_body}The training master names the nearest tournaments:{newline}{LIST}")
            .SetTextVariable("LIST", string.Join("\n", lines)).ToString();
    }

    internal static string Ok() => GameTexts.FindText("str_ok").ToString();
}
