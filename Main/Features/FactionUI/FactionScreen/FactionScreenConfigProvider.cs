using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// Loads Kysaro's four faction-screen files from <c>ModuleData/FactionUI/</c> (#704) and validates every
/// entry (TAOM's config-provider rule): keys starting with <c>_</c> are notes; an id must be a non-empty
/// string; a map position two finite numbers in [0, 1]; a viewport offset a finite number in
/// [-1000, 1000], a race a whole number in [0, 255], and a viewport object may hold only
/// <c>offset</c>, <c>hide_weapons</c> and <c>race</c>. A bad entry is skipped with a warning, the rest
/// still load, and a summary warning follows. Read once per process. Keys that name no playable
/// faction are reported by <see cref="FactionScreenCatalog"/>, which knows the factions.
/// </summary>
public sealed class FactionScreenConfigProvider
{
    public const string CharactersFile = "faction_characters.json";
    public const string KingdomsFile = "faction_kingdoms.json";
    public const string MapPositionsFile = "faction_map_pos.json";
    public const string ViewportFile = "faction_viewport.json";

    internal const float MaxViewportOffset = 1000f;
    internal const int MaxRace = 255;

    private static readonly HashSet<string> ViewportProperties = new(StringComparer.Ordinal) { "offset", "hide_weapons", "race" };

    private readonly IFrontEndResourceAdapter _adapter;
    private readonly FactionUIPaths _paths;
    private readonly IModLogger _logger;
    private FactionScreenConfig? _config;
    private int _skipped;

    public FactionScreenConfigProvider(IFrontEndResourceAdapter adapter, FactionUIPaths paths, IModLogger logger)
    {
        _adapter = adapter;
        _paths = paths;
        _logger = logger;
    }

    public FactionScreenConfig Config => _config ??= Load();

    private FactionScreenConfig Load()
    {
        var config = new FactionScreenConfig(
            ReadIds(CharactersFile),
            ReadIds(KingdomsFile),
            ReadMapPositions(),
            ReadViewportTweaks());
        if (_skipped > 0)
            _logger.LogWarning($"[FactionUI] {_skipped} entries in ModuleData/FactionUI/faction_*.json were skipped; see the warnings above");
        return config;
    }

    private Dictionary<string, string> ReadIds(string file)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in Entries(file))
        {
            var id = property.Value.Type == JTokenType.String ? (string?)property.Value : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                Skip(file, property, "is not a non-empty id");
                continue;
            }
            result[property.Name] = id!.Trim();
        }
        return result;
    }

    private Dictionary<string, (double X, double Y)> ReadMapPositions()
    {
        var result = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        foreach (var property in Entries(MapPositionsFile))
        {
            if (property.Value is JArray { Count: 2 } pair
                && TryNumber(pair[0], out var x) && FiniteFloatValidator.IsFiniteInRange(x, 0.0, 1.0)
                && TryNumber(pair[1], out var y) && FiniteFloatValidator.IsFiniteInRange(y, 0.0, 1.0))
            {
                result[property.Name] = (x, y);
                continue;
            }
            Skip(MapPositionsFile, property, "is not [x, y] with both in 0 to 1");
        }
        return result;
    }

    private Dictionary<string, ViewportTweak> ReadViewportTweaks()
    {
        var result = new Dictionary<string, ViewportTweak>(StringComparer.Ordinal);
        foreach (var property in Entries(ViewportFile))
        {
            var tweak = property.Value is JObject entry ? ReadTweak(entry) : ReadOffsetOnly(property.Value);
            if (tweak == null)
            {
                Skip(ViewportFile, property, $"needs only an offset in [-{MaxViewportOffset}, {MaxViewportOffset}], a true/false hide_weapons and a race in [0, {MaxRace}]");
                continue;
            }
            result[property.Name] = tweak;
        }
        return result;
    }

    private static ViewportTweak? ReadOffsetOnly(JToken token) =>
        TryOffset(token, out var offset) ? new ViewportTweak(offset, hideWeapons: false, race: null) : null;

    private static ViewportTweak? ReadTweak(JObject entry)
    {
        if (entry.Properties().Any(p => !ViewportProperties.Contains(p.Name)))
            return null;

        var offset = 0f;
        if (entry["offset"] is { } offsetToken && !TryOffset(offsetToken, out offset))
            return null;

        var hideWeapons = false;
        if (entry["hide_weapons"] is { } hideToken)
        {
            if (hideToken.Type != JTokenType.Boolean)
                return null;
            hideWeapons = hideToken.Value<bool>();
        }

        int? race = null;
        if (entry["race"] is { } raceToken)
        {
            if (raceToken.Type != JTokenType.Integer)
                return null;
            var value = raceToken.Value<long>();
            if (value < 0 || value > MaxRace)
                return null;
            race = (int)value;
        }

        return new ViewportTweak(offset, hideWeapons, race);
    }

    private static bool TryOffset(JToken token, out float offset)
    {
        offset = 0f;
        if (!TryNumber(token, out var value) || !FiniteFloatValidator.IsFiniteInRange(value, -MaxViewportOffset, MaxViewportOffset))
            return false;
        offset = (float)value;
        return true;
    }

    private static bool TryNumber(JToken token, out double value)
    {
        value = 0;
        if (token.Type is not (JTokenType.Float or JTokenType.Integer))
            return false;
        value = token.Value<double>();
        return true;
    }

    private IEnumerable<JProperty> Entries(string file)
    {
        var text = _adapter.ReadAllText(Path.Combine(_paths.ConfigDirectory, file));
        if (text == null)
            return Array.Empty<JProperty>();

        try
        {
            var properties = new List<JProperty>();
            foreach (var property in JObject.Parse(text).Properties())
            {
                if (!property.Name.StartsWith("_", StringComparison.Ordinal))
                    properties.Add(property);
            }
            return properties;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning($"[FactionUI] {file} is not valid JSON and was ignored: {ex.Message}");
            return Array.Empty<JProperty>();
        }
    }

    private void Skip(string file, JProperty property, string reason)
    {
        _skipped++;
        _logger.LogWarning($"[FactionUI] {file}: \"{property.Name}\" {reason}; entry skipped");
    }
}
