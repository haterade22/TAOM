using System.Globalization;
using TaleWorlds.Localization;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>The player-facing words of rest healing and start in camp (#860 parts B and C), apart from the healing line's label in
/// <see cref="CampRestModelLine"/>; every string is a <c>{=taom_camp_*}</c> key with its English default.</summary>
internal static class CampRestStartTexts
{
    internal static TextObject RestStatus(float regulars, float heroes) =>
        new TextObject("{=taom_camp_rest_status}Resting here heals {R} more wounded troops a day, and the heroes {H} more hit points a day.")
            .SetTextVariable("R", Number(regulars)).SetTextVariable("H", Number(heroes));

    /// <summary>The one message of a new campaign that could not start in camp.</summary>
    internal static string StartBlocked(CampBlockReason reason)
    {
        return new TextObject("{=taom_camp_start_blocked}Your campaign begins without a camp: {REASON}")
            .SetTextVariable("REASON", Why(reason)).ToString();
    }

    /// <summary>The reason words, lower case and ending in a full stop: they follow a lead-in ("Your campaign begins
    /// without a camp: ..." and the Make Camp tooltip's "Cannot camp now: ...").</summary>
    internal static TextObject Why(CampBlockReason reason)
    {
        switch (reason)
        {
            case CampBlockReason.InSettlement:
                return new TextObject("{=taom_camp_start_settlement}the party stands inside a settlement.");
            case CampBlockReason.Moving:
                return new TextObject("{=taom_camp_start_moving}the party is on the move.");
            case CampBlockReason.External:
                return new TextObject("{=taom_camp_start_external}something nearby forbids a camp here.");
            default:
                return new TextObject("{=taom_camp_start_other}a camp cannot be pitched here.");
        }
    }

    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
