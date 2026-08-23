using TaleWorlds.Localization;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// The one owner of the per-camp-type player strings, shared by the overlay VM and the game-menu
/// controller so the two surfaces cannot drift apart.
///
/// <para>The source module built its "Raising ..." line by lower-casing the English type label at
/// runtime; that breaks the moment the label is translated (German nouns stay capitalised, CJK has
/// no case at all), so each raising line is its own complete string here instead.</para>
/// </summary>
internal static class FieldCampTexts
{
    /// <summary>Short label for a standing camp ("Field camp", "Ambush ready").</summary>
    public static TextObject TypeLabel(CampType type)
    {
        switch (type)
        {
            case CampType.Ambush:
                return new TextObject("{=taom_fcamp_state_ambush}Ambush ready");
            case CampType.Lookout:
                return new TextObject("{=taom_fcamp_state_lookout}Lookout posted");
            case CampType.Field:
                return new TextObject("{=taom_fcamp_state_field}Field camp");
            case CampType.Fortified:
                return new TextObject("{=taom_fcamp_state_fortified}Fortified camp");
            default:
                return new TextObject("{=taom_fcamp_state_generic}Camp");
        }
    }

    /// <summary>Status line while the camp is still being raised (the progress bar rides under it).</summary>
    public static TextObject RaisingLabel(CampType type)
    {
        switch (type)
        {
            case CampType.Ambush:
                return new TextObject("{=taom_fcamp_raising_ambush}Setting up the ambush.");
            case CampType.Lookout:
                return new TextObject("{=taom_fcamp_raising_lookout}Raising the lookout.");
            case CampType.Field:
                return new TextObject("{=taom_fcamp_raising_field}Raising the field camp.");
            case CampType.Fortified:
                return new TextObject("{=taom_fcamp_raising_fortified}Raising the fortified camp.");
            default:
                return new TextObject("{=taom_fcamp_raising_generic}Raising the camp.");
        }
    }

    /// <summary>Overlay status while foraging; the bar under it shows the accumulator toward the next grain.</summary>
    public static TextObject ForagingStatus(CampType type, int foragedTotal)
    {
        return new TextObject("{=taom_fcamp_status_foraging}{CAMP} (foraging - {GRAIN} grain)")
            .SetTextVariable("CAMP", TypeLabel(type))
            .SetTextVariable("GRAIN", foragedTotal);
    }
}
