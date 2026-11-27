using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.IncidentVoices.Models;

namespace TAOM.Features.IncidentVoices;

/// <summary>
/// Validating loader for <c>incidents_lotr/incidents_lotr.json</c>. A missing or unreadable file gives an empty catalog and one
/// warning, so every incident stays vanilla's. A bad row is dropped with a warning: an id that is empty or repeated, a verdict
/// that is not KEEP-REWORD, SPLIT, RESTRICT or DISABLE, versions that are neither <c>["all"]</c> nor <c>["free","shadow"]</c> (a
/// DISABLE row may list none), a negative option count, a restrict version or place kind that is not known. Lists are trimmed and
/// lower-cased; variable and vocabulary names are upper-cased.
/// </summary>
internal sealed class IncidentCatalogProvider : IIncidentCatalogProvider
{
    internal const string FolderName = "incidents_lotr";
    internal const string FileName = "incidents_lotr.json";
    internal static readonly string[] Verdicts = { "KEEP-REWORD", "SPLIT", "RESTRICT", "DISABLE" };
    internal static readonly string[] PlaceKinds = { "town", "vineyard" };

    private const string Tag = "IncidentCatalogProvider: ";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<IncidentCatalog> _catalog;

    public IncidentCatalogProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _catalog = new Lazy<IncidentCatalog>(Load);
    }

    public IncidentCatalog GetCatalog() => _catalog.Value;

    private IncidentCatalog Load()
    {
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, FolderName, FileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning(Tag + FileName + " not found at " + path + ", every incident keeps vanilla's rules and text");
                return IncidentCatalog.Empty;
            }
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Tag + "failed to read " + FileName + ", every incident keeps vanilla's rules and text: " + ex.Message);
            return IncidentCatalog.Empty;
        }
    }

    internal IncidentCatalog Parse(string json)
    {
        CatalogFile? file;
        try
        {
            file = JsonConvert.DeserializeObject<CatalogFile>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Tag + "could not parse " + FileName + ", every incident keeps vanilla's rules and text: " + ex.Message);
            return IncidentCatalog.Empty;
        }

        var rejected = false;
        var rules = new Dictionary<string, IncidentRule>(StringComparer.Ordinal);
        foreach (var row in file?.Incidents ?? new List<Row?>())
        {
            var rule = Validate(row, rules);
            if (rule == null)
            {
                _logger.LogWarning(Tag + $"incident '{row?.Id}' has an empty or repeated id, an unknown verdict, versions, restrict version or place kind, or a negative option count; dropping the row");
                rejected = true;
                continue;
            }
            rules[rule.Id] = rule;
        }

        var vocabulary = (file?.Vocabulary ?? new List<string?>()).Select(Upper).Where(v => v.Length > 0).Distinct().ToList();
        if (rejected)
            _logger.LogWarning(Tag + FileName + " contained invalid rows. See prior warnings for details.");
        else
            _logger.LogInfo(Tag + $"Loaded {FileName} ({rules.Count} incident(s), {vocabulary.Count} vocabulary word(s))");
        return new IncidentCatalog(rules, vocabulary);
    }

    private static IncidentRule? Validate(Row? row, Dictionary<string, IncidentRule> seen)
    {
        var id = row?.Id?.Trim().ToLowerInvariant() ?? string.Empty;
        var verdict = row?.Verdict?.Trim().ToUpperInvariant() ?? string.Empty;
        var versions = Lower(row?.Versions);
        var places = new List<PlaceFix>();
        var ok = id.Length > 0 && !seen.ContainsKey(id) && Array.IndexOf(Verdicts, verdict) >= 0 && (row?.Options ?? 0) >= 0;
        var versionsOk = (versions.Count == 1 && versions[0] == IncidentTextKeys.All)
            || (versions.Count == 2 && versions[0] == IncidentTextKeys.Free && versions[1] == IncidentTextKeys.Shadow)
            || (verdict == "DISABLE" && versions.Count == 0);
        var restrictVersions = Lower(row?.Restrict?.Versions);
        var restrictOk = restrictVersions.All(v => v == IncidentTextKeys.Free || v == IncidentTextKeys.Shadow);
        foreach (var place in row?.Places ?? new List<PlaceRow?>())
        {
            var kind = place?.Kind?.Trim().ToLowerInvariant() ?? string.Empty;
            var variable = Upper(place?.Variable);
            if (variable.Length == 0 || Array.IndexOf(PlaceKinds, kind) < 0)
                ok = false;
            else
                places.Add(new PlaceFix(variable, kind));
        }

        if (!ok || !versionsOk || !restrictOk)
            return null;

        var restrict = new IncidentRestrict(Lower(row!.Restrict?.Groups), Lower(row.Restrict?.Families), restrictVersions);
        return new IncidentRule(id, verdict, versions, row.Options, (row.Vars ?? new List<string?>()).Select(Upper).Where(v => v.Length > 0).ToList(),
            restrict, row.Disable || verdict == "DISABLE", row.Disease, row.VanillaText, places);
    }

    private static List<string> Lower(List<string?>? values) =>
        (values ?? new List<string?>()).Select(v => v?.Trim().ToLowerInvariant() ?? string.Empty).Where(v => v.Length > 0).ToList();

    private static string Upper(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed class CatalogFile
    {
        [JsonProperty("vocabulary")]
        public List<string?>? Vocabulary { get; set; }

        [JsonProperty("incidents")]
        public List<Row?>? Incidents { get; set; }
    }

    private sealed class Row
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("verdict")]
        public string? Verdict { get; set; }

        [JsonProperty("versions")]
        public List<string?>? Versions { get; set; }

        [JsonProperty("options")]
        public int Options { get; set; }

        [JsonProperty("vars")]
        public List<string?>? Vars { get; set; }

        [JsonProperty("restrict")]
        public RestrictRow? Restrict { get; set; }

        [JsonProperty("disable")]
        public bool Disable { get; set; }

        [JsonProperty("disease")]
        public bool Disease { get; set; }

        [JsonProperty("vanilla_text")]
        public bool VanillaText { get; set; }

        [JsonProperty("places")]
        public List<PlaceRow?>? Places { get; set; }
    }

    private sealed class RestrictRow
    {
        [JsonProperty("groups")]
        public List<string?>? Groups { get; set; }

        [JsonProperty("families")]
        public List<string?>? Families { get; set; }

        [JsonProperty("versions")]
        public List<string?>? Versions { get; set; }
    }

    private sealed class PlaceRow
    {
        [JsonProperty("variable")]
        public string? Variable { get; set; }

        [JsonProperty("kind")]
        public string? Kind { get; set; }
    }
}
