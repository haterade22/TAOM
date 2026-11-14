using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.EncounterLines;

/// <summary>A bandit clan as <c>encounter_groups.json</c> lists it: its side, its race family and, for a clan of Men, the speaker group it comes from.</summary>
internal sealed record BanditClan(FactionSide Side, string Family, string? HomeGroup);

/// <summary>
/// The validated contents of <c>encounter_lines/encounter_groups.json</c>: culture to speaker group, group to race family,
/// race to family, and the bandit clans. Every lookup answers null for an id it does not hold.
/// </summary>
internal sealed class EncounterGroupMap
{
    internal const string Man = "man";
    internal const string Elf = "elf";
    internal const string Dwarf = "dwarf";
    internal const string Orc = "orc";
    internal const string NoFamily = "none";

    internal static readonly IReadOnlyCollection<string> SpeakingFamilies = new[] { Man, Elf, Dwarf, Orc };

    internal static readonly EncounterGroupMap Empty = new(
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, BanditClan>());

    internal EncounterGroupMap(
        IReadOnlyDictionary<string, string> cultureGroups,
        IReadOnlyDictionary<string, string> groupFamilies,
        IReadOnlyDictionary<string, string> raceFamilies,
        IReadOnlyDictionary<string, BanditClan> bandits)
    {
        CultureGroups = cultureGroups;
        GroupFamilies = groupFamilies;
        RaceFamilies = raceFamilies;
        Bandits = bandits;
    }

    internal IReadOnlyDictionary<string, string> CultureGroups { get; }

    internal IReadOnlyDictionary<string, string> GroupFamilies { get; }

    internal IReadOnlyDictionary<string, string> RaceFamilies { get; }

    internal IReadOnlyDictionary<string, BanditClan> Bandits { get; }

    internal string? GroupOfCulture(string? cultureId) => Find(CultureGroups, cultureId);

    internal string? FamilyOfGroup(string? group) => Find(GroupFamilies, group);

    internal string? FamilyOfRace(string? race) => Find(RaceFamilies, race);

    internal BanditClan? Bandit(string? clanId) =>
        clanId != null && Bandits.TryGetValue(clanId, out var clan) ? clan : null;

    /// <summary>Trims and lower-cases an id the way the loader stores keys; null stays null.</summary>
    internal static string? Normalize(string? value) => value?.Trim().ToLowerInvariant();

    private static string? Find(IReadOnlyDictionary<string, string> map, string? key) =>
        key != null && map.TryGetValue(key, out var value) ? value : null;
}
