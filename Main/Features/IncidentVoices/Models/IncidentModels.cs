using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.IncidentVoices.Models;

/// <summary>Which of the two voices an incident speaks in for the player: the Free Peoples' or the Shadow's.</summary>
public enum IncidentVersion
{
    Free,
    Shadow,
}

/// <summary>
/// The player's people as <c>encounter_groups.json</c> reads them: the speaker group (west, rohirrim, orcs, ...) and the
/// race family (man, elf, dwarf, orc). Either is null when the data does not know the player's race or culture.
/// </summary>
public sealed record PeopleFacts(string? Group, string? Family)
{
    public static readonly PeopleFacts Unknown = new(null, null);
}

/// <summary>A town or village the player's side might name: its display name, its owner's side ids and its culture.</summary>
public sealed record PlaceFacts(string Name, SideIds Side, string CultureId);

/// <summary>Who may see an incident. An empty list means no limit on that axis. Versions are "free" and "shadow".</summary>
public sealed record IncidentRestrict(IReadOnlyList<string> Groups, IReadOnlyList<string> Families, IReadOnlyList<string> Versions)
{
    public static readonly IncidentRestrict None = new(new string[0], new string[0], new string[0]);
}

/// <summary>A vanilla text variable that names a random place, and the kind of place the player's side may supply: "town" or "vineyard".</summary>
public sealed record PlaceFix(string Variable, string Kind);

/// <summary>
/// One row of <c>incidents_lotr.json</c>.
/// <c>Versions</c> is <c>["all"]</c> for one text everyone sees, or <c>["free","shadow"]</c> for two. <c>Options</c> is the number of
/// options vanilla registers, <c>Vars</c> the text variables vanilla's own condition sets (parsed, but not read at runtime: only the converter and the binding tests use it). <c>VanillaText</c> keeps every
/// text vanilla's (another developer's override stands) while the gates still apply.
/// </summary>
public sealed record IncidentRule(
    string Id,
    string Verdict,
    IReadOnlyList<string> Versions,
    int Options,
    IReadOnlyList<string> Vars,
    IncidentRestrict Restrict,
    bool Disable,
    bool Disease,
    bool VanillaText,
    IReadOnlyList<PlaceFix> Places)
{
    public const string IdPrefix = "incident_";

    /// <summary>The id without the <c>incident_</c> prefix, as the text keys use it.</summary>
    public string ShortId => Id.StartsWith(IdPrefix, System.StringComparison.Ordinal) ? Id.Substring(IdPrefix.Length) : Id;

    /// <summary>True when one text serves every player.</summary>
    public bool OneText => Versions.Count == 1 && Versions[0] == IncidentTextKeys.All;
}
