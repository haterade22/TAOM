using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders.Domain;
using TAOM.SceneScripts.Roads;

namespace TAOM.Features.RealmBorders;

/// <summary>
/// Reads <c>ModuleData/realm_borders/palette.json</c> once per process (a singleton: an edit needs a
/// game restart) and builds a fresh <see cref="RealmPalette"/> for every campaign, so each campaign
/// hands out the reserve colours from the best one again. Validated per entry: a malformed colour is
/// skipped with a warning and that realm falls back to the reserve, so one typo cannot blank the map;
/// a missing or unreadable file leaves every realm on the reserve and says so.
/// </summary>
public sealed class RealmPaletteProvider
{
    private readonly IPathService _paths;
    private readonly IModLogger _logger;
    private RealmPaletteConfig? _config;
    private bool _read;

    public RealmPaletteProvider(IPathService paths, IModLogger logger)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>A palette for one campaign: the curated colours, and the whole reserve still to hand out.</summary>
    public RealmPalette NewPalette()
    {
        if (!_read)
        {
            _read = true;
            _config = ReadConfig();
        }
        return Build(_config, _logger);
    }

    private RealmPaletteConfig? ReadConfig()
    {
        var path = Path.Combine(_paths.ModuleDataPath, "realm_borders", "palette.json");
        try
        {
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<RealmPaletteConfig>(File.ReadAllText(path));
            _logger.LogWarning($"[RealmBorders] palette not found at {path}; realms take reserve colours");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[RealmBorders] palette unreadable ({ex.Message}); realms take reserve colours");
        }
        return null;
    }

    internal static RealmPalette Build(RealmPaletteConfig? config, IModLogger logger)
    {
        var curated = new Dictionary<string, uint>(StringComparer.Ordinal);
        var reserve = new List<uint>();
        int rejected = 0;
        if (config != null)
        {
            foreach (var pair in config.Realms ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    rejected++;
                    logger.LogWarning($"[RealmBorders] palette: a blank realm id ('{pair.Key}') was given the colour '{pair.Value}'; skipped");
                }
                else if (TryParseColour(pair.Value, out uint colour))
                    curated[pair.Key] = colour;
                else
                {
                    rejected++;
                    logger.LogWarning($"[RealmBorders] palette: '{pair.Key}' has no valid #RRGGBB colour ('{pair.Value}'); it takes a reserve colour");
                }
            }
            foreach (var hex in config.Reserve ?? new List<string>())
            {
                if (TryParseColour(hex, out uint colour))
                    reserve.Add(colour);
                else
                {
                    rejected++;
                    logger.LogWarning($"[RealmBorders] palette: reserve colour '{hex}' is not #RRGGBB; skipped");
                }
            }
        }
        if (rejected > 0)
            logger.LogWarning($"[RealmBorders] palette: {rejected} value(s) rejected; see the warnings above");
        return new RealmPalette(curated, reserve);
    }

    /// <summary>
    /// Exactly "#" and six hex digits, as the file documents. The shared parser alone would also take
    /// eight digits as RRGGBBAA (so "#FFB0231B", written in the code's ARGB order, would draw orange)
    /// and pad a short byte with whitespace (so "#3F76B " would draw #3F760B), both silently.
    /// </summary>
    internal static bool TryParseColour(string? hex, out uint colour)
    {
        colour = 0;
        if (hex == null || hex.Length != 7 || hex[0] != '#')
            return false;
        for (int i = 1; i < 7; i++)
        {
            if (!Uri.IsHexDigit(hex[i]))
                return false;
        }
        return HexColorParser.TryParse(hex, out colour);
    }
}
