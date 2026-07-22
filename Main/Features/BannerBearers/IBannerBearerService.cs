using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.BannerBearers;

// All banner-bearer POLICY. Pure: takes primitives and the FormationClass value-type enum,
// never a Formation/Agent (ADR-007). The GameModel and MissionLogic boundaries adapt.
public interface IBannerBearerService
{
    bool IsEnabled { get; }

    // Read once per mission by the model — see BannerBearerConfig.MinimumFormationTroopCount
    // for why this must not vary mid-mission.
    int MinimumFormationTroopCount { get; }

    // How many bearers a formation of this size/class should field. 0 = none.
    int GetDesiredBearerCount(int formationUnitCount, FormationClass formationClass);

    // False for excluded races AND for any race id the engine doesn't know (fail-closed).
    bool IsRaceAllowed(int raceId);

    // Whether a troop of this formation class may carry a banner (default: Infantry only).
    // Keyed on the troop's DefaultFormationClass, i.e. its default_group XML attribute.
    bool IsFormationGroupAllowed(FormationClass formationClass);

    // Banner ItemObject id for a culture, or null when the culture should field no banner.
    string? ResolveBannerItemId(string? cultureId);

    // The culture a formation should fly, given every unit's culture id. The FIRST unit is not a
    // semantic culture owner (Formation.GetFirstUnit is just Arrangement.GetAllUnits()[0]), so a
    // mixed-culture formation — an allied army, or a mercenary-heavy player party — would fly
    // whichever standard happened to be arranged into slot 0. Majority wins instead, with a
    // deterministic tie-break so the result never depends on arrangement order.
    string? ResolveMajorityCultureId(IReadOnlyList<string?> cultureIds);
}
