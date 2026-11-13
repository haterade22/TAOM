using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// Validating boundary loader for <c>encounter_lines/encounter_groups.json</c> (config-providers.md "Config Providers MUST
/// Validate"). A missing file or unreadable JSON gives an empty map and one warning, so every encounter keeps vanilla's
/// text. A bad row is dropped with a warning: a group, race or clan whose family is not a known family, a clan whose side
/// is not free, evil or neutral, or whose home group is not a listed group. A culture listed in two groups stays in the
/// first. Keys and values are trimmed and lower-cased. There is no built-in copy: the file is the only source.
/// </summary>
internal sealed class EncounterGroupsConfigProvider : IEncounterGroupsConfigProvider
{
    internal const string FolderName = "encounter_lines";
    internal const string FileName = "encounter_groups.json";

    private const string Tag = "EncounterGroupsConfigProvider: ";

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<EncounterGroupMap> _map;

    public EncounterGroupsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _map = new Lazy<EncounterGroupMap>(Load);
    }

    public EncounterGroupMap GetMap() => _map.Value;

    private EncounterGroupMap Load()
    {
        GroupsFile? parsed;
        try
        {
            // Built inside the try: the Lazy caches a throw, so a bad path must end here as an empty map, not on every read.
            var path = Path.Combine(_pathService.ModuleDataPath, FolderName, FileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning(Tag + FileName + " not found at " + path + ", every encounter keeps vanilla's lines");
                return EncounterGroupMap.Empty;
            }
            parsed = JsonConvert.DeserializeObject<GroupsFile>(File.ReadAllText(path), SerializerSettings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Tag + "failed to read or parse " + FileName + ", every encounter keeps vanilla's lines: " + ex.Message);
            return EncounterGroupMap.Empty;
        }

        return Validate(parsed ?? new GroupsFile());
    }

    private EncounterGroupMap Validate(GroupsFile parsed)
    {
        var rejected = false;
        var cultureGroups = new Dictionary<string, string>(StringComparer.Ordinal);
        var groupFamilies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in parsed.Groups ?? new Dictionary<string, GroupRow?>())
        {
            var group = EncounterGroupMap.Normalize(pair.Key);
            var family = EncounterGroupMap.Normalize(pair.Value?.Family);
            if (string.IsNullOrEmpty(group) || family == null || !IsSpeakingFamily(family) || groupFamilies.ContainsKey(group!))
            {
                Reject($"groups['{pair.Key}'] has an empty or repeated id or the family '{pair.Value?.Family}', which is not one of {string.Join(", ", EncounterGroupMap.SpeakingFamilies)}; dropping the group", ref rejected);
                continue;
            }
            groupFamilies[group!] = family;
            foreach (var raw in pair.Value!.Cultures ?? new List<string?>())
            {
                var culture = EncounterGroupMap.Normalize(raw);
                if (string.IsNullOrEmpty(culture))
                    continue;
                if (cultureGroups.TryGetValue(culture!, out var first))
                {
                    Reject($"culture '{culture}' is listed in group '{first}' and again in '{group}'; keeping '{first}'", ref rejected);
                    continue;
                }
                cultureGroups[culture!] = group!;
            }
        }

        var raceFamilies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in parsed.Races ?? new Dictionary<string, string?>())
        {
            var race = EncounterGroupMap.Normalize(pair.Key);
            var family = EncounterGroupMap.Normalize(pair.Value);
            if (string.IsNullOrEmpty(race) || family == null || !(IsSpeakingFamily(family) || family == EncounterGroupMap.NoFamily) || raceFamilies.ContainsKey(race!))
            {
                Reject($"races['{pair.Key}'] = '{pair.Value}' is not one of {string.Join(", ", EncounterGroupMap.SpeakingFamilies)} or {EncounterGroupMap.NoFamily}; dropping the row", ref rejected);
                continue;
            }
            raceFamilies[race!] = family;
        }

        var bandits = new Dictionary<string, BanditClan>(StringComparer.Ordinal);
        foreach (var pair in parsed.Bandits ?? new Dictionary<string, BanditRow?>())
        {
            var clan = EncounterGroupMap.Normalize(pair.Key);
            var family = EncounterGroupMap.Normalize(pair.Value?.Family);
            var side = ParseSide(pair.Value?.Side);
            var home = EncounterGroupMap.Normalize(pair.Value?.HomeGroup);
            if (home == string.Empty)
                home = null;
            if (string.IsNullOrEmpty(clan) || family == null || !IsSpeakingFamily(family) || side == null || bandits.ContainsKey(clan!)
                || (home != null && !groupFamilies.ContainsKey(home)))
            {
                Reject($"bandits['{pair.Key}'] needs a family from {string.Join(", ", EncounterGroupMap.SpeakingFamilies)}, a side of free, evil or neutral, and a home_group that is a listed group (got '{pair.Value?.Family}', '{pair.Value?.Side}', '{pair.Value?.HomeGroup}'); dropping the clan", ref rejected);
                continue;
            }
            bandits[clan!] = new BanditClan(side.Value, family, home);
        }

        if (rejected)
            _logger.LogWarning(Tag + FileName + " contained invalid rows. See prior warnings for details.");
        else
            _logger.LogInfo(Tag + $"Loaded {FileName} ({groupFamilies.Count} group(s), {cultureGroups.Count} culture(s), {raceFamilies.Count} race(s), {bandits.Count} bandit clan(s))");

        return new EncounterGroupMap(cultureGroups, groupFamilies, raceFamilies, bandits);
    }

    private void Reject(string message, ref bool rejected)
    {
        _logger.LogWarning(Tag + message);
        rejected = true;
    }

    private static bool IsSpeakingFamily(string family)
    {
        foreach (var known in EncounterGroupMap.SpeakingFamilies)
        {
            if (known == family)
                return true;
        }
        return false;
    }

    private static FactionSide? ParseSide(string? value) => EncounterGroupMap.Normalize(value) switch
    {
        "free" => FactionSide.Free,
        "evil" => FactionSide.Evil,
        "neutral" => FactionSide.Neutral,
        _ => null,
    };

    private sealed class GroupsFile
    {
        [JsonProperty("groups")]
        public Dictionary<string, GroupRow?>? Groups { get; set; }

        [JsonProperty("races")]
        public Dictionary<string, string?>? Races { get; set; }

        [JsonProperty("bandits")]
        public Dictionary<string, BanditRow?>? Bandits { get; set; }
    }

    private sealed class GroupRow
    {
        [JsonProperty("family")]
        public string? Family { get; set; }

        [JsonProperty("cultures")]
        public List<string?>? Cultures { get; set; }
    }

    private sealed class BanditRow
    {
        [JsonProperty("side")]
        public string? Side { get; set; }

        [JsonProperty("family")]
        public string? Family { get; set; }

        [JsonProperty("home_group")]
        public string? HomeGroup { get; set; }
    }
}
