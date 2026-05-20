using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

public class CultureMarketplaceConfigProvider : ICultureMarketplaceConfigProvider
{
    private const string ConfigSubfolder = "culture_marketplace";
    private const string ConfigFileName = "culture_marketplace_config.xml";
    private const float MaxWeight = 1000f;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private Dictionary<string, MarketplaceConfigOverride> _byCulture;
    private Dictionary<string, IReadOnlyList<string>> _routing;

    public CultureMarketplaceConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, MarketplaceConfigOverride> GetOverridesByCulture()
    {
        EnsureLoaded();
        return _byCulture;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetItemRouting()
    {
        EnsureLoaded();
        return _routing;
    }

    private void EnsureLoaded()
    {
        if (_byCulture != null) return;

        _byCulture = new Dictionary<string, MarketplaceConfigOverride>(StringComparer.OrdinalIgnoreCase);
        _routing = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var path = Path.Combine(_pathService.ModuleDataPath, ConfigSubfolder, ConfigFileName);
        if (!File.Exists(path))
        {
            _logger.LogInfo($"[CultureMarketplace] No override config at {path} — auto-derive only");
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] Failed to parse {ConfigFileName}: {ex.Message}");
            return;
        }

        var root = doc.Root;
        if (root == null)
        {
            _logger.LogWarning($"[CultureMarketplace] {ConfigFileName} has no root element");
            return;
        }

        var revertedFields = 0;
        foreach (var cultureEl in root.Elements("Culture"))
        {
            var cultureId = cultureEl.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(cultureId))
            {
                _logger.LogWarning("[CultureMarketplace] <Culture> element missing id — skipped");
                continue;
            }

            var blacklist = new HashSet<string>(StringComparer.Ordinal);
            var blacklistEl = cultureEl.Element("Blacklist");
            if (blacklistEl != null)
            {
                foreach (var itemEl in blacklistEl.Elements("Item"))
                {
                    var id = itemEl.Attribute("id")?.Value;
                    if (!string.IsNullOrEmpty(id))
                        blacklist.Add(id);
                }
            }

            var boosts = new Dictionary<string, float>(StringComparer.Ordinal);
            var boostEl = cultureEl.Element("Boost");
            if (boostEl != null)
            {
                foreach (var itemEl in boostEl.Elements("Item"))
                {
                    var id = itemEl.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var weightRaw = itemEl.Attribute("weight")?.Value;
                    if (weightRaw == null)
                    {
                        _logger.LogWarning($"[CultureMarketplace] Boost '{id}' in culture '{cultureId}' missing weight — reverted to 1.0");
                        boosts[id] = 1f;
                        revertedFields++;
                        continue;
                    }

                    if (!float.TryParse(weightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                    {
                        _logger.LogWarning($"[CultureMarketplace] Boost '{id}' weight='{weightRaw}' unparseable — reverted to 1.0");
                        boosts[id] = 1f;
                        revertedFields++;
                        continue;
                    }

                    if (!FiniteFloatValidator.IsFiniteInRange(weight, 0f, MaxWeight))
                    {
                        _logger.LogWarning($"[CultureMarketplace] Boost '{id}' weight={weight} outside [0,{MaxWeight}] or non-finite — reverted to 1.0");
                        boosts[id] = 1f;
                        revertedFields++;
                        continue;
                    }

                    boosts[id] = weight;
                }
            }

            _byCulture[cultureId] = new MarketplaceConfigOverride(cultureId, blacklist, boosts);
        }

        // Parse <Routing> — top-level cross-culture item routing (e.g., wargs into 4 cultures).
        var routingEl = root.Element("Routing");
        if (routingEl != null)
        {
            foreach (var itemEl in routingEl.Elements("Item"))
            {
                var itemId = itemEl.Attribute("id")?.Value;
                var culturesRaw = itemEl.Attribute("cultures")?.Value;
                if (string.IsNullOrEmpty(itemId))
                {
                    _logger.LogWarning("[CultureMarketplace] <Routing>/<Item> missing id — skipped");
                    continue;
                }
                if (string.IsNullOrEmpty(culturesRaw))
                {
                    _logger.LogWarning($"[CultureMarketplace] <Routing>/<Item id='{itemId}'> missing cultures — skipped");
                    continue;
                }
                var cultureIds = new List<string>();
                foreach (var raw in culturesRaw.Split(','))
                {
                    var trimmed = raw.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        cultureIds.Add(trimmed);
                }
                if (cultureIds.Count == 0)
                {
                    _logger.LogWarning($"[CultureMarketplace] <Routing>/<Item id='{itemId}'> cultures='{culturesRaw}' parsed to empty list — skipped");
                    continue;
                }
                _routing[itemId] = cultureIds;
            }
        }

        if (revertedFields > 0)
            _logger.LogWarning($"[CultureMarketplace] {revertedFields} override field(s) reverted to defaults — review prior warnings");

        _logger.LogInfo($"[CultureMarketplace] Loaded overrides for {_byCulture.Count} culture(s); {_routing.Count} cross-culture item route(s)");
    }
}
