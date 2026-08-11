using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EliteEmissary.Domain;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.EliteEmissary;

/// <summary>
/// Loads + validates <c>elite_emissary/elite_emissary_config.xml</c> (Config-Providers-MUST-Validate,
/// csharp-architecture.md). Missing/malformed → <see cref="EliteEmissaryConfig.Empty"/>. Validation:
/// a <c>&lt;Culture&gt;</c> id not in the known culture set is dropped+warned (a typo would otherwise
/// silently produce "this faction has no elites" — the M1 trap); a <c>&lt;Troop&gt;</c> with no
/// <c>merchant_cost</c> row in troop_resource_costs.xml is dropped+warned (it would be unsellable).
/// Key-settlement ids can't be validated here (MBObjectManager isn't populated at load) — the behavior
/// validates them against live settlements at session launch.
/// </summary>
public sealed class EliteEmissaryConfigProvider : IEliteEmissaryConfigProvider
{
    // Owner-culture StringIds an emissary offer block may be keyed by. Source: CLAUDE.md / xml-data.md
    // culture table (custom LOTR cultures + XSLT engine-id cultures + the two orc-host cultures).
    private static readonly HashSet<string> KnownCultureIds = new(StringComparer.Ordinal)
    {
        "gondor", "mordor", "erebor", "rivendell", "lothlorien", "mirkwood",
        "isengard", "gundabad", "dolguldur", "umbar", "goblin", "mistymountainorcs",
        "bluecraig", "lindon",
        "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania",
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly ISpecialResourceConfigProvider _resourceConfig;
    private readonly Lazy<EliteEmissaryConfig> _config;

    public EliteEmissaryConfigProvider(IPathService pathService, IModLogger logger, ISpecialResourceConfigProvider resourceConfig)
    {
        _pathService = pathService;
        _logger = logger;
        _resourceConfig = resourceConfig;
        _config = new Lazy<EliteEmissaryConfig>(LoadConfig);
    }

    public EliteEmissaryConfig GetConfig() => _config.Value;

    private EliteEmissaryConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "elite_emissary", "elite_emissary_config.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"EliteEmissaryConfigProvider: config not found at {path} — Elite Emissary inert");
            return EliteEmissaryConfig.Empty;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception ex)
        {
            _logger.LogError($"EliteEmissaryConfigProvider: failed to parse elite_emissary_config.xml: {ex.Message}");
            return EliteEmissaryConfig.Empty;
        }

        try
        {
            var root = doc.Root;
            var enabled = ParseBool(root?.Attribute("enabled")?.Value, true);

            var keySettlements = new HashSet<string>(StringComparer.Ordinal);
            var keyEl = root?.Element("KeySettlements");
            if (keyEl != null)
            {
                foreach (var s in keyEl.Elements("Settlement"))
                {
                    var id = s.Attribute("id")?.Value;
                    if (!string.IsNullOrWhiteSpace(id))
                        keySettlements.Add(id.Trim());
                }
            }

            var cultureOffers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var offersEl = root?.Element("CultureOffers");
            int droppedTroops = 0, droppedCultures = 0;
            if (offersEl != null)
            {
                foreach (var c in offersEl.Elements("Culture"))
                {
                    var cultureId = c.Attribute("id")?.Value?.Trim();
                    if (string.IsNullOrEmpty(cultureId))
                    {
                        _logger.LogWarning("EliteEmissaryConfigProvider: <Culture> with empty id — skipped");
                        continue;
                    }
                    if (!KnownCultureIds.Contains(cultureId))
                    {
                        _logger.LogWarning($"EliteEmissaryConfigProvider: unknown culture id '{cultureId}' — dropped (offers for it would never be reachable)");
                        droppedCultures++;
                        continue;
                    }
                    // A known culture that maps to NO special resource (goblin, mistymountainorcs) would
                    // pass the merchant_cost checks but be silently dead at runtime — ResolveResource
                    // returns null and HideWhenNoResource hides the option. Drop+warn at load instead of
                    // shipping a dead offer block (the parsed-but-unresolvable M1 trap; Codex review 2026-06-25 [MED]).
                    if (_resourceConfig.GetByCultureId(cultureId) == null)
                    {
                        _logger.LogWarning($"EliteEmissaryConfigProvider: culture '{cultureId}' maps to no special resource in special_resources_config.xml — dropped (its offers would be silently unreachable)");
                        droppedCultures++;
                        continue;
                    }
                    if (cultureOffers.ContainsKey(cultureId))
                    {
                        _logger.LogWarning($"EliteEmissaryConfigProvider: duplicate <Culture id='{cultureId}'> — keeping the first block, ignoring the rest");
                        continue;
                    }

                    var troops = new List<string>();
                    foreach (var t in c.Elements("Troop"))
                    {
                        var troopId = t.Attribute("id")?.Value?.Trim();
                        if (string.IsNullOrEmpty(troopId))
                            continue;

                        var cost = _resourceConfig.GetTroopCost(troopId);
                        if (cost == null || cost.MerchantCost <= 0)
                        {
                            _logger.LogWarning($"EliteEmissaryConfigProvider: offer '{troopId}' (culture {cultureId}) has no merchant_cost in troop_resource_costs.xml — dropped (unsellable)");
                            droppedTroops++;
                            continue;
                        }
                        troops.Add(troopId);
                    }

                    if (troops.Count > 0)
                        cultureOffers[cultureId] = troops;
                    else
                        _logger.LogWarning($"EliteEmissaryConfigProvider: culture '{cultureId}' has no sellable offers after validation — not recorded");
                }
            }

            _logger.LogInfo($"EliteEmissaryConfigProvider: loaded {keySettlements.Count} key settlement(s), {cultureOffers.Count} culture offer list(s) (enabled={enabled}; dropped {droppedCultures} culture(s), {droppedTroops} troop(s))");
            return new EliteEmissaryConfig(enabled, keySettlements, cultureOffers);
        }
        catch (Exception ex)
        {
            _logger.LogError($"EliteEmissaryConfigProvider: failed to parse elite_emissary_config.xml: {ex.Message}");
            return EliteEmissaryConfig.Empty;
        }
    }

    private static bool ParseBool(string value, bool defaultValue) =>
        bool.TryParse(value, out var b) ? b : defaultValue;
}
