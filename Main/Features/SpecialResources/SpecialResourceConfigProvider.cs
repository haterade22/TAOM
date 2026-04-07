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
    private List<SpecialResource> _resources;
    private Dictionary<string, SpecialResource> _byKingdom;
    private Dictionary<string, TroopResourceCostEntry> _troopCosts;

    public SpecialResourceConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<SpecialResource> GetAllResources()
    {
        EnsureLoaded();
        return _resources;
    }

    public SpecialResource GetByKingdomId(string kingdomId)
    {
        EnsureLoaded();
        return _byKingdom.TryGetValue(kingdomId, out var resource) ? resource : null;
    }

    public TroopResourceCostEntry GetTroopCost(string troopId)
    {
        EnsureLoaded();
        return _troopCosts.TryGetValue(troopId, out var entry) ? entry : null;
    }

    private void EnsureLoaded()
    {
        if (_resources != null)
            return;

        _resources = new List<SpecialResource>();
        _byKingdom = new Dictionary<string, SpecialResource>();
        _troopCosts = new Dictionary<string, TroopResourceCostEntry>();

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
                var resource = new SpecialResource(
                    id: el.Attribute("id")?.Value ?? "",
                    kingdomId: el.Attribute("kingdom_id")?.Value ?? "",
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
                    perHideoutClear: ParseFloat(el, "per_hideout_clear", 0f));

                _resources.Add(resource);

                if (!string.IsNullOrEmpty(resource.KingdomId))
                    _byKingdom[resource.KingdomId] = resource;
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
                var entry = new TroopResourceCostEntry(
                    troopId: el.Attribute("id")?.Value ?? "",
                    resourceId: el.Attribute("resource_id")?.Value ?? "",
                    upgradeCost: (int)ParseFloat(el, "upgrade_cost", 0f),
                    dailyUpkeep: ParseFloat(el, "daily_upkeep", 0f));

                if (!string.IsNullOrEmpty(entry.TroopId))
                    _troopCosts[entry.TroopId] = entry;
            }

            _logger.LogInfo($"SpecialResourceConfigProvider: Loaded {_troopCosts.Count} troop cost entries");
        }
        catch (Exception ex)
        {
            _logger.LogError($"SpecialResourceConfigProvider: Failed to parse troop costs: {ex.Message}");
        }
    }

    private static float ParseFloat(XElement el, string attr, float defaultValue)
    {
        var val = el.Attribute(attr)?.Value;
        return val != null ? float.Parse(val, CultureInfo.InvariantCulture) : defaultValue;
    }
}
