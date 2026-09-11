using System.Globalization;
using TaleWorlds.Localization;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The player-facing lines for every special-resource OUTFLOW (#558). Earnings had a green toast
/// from the start; upkeep, the party-screen upgrade commit, the recruit charge and the floor-at-zero
/// overdraft only ever wrote to the log, which is how "my War Spoils vanish after every battle"
/// became a report.
///
/// Pure and flat so the slot rule is testable without a campaign: every number rides on the
/// TextObject as a variable, never baked into the default text (the desertion popup shipped that way
/// and was unlocalizable by construction, #434). Numbers are pre-formatted with
/// <see cref="FormatAmount"/> so "0.05" and "12" both read as written.
/// </summary>
internal static class SpecialResourceMessages
{
    public static TextObject DailyUpkeep(string resourceName, float income, float upkeep, float balance) =>
        new TextObject("{=taom_res_daily_upkeep}{RESOURCE}: +{INCOME} income, -{UPKEEP} upkeep ({BALANCE} left)")
            .SetTextVariable("RESOURCE", resourceName)
            .SetTextVariable("INCOME", FormatAmount(income))
            .SetTextVariable("UPKEEP", FormatAmount(upkeep))
            .SetTextVariable("BALANCE", FormatAmount(balance));

    public static TextObject UpkeepOverdraft(string resourceName, float upkeep, float balanceBefore) =>
        new TextObject("{=taom_res_upkeep_overdraft}Upkeep of {UPKEEP} {RESOURCE} exceeded your {BALANCE}; none is left")
            .SetTextVariable("RESOURCE", resourceName)
            .SetTextVariable("UPKEEP", FormatAmount(upkeep))
            .SetTextVariable("BALANCE", FormatAmount(balanceBefore));

    public static TextObject UpgradeSpend(string resourceName, float amount, float balance) =>
        new TextObject("{=taom_res_upgrade_spend}-{AMOUNT} {RESOURCE} spent on upgrades ({BALANCE} left)")
            .SetTextVariable("RESOURCE", resourceName)
            .SetTextVariable("AMOUNT", FormatAmount(amount))
            .SetTextVariable("BALANCE", FormatAmount(balance));

    public static TextObject RecruitCharge(string resourceName, float amount, int count, string troopName) =>
        new TextObject("{=taom_res_recruit_charge}-{AMOUNT} {RESOURCE} for {COUNT} {TROOP}")
            .SetTextVariable("RESOURCE", resourceName)
            .SetTextVariable("AMOUNT", FormatAmount(amount))
            .SetTextVariable("COUNT", count)
            .SetTextVariable("TROOP", troopName);

    /// <summary>Up to two decimals, trailing zeros dropped, invariant separator: 3, 2.4, 0.05.</summary>
    internal static string FormatAmount(float value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
