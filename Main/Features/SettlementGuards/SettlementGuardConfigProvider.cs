using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards.Domain;

namespace TAOM.Features.SettlementGuards;

public class SettlementGuardConfigProvider : ISettlementGuardConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private Dictionary<string, GuardPool> _bySettlement;
    private Dictionary<string, GuardPool> _byClan;
    private Dictionary<string, GuardPool> _byCulture;
    private Dictionary<string, string> _spearMappings;

    public SettlementGuardConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public GuardPool GetBySettlementId(string settlementId)
    {
        EnsureLoaded();
        return settlementId != null && _bySettlement.TryGetValue(settlementId, out var pool) ? pool : null;
    }

    public GuardPool GetByClanId(string clanId)
    {
        EnsureLoaded();
        return clanId != null && _byClan.TryGetValue(clanId, out var pool) ? pool : null;
    }

    public GuardPool GetByCultureId(string cultureId)
    {
        EnsureLoaded();
        return cultureId != null && _byCulture.TryGetValue(cultureId, out var pool) ? pool : null;
    }

    public string GetSpearItemId(string cultureId)
    {
        EnsureLoaded();
        return cultureId != null && _spearMappings.TryGetValue(cultureId, out var itemId) ? itemId : null;
    }

    public IReadOnlyDictionary<string, string> GetSpearMappings()
    {
        EnsureLoaded();
        return _spearMappings;
    }

    private void EnsureLoaded()
    {
        if (_bySettlement != null)
            return;

        _bySettlement = new Dictionary<string, GuardPool>();
        _byClan = new Dictionary<string, GuardPool>();
        _byCulture = new Dictionary<string, GuardPool>();
        _spearMappings = new Dictionary<string, string>();

        LoadConfig();
    }

    private void LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "settlement_guards", "settlement_guards_config.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"SettlementGuardConfigProvider: Config not found: {path}");
            return;
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;

            foreach (var el in root.Elements("Settlement"))
            {
                var id = el.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                _bySettlement[id] = ParseGuardPool(el);
            }

            foreach (var el in root.Elements("Clan"))
            {
                var id = el.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                _byClan[id] = ParseGuardPool(el);
            }

            foreach (var el in root.Elements("Culture"))
            {
                var id = el.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;
                _byCulture[id] = ParseGuardPool(el);
            }

            var spearsEl = root.Element("Spears");
            if (spearsEl != null)
            {
                foreach (var el in spearsEl.Elements("Spear"))
                {
                    var culture = el.Attribute("culture")?.Value;
                    var item = el.Attribute("item")?.Value;
                    if (!string.IsNullOrEmpty(culture) && !string.IsNullOrEmpty(item))
                        _spearMappings[culture] = item;
                }
            }

            _logger.LogInfo($"SettlementGuardConfigProvider: Loaded {_bySettlement.Count} settlements, " +
                            $"{_byClan.Count} clans, {_byCulture.Count} cultures, {_spearMappings.Count} spear mappings");
        }
        catch (Exception ex)
        {
            _logger.LogError($"SettlementGuardConfigProvider: Failed to parse config: {ex.Message}");
        }
    }

    private static GuardPool ParseGuardPool(XElement parent)
    {
        var guards = new List<GuardEntry>();
        var guardsEl = parent.Element("Guards");

        if (guardsEl != null)
        {
            foreach (var g in guardsEl.Elements("Guard"))
            {
                var troopId = g.Attribute("troop")?.Value;
                if (string.IsNullOrEmpty(troopId)) continue;

                var weight = 1;
                var weightAttr = g.Attribute("weight")?.Value;
                if (weightAttr != null && int.TryParse(weightAttr, out var w))
                    weight = w;

                var spawnPoints = new List<string>();
                var spawnPointsAttr = g.Attribute("spawn_points")?.Value;
                if (!string.IsNullOrEmpty(spawnPointsAttr))
                {
                    foreach (var sp in spawnPointsAttr.Split(','))
                    {
                        var trimmed = sp.Trim();
                        if (trimmed.Length > 0)
                            spawnPoints.Add(trimmed);
                    }
                }

                guards.Add(new GuardEntry(troopId, weight, spawnPoints));
            }
        }

        var prisonGuardId = parent.Element("PrisonGuard")?.Attribute("troop")?.Value;

        return new GuardPool(guards, prisonGuardId);
    }
}
