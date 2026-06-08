using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TAOM.Features.Music;

public sealed class MusicTrackIndex
{
    public const string NeutralCulture = "neutral_culture";

    private readonly Dictionary<string, List<MusicTrackDefinition>> _tracksByPool;

    private MusicTrackIndex(Dictionary<string, List<MusicTrackDefinition>> tracksByPool)
    {
        _tracksByPool = tracksByPool ?? new Dictionary<string, List<MusicTrackDefinition>>(StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _tracksByPool.Values.Sum(v => v.Count);

    public static MusicTrackIndex LoadFromModuleRoot(string moduleRootPath)
    {
        if (string.IsNullOrWhiteSpace(moduleRootPath))
            return Empty();

        var projectPath = Path.Combine(moduleRootPath, "ModuleData", "project.mbproj");
        if (!File.Exists(projectPath))
            return Empty();

        var moduleSoundFiles = XDocument.Load(projectPath)
            .Descendants("file")
            .Where(e => string.Equals(e.Attribute("type")?.Value, "module_sound", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => Path.Combine(moduleRootPath, NormalizeRelativePath(name)))
            .Where(File.Exists)
            .ToList();

        return LoadFromModuleSoundFiles(moduleRootPath, moduleSoundFiles);
    }

    internal static MusicTrackIndex LoadFromModuleSoundFiles(string moduleRootPath, IEnumerable<string> moduleSoundFiles)
    {
        var tracksByPool = new Dictionary<string, List<MusicTrackDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (moduleSoundFiles == null)
            return new MusicTrackIndex(tracksByPool);

        foreach (var file in moduleSoundFiles)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                continue;

            foreach (var entry in XDocument.Load(file).Descendants("module_sound"))
            {
                var track = TryParseTrack(moduleRootPath, entry);
                if (track == null)
                    continue;

                var key = MakeKey(track.Bucket, track.CultureId);
                if (!tracksByPool.TryGetValue(key, out var list))
                {
                    list = new List<MusicTrackDefinition>();
                    tracksByPool[key] = list;
                }

                list.Add(track);
            }
        }

        foreach (var list in tracksByPool.Values)
            list.Sort((a, b) => string.Compare(a.EventName, b.EventName, StringComparison.OrdinalIgnoreCase));

        return new MusicTrackIndex(tracksByPool);
    }

    internal static MusicTrackIndex LoadFromModuleSoundDocuments(string moduleRootPath, IEnumerable<XDocument> documents)
    {
        var tracksByPool = new Dictionary<string, List<MusicTrackDefinition>>(StringComparer.OrdinalIgnoreCase);
        if (documents == null)
            return new MusicTrackIndex(tracksByPool);

        foreach (var document in documents)
        {
            if (document == null)
                continue;

            foreach (var entry in document.Descendants("module_sound"))
            {
                var track = TryParseTrack(moduleRootPath, entry);
                if (track == null)
                    continue;

                var key = MakeKey(track.Bucket, track.CultureId);
                if (!tracksByPool.TryGetValue(key, out var list))
                {
                    list = new List<MusicTrackDefinition>();
                    tracksByPool[key] = list;
                }

                list.Add(track);
            }
        }

        foreach (var list in tracksByPool.Values)
            list.Sort((a, b) => string.Compare(a.EventName, b.EventName, StringComparison.OrdinalIgnoreCase));

        return new MusicTrackIndex(tracksByPool);
    }

    public MusicTrackPool ResolvePool(MusicBucket bucket, string cultureId)
    {
        var requested = string.IsNullOrWhiteSpace(cultureId) ? NeutralCulture : cultureId.Trim();
        if (TryGetPool(bucket, requested, out var tracks))
            return new MusicTrackPool(bucket, requested, requested, tracks);

        if (!string.Equals(requested, NeutralCulture, StringComparison.OrdinalIgnoreCase)
            && TryGetPool(bucket, NeutralCulture, out tracks))
        {
            return new MusicTrackPool(bucket, requested, NeutralCulture, tracks);
        }

        return new MusicTrackPool(bucket, requested, string.Empty, new List<MusicTrackDefinition>());
    }

    public IReadOnlyList<MusicTrackDefinition> GetTracks(MusicBucket bucket, string cultureId)
    {
        return ResolvePool(bucket, cultureId).Tracks;
    }

    public bool HasPool(MusicBucket bucket, string cultureId)
    {
        return ResolvePool(bucket, cultureId).HasTracks;
    }

    private bool TryGetPool(MusicBucket bucket, string cultureId, out IReadOnlyList<MusicTrackDefinition> tracks)
    {
        if (_tracksByPool.TryGetValue(MakeKey(bucket, cultureId), out var list) && list.Count > 0)
        {
            tracks = list;
            return true;
        }

        tracks = null;
        return false;
    }

    private static MusicTrackDefinition TryParseTrack(string moduleRootPath, XElement entry)
    {
        if (!string.Equals(entry.Attribute("sound_category")?.Value, "music", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.Equals(entry.Attribute("is_2d")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return null;

        var relativePath = NormalizeRelativePath(entry.Attribute("path")?.Value);
        if (!relativePath.StartsWith("taom/", StringComparison.OrdinalIgnoreCase))
            return null;

        var parts = relativePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return null;

        if (!TryParseBucket(parts[1], out var bucket))
            return null;

        var cultureId = parts[2];
        var eventName = entry.Attribute("name")?.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(eventName))
            eventName = Path.ChangeExtension(relativePath, null).Replace('\\', '/');

        var absolutePath = string.IsNullOrWhiteSpace(moduleRootPath)
            ? string.Empty
            : Path.Combine(moduleRootPath, "ModuleSounds", relativePath.Replace('/', Path.DirectorySeparatorChar));

        return new MusicTrackDefinition(bucket, cultureId, eventName, relativePath, absolutePath);
    }

    private static bool TryParseBucket(string value, out MusicBucket bucket)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "worldmap":
                bucket = MusicBucket.World;
                return true;
            case "town_wander":
                bucket = MusicBucket.Town;
                return true;
            case "tavern_wander":
                bucket = MusicBucket.Tavern;
                return true;
            case "battle_music":
                bucket = MusicBucket.Battle;
                return true;
            case "siege_music":
                bucket = MusicBucket.Siege;
                return true;
            case "character_creation":
                bucket = MusicBucket.CharacterCreation;
                return true;
            default:
                bucket = default;
                return false;
        }
    }

    private static string MakeKey(MusicBucket bucket, string cultureId)
    {
        return $"{bucket}:{cultureId ?? string.Empty}";
    }

    private static string NormalizeRelativePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
    }

    private static MusicTrackIndex Empty()
    {
        return new MusicTrackIndex(new Dictionary<string, List<MusicTrackDefinition>>(StringComparer.OrdinalIgnoreCase));
    }
}
