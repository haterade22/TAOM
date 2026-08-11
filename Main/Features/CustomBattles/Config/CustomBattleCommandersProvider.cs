using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.CustomBattles.Config;

/// <summary>
/// Loads + validates <c>custom_battle/custom_battle_commanders.json</c>. Mirrors
/// <c>CultureConversionConfigProvider</c>: <see cref="Lazy{T}"/> singleton, fail-safe semantic
/// validation (each violation logged; a faction that fails validation is dropped so it falls back
/// to the default alphabetical top-N behavior). Reuse.Singleton — edits require a full game
/// restart, not a save-load.
///
/// Validation split: faction keys + id syntax (empty/whitespace, duplicates, unknown-key warning)
/// are validated here at load. Whether each id RESOLVES to a real <c>BasicCharacterObject</c>
/// cannot be checked here (needs the live <c>MBObjectManager</c>); those are warned + skipped at
/// resolution time in <c>SideCommanderFilter.ResolveCommandersForCulture</c>.
/// </summary>
public class CustomBattleCommandersProvider : ICustomBattleCommandersProvider
{
    // Warning-only aid: a faction key not in this set is kept but flagged (likely a typo — a real
    // faction with that id would otherwise silently fall back to default). Stale set => spurious or
    // missing warning, never broken behavior. Mirrors ConfigIdValidationTests.ValidCultureIds.
    private static readonly HashSet<string> KnownCultureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gondor", "mordor", "erebor", "rivendell", "lothlorien",
        "mirkwood", "isengard", "gundabad", "dolguldur", "umbar",
        "shaghana", "abanissa", "goblin", "mistymountainorcs",
        "bluecraig", "lindon",
        "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania"
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<Dictionary<string, IReadOnlyList<string>>> _factions;

    public CustomBattleCommandersProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _factions = new Lazy<Dictionary<string, IReadOnlyList<string>>>(Load);
    }

    public bool HasCuratedEntry(string factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return false;
        return _factions.Value.ContainsKey(factionId);
    }

    public IReadOnlyList<string> GetCuratedCommanderIds(string factionId)
    {
        if (!string.IsNullOrWhiteSpace(factionId) && _factions.Value.TryGetValue(factionId, out var ids))
            return ids;
        return Array.Empty<string>();
    }

    private Dictionary<string, IReadOnlyList<string>> Load()
    {
        var empty = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(_pathService.ModuleDataPath, "custom_battle", "custom_battle_commanders.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CustomBattleCommandersProvider: custom_battle_commanders.json not found at {path}, all factions use default commander selection");
            return empty;
        }

        CustomBattleCommandersConfig parsed;
        try
        {
            var json = File.ReadAllText(path);
            parsed = JsonConvert.DeserializeObject<CustomBattleCommandersConfig>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CustomBattleCommandersProvider: Failed to parse custom_battle_commanders.json: {ex.Message}");
            return empty;
        }

        if (parsed?.Factions == null)
        {
            _logger.LogWarning("CustomBattleCommandersProvider: custom_battle_commanders.json has no 'factions' map, all factions use default commander selection");
            return empty;
        }

        return Validate(parsed);
    }

    private Dictionary<string, IReadOnlyList<string>> Validate(CustomBattleCommandersConfig parsed)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var rejected = false;

        foreach (var entry in parsed.Factions)
        {
            var factionId = entry.Key;

            if (string.IsNullOrWhiteSpace(factionId))
            {
                _logger.LogWarning("CustomBattleCommandersProvider: empty/whitespace faction key skipped");
                rejected = true;
                continue;
            }

            if (!KnownCultureIds.Contains(factionId))
            {
                _logger.LogWarning($"CustomBattleCommandersProvider: faction key '{factionId}' is not a known culture id — entry kept but it will only take effect if a faction with that id exists (check for a typo)");
                rejected = true;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ids = new List<string>();
            var rawIds = entry.Value ?? new List<string>();

            foreach (var rawId in rawIds)
            {
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    _logger.LogWarning($"CustomBattleCommandersProvider: empty/whitespace commander id skipped in faction '{factionId}'");
                    rejected = true;
                    continue;
                }

                var id = rawId.Trim();
                if (!seen.Add(id))
                {
                    _logger.LogWarning($"CustomBattleCommandersProvider: duplicate commander id '{id}' in faction '{factionId}' dropped (first occurrence kept)");
                    rejected = true;
                    continue;
                }

                ids.Add(id);
            }

            if (ids.Count == 0)
            {
                _logger.LogWarning($"CustomBattleCommandersProvider: faction '{factionId}' has no valid commander ids after validation — falling back to default selection");
                rejected = true;
                continue;
            }

            result[factionId] = ids;
        }

        if (rejected)
            _logger.LogWarning("CustomBattleCommandersProvider: custom_battle_commanders.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"CustomBattleCommandersProvider: Loaded {result.Count} curated faction(s)");

        return result;
    }
}
