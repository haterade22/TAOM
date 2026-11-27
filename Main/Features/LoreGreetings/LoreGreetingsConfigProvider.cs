using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LoreGreetings.Domain;

namespace TAOM.Features.LoreGreetings;

/// <summary>
/// Validating boundary loader for <c>lore_greetings/lore_greetings.json</c> (config-providers.md "Config Providers MUST Validate").
/// A missing file or unreadable JSON gives an empty table and one warning, so every greeting stays as it was. Each bad row is
/// dropped with a warning, and one summary warning follows: a person whose id is not a lower-case word (letters, digits,
/// underscores; it goes into a localization key), whose id is taken, who has no gender or no character id, or one of whose
/// character ids already belongs to another person; a greeting whose speaker or listener is no person, who greets himself,
/// repeats a pair, has no line, or has a line with a text variable or an animation mark (<c>{</c>, <c>[</c>, <c>]</c>). Ids and
/// lines are trimmed. Read once per process: an edit needs a full game restart.
/// </summary>
internal sealed class LoreGreetingsConfigProvider : ILoreGreetingsConfigProvider
{
    internal const string FolderName = "lore_greetings";
    internal const string FileName = "lore_greetings.json";

    private const string Tag = "LoreGreetingsConfigProvider: ";

    private static readonly Regex PersonId = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<LoreGreetingTable> _table;

    public LoreGreetingsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _table = new Lazy<LoreGreetingTable>(Load);
    }

    public LoreGreetingTable GetTable() => _table.Value;

    private LoreGreetingTable Load()
    {
        GreetingsFile? parsed;
        try
        {
            var path = Path.Combine(_pathService.ModuleDataPath, FolderName, FileName);
            if (!File.Exists(path))
            {
                _logger.LogWarning(Tag + FileName + " not found at " + path + ", no lore greetings");
                return LoreGreetingTable.Empty;
            }
            parsed = JsonConvert.DeserializeObject<GreetingsFile>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Tag + "failed to read or parse " + FileName + ", no lore greetings: " + ex.Message);
            return LoreGreetingTable.Empty;
        }

        var rejected = 0;
        var persons = Persons(parsed?.Persons, ref rejected);
        var lines = Greetings(parsed?.Greetings, persons, ref rejected);
        if (rejected > 0)
            _logger.LogWarning(Tag + rejected + " rows of " + FileName + " were dropped; see the warnings above");
        _logger.LogInfo(Tag + "loaded " + persons.Count + " persons and " + lines.Count + " greetings");
        return new LoreGreetingTable(persons, lines);
    }

    private List<LorePerson> Persons(List<PersonRow?>? rows, ref int rejected)
    {
        var persons = new List<LorePerson>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var characters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows ?? new List<PersonRow?>())
        {
            var id = row?.Id?.Trim() ?? string.Empty;
            var chars = (row?.Characters ?? new List<string?>()).Select(c => c?.Trim()).Where(c => !string.IsNullOrEmpty(c)).Select(c => c!).Distinct().ToList();
            string? reason = null;
            if (row == null)
                reason = "an empty row";
            else if (!PersonId.IsMatch(id))
                reason = "an id that is not a lower-case word";
            else if (ids.Contains(id))
                reason = "an id already taken";
            else if (row.Female == null)
                reason = "no female field";
            else if (chars.Count == 0)
                reason = "no character id";
            else if (chars.FirstOrDefault(characters.Contains) is { } taken)
                reason = "the character id " + taken + ", which another person already has";

            if (reason != null)
            {
                Reject("persons: '" + (row?.Id ?? "null") + "' has " + reason + "; dropping the person", ref rejected);
                continue;
            }

            ids.Add(id);
            characters.UnionWith(chars);
            persons.Add(new LorePerson(id, row!.Female!.Value, chars));
        }
        return persons;
    }

    private Dictionary<(string Speaker, string Listener), LoreGreetingLines> Greetings(List<GreetingRow?>? rows, List<LorePerson> persons, ref int rejected)
    {
        var known = new HashSet<string>(persons.Select(p => p.Id), StringComparer.Ordinal);
        var lines = new Dictionary<(string Speaker, string Listener), LoreGreetingLines>();
        foreach (var row in rows ?? new List<GreetingRow?>())
        {
            var speaker = row?.Speaker?.Trim() ?? string.Empty;
            var listener = row?.Listener?.Trim() ?? string.Empty;
            var greeting = new LoreGreetingLines(Line(row?.Text), Line(row?.Hostile), Line(row?.First));
            string? reason = null;
            if (row == null)
                reason = "is an empty row";
            else if (!known.Contains(speaker))
                reason = "names the speaker '" + speaker + "', who is no person";
            else if (!known.Contains(listener))
                reason = "names the listener '" + listener + "', who is no person";
            else if (speaker == listener)
                reason = "has " + speaker + " greet himself";
            else if (lines.ContainsKey((speaker, listener)))
                reason = "repeats the pair";
            else if (greeting.Text == null && greeting.Hostile == null && greeting.First == null)
                reason = "has no line";
            else if (new[] { greeting.Text, greeting.Hostile, greeting.First }.Any(l => l != null && l.IndexOfAny(new[] { '{', '[', ']' }) >= 0))
                reason = "has a line with '{', '[' or ']'";

            if (reason != null)
            {
                Reject("greetings: " + speaker + " to " + listener + " " + reason + "; dropping the greeting", ref rejected);
                continue;
            }
            lines[(speaker, listener)] = greeting;
        }
        return lines;
    }

    private static string? Line(string? text)
    {
        var trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private void Reject(string message, ref int rejected)
    {
        rejected++;
        _logger.LogWarning(Tag + message);
    }

    private sealed class GreetingsFile
    {
        [JsonProperty("persons")]
        public List<PersonRow?>? Persons { get; set; }

        [JsonProperty("greetings")]
        public List<GreetingRow?>? Greetings { get; set; }
    }

    private sealed class PersonRow
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("female")]
        public bool? Female { get; set; }

        [JsonProperty("characters")]
        public List<string?>? Characters { get; set; }
    }

    private sealed class GreetingRow
    {
        [JsonProperty("speaker")]
        public string? Speaker { get; set; }

        [JsonProperty("listener")]
        public string? Listener { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("hostile")]
        public string? Hostile { get; set; }

        [JsonProperty("first")]
        public string? First { get; set; }
    }
}
