using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceConfigProvider : ISpecialResourceConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly ISpecialResourceSettingsProvider _settings;
    private List<SpecialResource> _resources;
    private Dictionary<string, SpecialResource> _byKingdom;
    private Dictionary<string, SpecialResource> _byCulture;
    private Dictionary<string, SpecialResource> _byId;
    private Dictionary<string, TroopResourceCostEntry> _troopCosts;
    private Dictionary<string, TroopResourceCostEntry> _goldOnlyTroopCosts;

    public SpecialResourceConfigProvider(IPathService pathService, IModLogger logger, ISpecialResourceSettingsProvider settings = null)
    {
        _pathService = pathService;
        _logger = logger;
        _settings = settings;
    }

    public IReadOnlyList<SpecialResource> GetAllResources()
    {
        EnsureLoaded();
        return _resources;
    }

    public SpecialResource? GetByKingdomId(string? kingdomId)
    {
        EnsureLoaded();
        return kingdomId != null && _byKingdom.TryGetValue(kingdomId, out var resource) ? resource : null;
    }

    public SpecialResource? GetByCultureId(string? cultureId)
    {
        EnsureLoaded();
        return cultureId != null && _byCulture.TryGetValue(cultureId, out var resource) ? resource : null;
    }

    public SpecialResource? GetById(string resourceId)
    {
        EnsureLoaded();
        return resourceId != null && _byId.TryGetValue(resourceId, out var resource) ? resource : null;
    }

    public TroopResourceCostEntry GetTroopCost(string troopId)
    {
        EnsureLoaded();
        var costs = _settings?.EliteTroopsCostGoldOnly == true ? _goldOnlyTroopCosts : _troopCosts;
        return costs.TryGetValue(troopId, out var entry) ? entry : null;
    }

    private void EnsureLoaded()
    {
        if (_resources != null)
            return;

        _resources = new List<SpecialResource>();
        _byKingdom = new Dictionary<string, SpecialResource>();
        _byCulture = new Dictionary<string, SpecialResource>();
        _byId = new Dictionary<string, SpecialResource>();
        _troopCosts = new Dictionary<string, TroopResourceCostEntry>();
        _goldOnlyTroopCosts = new Dictionary<string, TroopResourceCostEntry>();

        LoadResources();
        LoadTroopCosts();
    }

    private void LoadResources()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "special_resources", "special_resources_config.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SpecialResourceConfigProvider: Config not found: {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            foreach (var el in doc.Root.Elements("Resource"))
            {
                var kingdomIds = new List<string>();
                foreach (var k in el.Elements("Kingdom"))
                {
                    var kid = k.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(kid))
                        kingdomIds.Add(kid);
                }

                var cultureIds = new List<string>();
                foreach (var c in el.Elements("Culture"))
                {
                    var cid = c.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(cid))
                        cultureIds.Add(cid);
                }

                var tiers = ParseTiers(el);
                var (battleReference, battleMin, battleMax) = ParseBattleScale(el);

                var resource = new SpecialResource(
                    id: el.Attribute("id")?.Value ?? "",
                    kingdomIds: kingdomIds,
                    cultureIds: cultureIds,
                    displayName: el.Attribute("display_name")?.Value ?? "",
                    iconSpriteName: el.Attribute("icon_sprite")?.Value ?? "",
                    cap: ParseFloat(el, "cap", 100f),
                    startingAmount: ParseFloat(el, "starting_amount", 0f),
                    dailyPerTown: ParseFloat(el, "daily_per_town", 0f),
                    perBattleVictoryBase: ParseFloat(el, "per_battle_victory_base", 0f),
                    perRaid: ParseFloat(el, "per_raid", 0f),
                    perSiegeVictory: ParseFloat(el, "per_siege_victory", 0f),
                    perPrisoner: ParseFloat(el, "per_prisoner", 0f),
                    perTournamentWin: ParseFloat(el, "per_tournament_win", 0f),
                    perHideoutClear: ParseFloat(el, "per_hideout_clear", 0f),
                    tierThresholds: tiers,
                    battleReferenceSize: battleReference,
                    battleMinScale: battleMin,
                    battleMaxScale: battleMax,
                    emissaryPriceMultiplier: ParseEmissaryPriceMultiplier(el));

                _resources.Add(resource);

                foreach (var kid in kingdomIds)
                    _byKingdom[kid] = resource;
                foreach (var cid in cultureIds)
                    _byCulture[cid] = resource;
                if (!string.IsNullOrEmpty(resource.Id))
                    _byId[resource.Id] = resource;
            }

            _logger.LogInfo($"SpecialResourceConfigProvider: Loaded {_resources.Count} resource definitions");
        }
        catch (Exception ex)
        {
            _logger.LogError($"SpecialResourceConfigProvider: Failed to parse resources: {ex.Message}");
        }
    }

    private void LoadTroopCosts()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "special_resources", "troop_resource_costs.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SpecialResourceConfigProvider: Troop costs not found: {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            foreach (var el in doc.Root.Elements("Troop"))
            {
                var resourceId = el.Attribute("resource_id")?.Value ?? "";
                var merchantCost = (int)ParseFloat(el, "merchant_cost", 0f);
                // The emissary's resource price follows the TROOP's own resource, never the settlement owner's:
                // the owner's resource comes from a kingdom-first lookup that can differ from the culture whose
                // troops the emissary offers (a defected clan), and the price must not move with it. A row that
                // names no known resource keeps its raw price.
                var multiplier = _byId.TryGetValue(resourceId, out var priced) ? priced.EmissaryPriceMultiplier : 1;
                var entry = new TroopResourceCostEntry(
                    troopId: el.Attribute("id")?.Value ?? "",
                    resourceId: resourceId,
                    upgradeCost: (int)ParseFloat(el, "upgrade_cost", 0f),
                    dailyUpkeep: ParseFloat(el, "daily_upkeep", 0f),
                    recruitCost: (int)ParseFloat(el, "recruit_cost", 0f),
                    merchantCost: merchantCost,
                    emissaryPrice: (int)Math.Min((long)merchantCost * multiplier, int.MaxValue));

                if (!string.IsNullOrEmpty(entry.TroopId))
                {
                    _troopCosts[entry.TroopId] = entry;
                    _goldOnlyTroopCosts[entry.TroopId] = entry.AsMerchantOnly();
                }
            }

            _logger.LogInfo($"SpecialResourceConfigProvider: Loaded {_troopCosts.Count} troop cost entries");
        }
        catch (Exception ex)
        {
            _logger.LogError($"SpecialResourceConfigProvider: Failed to parse troop costs: {ex.Message}");
        }
    }

    private static List<ResourceTier> ParseTiers(XElement resourceEl)
    {
        var tiersEl = resourceEl.Element("Tiers");
        if (tiersEl == null)
            return new List<ResourceTier>();

        var tiers = new List<ResourceTier>();
        foreach (var tierEl in tiersEl.Elements("Tier"))
        {
            var levelStr = tierEl.Attribute("level")?.Value;
            var name = tierEl.Attribute("name")?.Value ?? "";
            var thresholdStr = tierEl.Attribute("threshold")?.Value;
            var description = tierEl.Attribute("description")?.Value ?? "";

            if (levelStr == null || thresholdStr == null)
                continue;

            if (!int.TryParse(levelStr, out var level))
                continue;

            if (!float.TryParse(thresholdStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold))
                continue;

            tiers.Add(new ResourceTier(level, name, threshold, description));
        }

        tiers.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));
        return tiers;
    }

    // The three battle-scale attributes are optional. A set that breaks an invariant (reference not above 0,
    // a negative floor, a ceiling below the floor) falls back to the defaults together, with one warning,
    // because a half-valid set could invert the scale. ParseFloat already drops NaN and Infinity.
    private (float Reference, float Min, float Max) ParseBattleScale(XElement el)
    {
        var reference = ParseFloat(el, "battle_reference_size", SpecialResource.DefaultBattleReferenceSize);
        var min = ParseFloat(el, "battle_min_scale", SpecialResource.DefaultBattleMinScale);
        var max = ParseFloat(el, "battle_max_scale", SpecialResource.DefaultBattleMaxScale);

        if (reference > 0f && min >= 0f && max >= min)
            return (reference, min, max);

        _logger.LogWarning(
            $"SpecialResourceConfigProvider: Resource '{el.Attribute("id")?.Value}' has an invalid battle scale " +
            $"(reference {reference}, min {min}, max {max}); using the defaults.");
        return (SpecialResource.DefaultBattleReferenceSize, SpecialResource.DefaultBattleMinScale,
            SpecialResource.DefaultBattleMaxScale);
    }

    // Optional whole number from 1 to MaxEmissaryPriceMultiplier. Anything else (zero or negative, a fraction,
    // too large, not a number) falls back to 1 with a warning, so a typo never makes troops free or unaffordable.
    private int ParseEmissaryPriceMultiplier(XElement el)
    {
        const string attr = "emissary_price_multiplier";
        var raw = el.Attribute(attr)?.Value;
        if (raw == null) return SpecialResource.DefaultEmissaryPriceMultiplier;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value >= 1 && value <= SpecialResource.MaxEmissaryPriceMultiplier)
            return value;

        _logger.LogWarning(
            $"SpecialResourceConfigProvider: Resource '{el.Attribute("id")?.Value}' has an invalid {attr} '{raw}' " +
            $"(expected a whole number from 1 to {SpecialResource.MaxEmissaryPriceMultiplier}); using {SpecialResource.DefaultEmissaryPriceMultiplier}.");
        return SpecialResource.DefaultEmissaryPriceMultiplier;
    }

    private static float ParseFloat(XElement el, string attr, float defaultValue)
    {
        var val = el.Attribute(attr)?.Value;
        if (val == null) return defaultValue;
        // Phase 9b #133 P1 — was `float.Parse` (throws on malformed value, bubbles to outer catch
        // → silently zeroes ALL resources for the file). Use TryParse + NaN/Infinity guard.
        if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return defaultValue;
        if (float.IsNaN(result) || float.IsInfinity(result))
            return defaultValue;
        return result;
    }
}
