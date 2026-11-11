using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions.Config;

/// <summary>
/// Validating loader for <c>story_missions/story_missions.json</c> (#1105), read once per process. A row that lacks an id, a valid scene, side, culture, garrison
/// or roster is skipped with one warning naming it; a number the Long Defeat readers own (the rules, the keep, the muster points, each wave) reverts or drops
/// as they do. The readers are the Long Defeat's, so a story row and a fortress row mean the same thing by the same key. The load never throws, because a
/// faulted <see cref="Lazy{T}"/> rethrows forever. Warnings carry the Long Defeat scope's tag (<c>LongDefeatConfigCatalog</c>) and this file's name.
/// </summary>
internal sealed class StoryMissionCatalog
{
    private const string FolderName = "story_missions";
    private const string FileName = "story_missions.json";

    internal const int MaxMusicLength = 120;

    private static readonly Regex MusicPath = new(@"^[A-Za-z0-9_]([A-Za-z0-9_/.-]*[A-Za-z0-9_-])?\.ogg$", RegexOptions.CultureInvariant);

    private readonly Lazy<IReadOnlyList<StoryMissionDefinition>> _missions;

    public StoryMissionCatalog(IPathService pathService, IModLogger logger)
        : this(logger, () => ReadFile(pathService, logger))
    {
    }

    private StoryMissionCatalog(IModLogger logger, Func<string?> readFile)
    {
        _missions = new Lazy<IReadOnlyList<StoryMissionDefinition>>(() => Guard(logger, readFile));
    }

    /// <summary>For tests: the same validation over text instead of the file. Null text is the missing-file case.</summary>
    internal static StoryMissionCatalog FromText(string? text, IModLogger logger) => new(logger, () => text);

    /// <summary>Every valid mission, in file order.</summary>
    public IReadOnlyList<StoryMissionDefinition> Missions => _missions.Value;

    public bool TryGet(string id, out StoryMissionDefinition mission)
    {
        mission = null!;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var candidate in _missions.Value)
        {
            if (string.Equals(candidate.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                mission = candidate;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<StoryMissionDefinition> Guard(IModLogger logger, Func<string?> readFile)
    {
        var scope = new LongDefeatConfigScope(logger, FileName);
        try
        {
            return Read(readFile(), scope);
        }
        catch (Exception ex)
        {
            scope.Warn($"could not be loaded ({ex.GetType().Name}: {ex.Message}); no missions");
            return Array.Empty<StoryMissionDefinition>();
        }
    }

    private static IReadOnlyList<StoryMissionDefinition> Read(string? text, LongDefeatConfigScope s)
    {
        var result = new List<StoryMissionDefinition>();
        var root = s.OpenRoot(text, "was not found or could not be read; no missions");
        if (root == null)
            return result;

        if (!(root["missions"] is JArray array))
        {
            s.Warn("'missions' is missing or not an array; no missions");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < array.Count; i++)
        {
            var mission = ReadMission(array[i] as JObject, i, seen, s);
            if (mission != null)
                result.Add(mission);
        }

        s.Loaded($"{result.Count} mission(s)");
        return result;
    }

    private static StoryMissionDefinition? ReadMission(JObject? o, int index, HashSet<string> seen, LongDefeatConfigScope s)
    {
        var id = LongDefeatConfigScope.Text(o?["id"]);
        if (o == null || id == null)
            return Skip(s, $"mission #{index}", "it has no 'id'");
        if (seen.Contains(id))
            return Skip(s, $"mission '{id}'", "duplicate id, the first one is kept");

        var scene = LongDefeatConfigScope.Text(o["scene"]);
        if (scene == null)
            return Skip(s, $"mission '{id}'", "'scene' is missing");
        if (!FortressesReader.IsSceneName(scene))
            return Skip(s, $"mission '{id}'", $"scene must be at most {FortressesReader.MaxSceneLength} letters, digits or underscores");
        if (!TrySide(o["side"], out var side, out var why))
            return Skip(s, $"mission '{id}'", "'side' " + why);

        var defenderCulture = LongDefeatConfigScope.Text(o["defenderCulture"]);
        if (defenderCulture == null)
            return Skip(s, $"mission '{id}'", "'defenderCulture' is missing");
        var attackerCulture = LongDefeatConfigScope.Text(o["attackerCulture"]);
        if (attackerCulture == null)
            return Skip(s, $"mission '{id}'", "'attackerCulture' is missing");

        var garrison = FortressesReader.ReadGarrison(o["garrison"] as JArray, id, s);
        if (garrison.Count == 0)
            return Skip(s, $"mission '{id}'", "it has no valid garrison row");
        if (!(o["roster"] is JObject rosterObject))
            return Skip(s, $"mission '{id}'", "'roster' is missing or not an object");

        var roster = WavesReader.ReadRoster(id, rosterObject, s);
        if (roster == null)
            return Skip(s, $"mission '{id}'", "its roster has no valid wave");

        var rules = WavesReader.KeepAboveStragglers(WavesReader.ReadRules(s.Group(o, "rules", ""), s), new[] { roster }, s);
        seen.Add(id);
        return new StoryMissionDefinition(id, LongDefeatConfigScope.Text(o["name"]) ?? id, scene, side, defenderCulture, attackerCulture, garrison, roster, rules,
            FortressesReader.ReadKeep(o["keep"], id, rules.KeepRadius, s), FortressesReader.ReadMuster(o["muster"], id, s), ReadMusic(o["music"], id, s));
    }

    private static string? ReadMusic(JToken? token, string id, LongDefeatConfigScope s)
    {
        if (token == null)
            return null;

        var text = LongDefeatConfigScope.Text(token);
        if (text != null && text.Length <= MaxMusicLength && !text.Contains("..") && MusicPath.IsMatch(text))
            return text;

        s.Warn($"mission '{id}': 'music' '{token}' is not a relative .ogg path under ModuleSounds of at most {MaxMusicLength} characters; dropped, the campaign's music plays");
        return null;
    }

    private static StoryMissionDefinition? Skip(LongDefeatConfigScope s, string row, string why)
    {
        s.Warn($"{row} skipped ({why})");
        return null;
    }

    // Only the two words the file may use, so a typo skips the row rather than taking a default that would change what each way of ending means.
    private static bool TrySide(JToken? token, out StorySide side, out string why)
    {
        side = StorySide.Defending;
        var text = LongDefeatConfigScope.Text(token);
        if (text == "Defend")
        {
            why = "";
            return true;
        }

        if (text == "Attack")
        {
            side = StorySide.Attacking;
            why = "";
            return true;
        }

        why = $"is '{text ?? token?.ToString() ?? "missing"}', not 'Defend' or 'Attack'";
        return false;
    }

    private static string? ReadFile(IPathService pathService, IModLogger logger)
    {
        var path = Path.Combine(pathService.ModuleDataPath, FolderName, FileName);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"StoryMissionCatalog: {FileName} could not be read at {path} ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }
}
