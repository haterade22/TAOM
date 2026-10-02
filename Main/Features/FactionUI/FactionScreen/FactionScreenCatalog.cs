using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.FactionScreen;

/// <summary>
/// The playable factions Kysaro's faction screen lists (#704), built from the faction map's own data
/// (the culture stage hands over the regions and factions it has just loaded; his module re-read TAOM's
/// JSON from a hard-coded <c>Modules/TAOM</c> path) plus his art table and tuning files. A faction is
/// listed when <c>factions.json</c> marks it playable and a region in <c>regions.json</c> belongs to it;
/// the first such region is the one the faction screen confirms, through TAOM's own faction-map
/// selection. Its side and difficulty label are the faction map's own, so a new playable faction needs
/// no code here.
/// </summary>
public sealed class FactionScreenCatalog
{
    private readonly FactionScreenConfigProvider _config;
    private readonly FrontEndSpriteService _sprites;
    private readonly ITextLocalizerAdapter _localizer;
    private readonly IFactionSelectionService _selection;
    private readonly IModLogger _logger;
    private bool _tuningKeysChecked;

    public FactionScreenCatalog(
        FactionScreenConfigProvider config,
        FrontEndSpriteService sprites,
        ITextLocalizerAdapter localizer,
        IFactionSelectionService selection,
        IModLogger logger)
    {
        _config = config;
        _sprites = sprites;
        _localizer = localizer;
        _selection = selection;
        _logger = logger;
    }

    public List<FactionInfo> LoadPlayable(
        IReadOnlyDictionary<string, RegionData> regions,
        IReadOnlyDictionary<string, FactionData> factions)
    {
        var regionByFaction = new Dictionary<string, string>();
        foreach (var region in regions)
        {
            if (!string.IsNullOrEmpty(region.Value.FactionId) && !regionByFaction.ContainsKey(region.Value.FactionId))
                regionByFaction[region.Value.FactionId] = region.Key;
        }

        var config = _config.Config;
        var result = new List<FactionInfo>();
        foreach (var entry in factions)
        {
            var key = entry.Key;
            var data = entry.Value;
            if (!data.Playable || !regionByFaction.TryGetValue(key, out var regionName))
                continue;

            var info = new FactionInfo
            {
                Key = key,
                Name = Localize(data.Name),
                Description = Localize(data.Description),
                RegionName = regionName,
                CultureId = data.GameFaction,
                DifficultyText = Localize(_selection.FormatDifficultyText(data.Difficulty)),
                PaintedPortraitSprite = FactionScreenArt.PaintedPortraits.TryGetValue(key, out var portrait) ? portrait : "",
                PaintedPortraitWidth = FactionScreenArt.PaintedPortraitWidths.TryGetValue(key, out var width) ? width : 640f,
                PaintedPortraitScale = FactionScreenArt.PaintedPortraitScales.TryGetValue(key, out var scale) ? scale : 1f,
                Alignment = data.Side,
                KingdomId = config.Kingdoms.TryGetValue(key, out var kingdom) ? kingdom : "",
                TerritorySprite = _sprites.HasImage("fs_territory_" + key) ? "fs_territory_" + key : "",
                IconSprite = _sprites.HasImage("fs_emblem_" + key) ? "fs_emblem_" + key : FactionScreenArt.DefaultEmblem,
                BackgroundSprite = FactionScreenArt.DefaultBackground,
            };

            var region = regions[regionName];
            if (region.HasCapitalPos)
            {
                info.MapX = region.CapitalX;
                info.MapY = region.CapitalY;
            }
            if (config.MapPositions.TryGetValue(key, out var pin))
            {
                info.MapX = pin.X;
                info.MapY = pin.Y;
            }

            foreach (var bonus in data.Bonuses)
                info.Benefits.Add(new KeyValuePair<string, bool>(Localize(bonus.Text), bonus.Positive));
            info.Strengths.AddRange(data.Strengths.Select(Localize));
            info.Weaknesses.AddRange(data.Weaknesses.Select(Localize));
            result.Add(info);
        }

        ReportTuningKeysThatNameNoFaction(config, new HashSet<string>(result.Select(f => f.Key)));
        return result;
    }

    // TAOM's config-provider rule: a mistyped faction key in one of Kysaro's tuning files would
    // otherwise be ignored without a word. Checked here, where both sets are known; once per process.
    private void ReportTuningKeysThatNameNoFaction(FactionScreenConfig config, HashSet<string> playable)
    {
        if (_tuningKeysChecked)
            return;
        _tuningKeysChecked = true;

        Report(FactionScreenConfigProvider.CharactersFile, config.ViewportCharacters.Keys);
        Report(FactionScreenConfigProvider.KingdomsFile, config.Kingdoms.Keys);
        Report(FactionScreenConfigProvider.MapPositionsFile, config.MapPositions.Keys);
        Report(FactionScreenConfigProvider.ViewportFile, config.ViewportTweaks.Keys);

        void Report(string file, IEnumerable<string> keys)
        {
            var unknown = keys.Where(k => !playable.Contains(k)).ToList();
            if (unknown.Count > 0)
                _logger.LogWarning($"[FactionUI] {file}: {string.Join(", ", unknown)} name no playable faction in factionmap/factions.json; those entries are unused");
        }
    }

    /// <summary>The player's language, with Kysaro's long-dash cleanup for his layout.</summary>
    private string Localize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        return _localizer.Localize(raw)
            .Replace(" — ", ", ").Replace(" —", ",").Replace("— ", ", ").Replace("—", ", ")
            .Replace(" – ", ", ");
    }
}
