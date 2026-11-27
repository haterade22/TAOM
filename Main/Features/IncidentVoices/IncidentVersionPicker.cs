using TAOM.Features.EncounterLines;
using TAOM.Features.Execution;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Which voice a side and a people read: Free reads the Free text, Evil the Shadow text. Neutral is decided by people (owner,
/// 2026-11-26): Neutral orcs, Southrons and Easterlings read the Shadow text; every other Neutral people, Men of the West,
/// Rohirrim, Northmen, Dunlendings, Elves and Dwarves, and a player whose people is unknown, read the Free text. A side that
/// cannot be read is Free.
/// </summary>
internal static class IncidentVersionPicker
{
    private static readonly string[] ShadowLeaningPeoples = { "orcs", "southrons", "easterlings" };

    public static IncidentVersion Pick(FactionSide? side, string? group)
    {
        switch (side)
        {
            case FactionSide.Evil:
                return IncidentVersion.Shadow;
            case FactionSide.Neutral:
                return group != null && System.Array.IndexOf(ShadowLeaningPeoples, group) >= 0 ? IncidentVersion.Shadow : IncidentVersion.Free;
            default:
                return IncidentVersion.Free;
        }
    }

    /// <summary>The player's people from the shipped map: race family first, then the speaker group of race or culture.</summary>
    public static PeopleFacts Describe(EncounterGroupMap map, string? cultureId, string? raceName)
    {
        var family = map.FamilyOfRace(raceName);
        var group = map.GroupOf(cultureId, raceName);
        var groupFamily = map.FamilyOfGroup(group);
        if (family == null || family == EncounterGroupMap.NoFamily)
            return new PeopleFacts(group, groupFamily);
        return new PeopleFacts(groupFamily == family ? group : null, family);
    }
}
