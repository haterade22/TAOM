using System.Collections.Generic;
using TaleWorlds.Localization;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The encyclopedia troop-tree badge (#590), pure and flat like <see cref="SpecialResourceMessages"/>
/// so it is testable without a campaign. A troop earns the badge when it costs the player a
/// resource to upgrade into, recruit, or keep. A row that only carries an Elite Emissary price is
/// an ordinary tree troop the emissary happens to sell (50 of the 77 rows) and stays unmarked.
/// Every number is a slot or a pre-formatted value, never baked into a default text (#434).
/// </summary>
internal static class SpecialResourceTroopBadge
{
    /// <summary>Positive requirements throughout, so a NaN upkeep fails the gate rather than passing it.</summary>
    public static bool IsShown(TroopResourceCostEntry cost) =>
        cost != null && (cost.UpgradeCost > 0 || cost.RecruitCost > 0 || cost.DailyUpkeep > 0f);

    public static TextObject Title(SpecialResource resource) =>
        new TextObject("{=taom_res_badge_title}Requires {RESOURCE}")
            .SetTextVariable("RESOURCE", resource.DisplayName);

    /// <summary>Label and pre-formatted amount, one row per cost field above zero, in a fixed order.</summary>
    public static IReadOnlyList<KeyValuePair<TextObject, string>> Rows(TroopResourceCostEntry cost)
    {
        var rows = new List<KeyValuePair<TextObject, string>>(4);
        if (cost == null) return rows;
        if (cost.UpgradeCost > 0) Add(rows, "{=taom_res_badge_upgrade}Upgrade", cost.UpgradeCost);
        if (cost.RecruitCost > 0) Add(rows, "{=taom_res_badge_recruit}Recruit", cost.RecruitCost);
        if (cost.DailyUpkeep > 0f) Add(rows, "{=taom_res_badge_upkeep}Upkeep per day", cost.DailyUpkeep);
        if (cost.MerchantCost > 0) Add(rows, "{=taom_res_badge_emissary}Elite Emissary price", cost.MerchantCost);
        return rows;
    }

    /// <summary>
    /// Upgrades, recruits and upkeep are charged in the PLAYER's resolved resource whatever the
    /// troop's faction (#558), so a Gondor player reading a Mordor elite pays Castar. Null when the
    /// player's resource is the troop's own, or when the player has none.
    /// </summary>
    public static TextObject PaidInNote(SpecialResource troopResource, SpecialResource playerResource)
    {
        if (troopResource == null || playerResource == null || playerResource.Id == troopResource.Id)
            return null;

        return new TextObject("{=taom_res_badge_paid_in}Upgrades, recruits and upkeep are paid in your own {RESOURCE}")
            .SetTextVariable("RESOURCE", playerResource.DisplayName);
    }

    private static void Add(List<KeyValuePair<TextObject, string>> rows, string label, float amount) =>
        rows.Add(new KeyValuePair<TextObject, string>(new TextObject(label), SpecialResourceMessages.FormatAmount(amount)));
}
