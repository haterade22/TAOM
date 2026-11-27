using TAOM.Features.EncounterLines;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Whether the player's people and voice may see an incident. A disabled incident is never allowed (it stays registered: a saved
/// cooldown entry names it). Elves never see a sickness incident (<c>Disease</c>). The restrict lists are allow-lists on three axes,
/// the speaker group, the race family and the voice; an empty list limits nothing, and a listed axis the player's data cannot
/// answer (unknown group or family) closes the incident.
/// </summary>
internal static class IncidentEligibility
{
    public static bool Allowed(IncidentRule rule, IncidentVersion version, PeopleFacts people)
    {
        if (rule.Disable)
            return false;
        if (rule.Disease && people.Family == EncounterGroupMap.Elf)
            return false;

        var restrict = rule.Restrict;
        if (restrict.Versions.Count > 0 && !Contains(restrict.Versions, IncidentTextKeys.VersionToken(version)))
            return false;
        if (restrict.Families.Count > 0 && (people.Family == null || !Contains(restrict.Families, people.Family)))
            return false;
        if (restrict.Groups.Count > 0 && (people.Group == null || !Contains(restrict.Groups, people.Group)))
            return false;
        return true;
    }

    /// <summary>A plain reason a console can show, or null when the incident is allowed.</summary>
    public static string? ClosedReason(IncidentRule rule, IncidentVersion version, PeopleFacts people)
    {
        if (Allowed(rule, version, people))
            return null;
        if (rule.Disable)
            return "TAOM has no counterpart for it in Middle-earth and keeps it closed.";
        if (rule.Disease && people.Family == EncounterGroupMap.Elf)
            return "Elves do not sicken.";
        return "it is closed for your people or your side (family " + (people.Family ?? "unknown") + ", group " + (people.Group ?? "unknown") + ", " + IncidentTextKeys.VersionToken(version) + " voice).";
    }

    private static bool Contains(System.Collections.Generic.IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }
        return false;
    }
}
