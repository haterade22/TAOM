using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MonsterSize;

/// <summary>
/// Makes each Monster's <see cref="MonsterSizeConfig.AttributeName"/> the size of every mount built on it, by writing it
/// into the body_length of the Horse items that name the Monster (docs/features/monster-size.md). The attribute is
/// hand-edited XML, so a value that is not a whole number in range is refused with a warning (and a summary warning
/// counting the refusals), and its mounts keep their items' own body_length. A size no Horse item's Monster matches
/// (a typo'd or renamed id) is warned too, since the summary lists only the items it changed. A failure part-way is
/// logged with the items already written, which keep their new size; the rest keep what the XML built.
/// </summary>
public class MonsterSizeService : IMonsterSizeService
{
    private readonly IMonsterSizeCatalogAdapter _catalog;
    private readonly IModLogger _logger;

    public MonsterSizeService(IMonsterSizeCatalogAdapter catalog, IModLogger logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    /// <summary>The one parsing rule for <see cref="MonsterSizeConfig.AttributeName"/>, shared with the data tests: digits
    /// only after trimming (no sign, decimal point, exponent or separator), invariant culture, and
    /// <see cref="MonsterSizeConfig.MinBodyLength"/> to <see cref="MonsterSizeConfig.MaxBodyLength"/> inclusive.</summary>
    public static bool TryParseBodyLength(string? raw, out int bodyLength)
        => int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out bodyLength)
           && bodyLength >= MonsterSizeConfig.MinBodyLength && bodyLength <= MonsterSizeConfig.MaxBodyLength;

    public int ApplyMonsterSizes()
    {
        var written = new List<string>();
        try
        {
            Dictionary<string, int> sizes = ParseSizes(_catalog.ReadDeclaredSizes());
            if (sizes.Count == 0)
                return 0;

            var matched = new HashSet<string>(StringComparer.Ordinal);
            foreach (HorseItemRecord item in _catalog.ReadHorseItems())
            {
                // The item's own value is the placeholder Items.xsd forces on it (MonsterSizeConfig.ItemPlaceholderBodyLength),
                // so overriding it is the normal path; the summary keeps the old value for anyone reading the log.
                if (item.MonsterId == null || !sizes.TryGetValue(item.MonsterId, out int size))
                    continue;
                matched.Add(item.MonsterId);
                if (item.BodyLength == size)
                    continue;
                if (_catalog.SetBodyLength(item.ItemId, size))
                    written.Add($"{item.ItemId}={size} (was {item.BodyLength})");
                else
                    _logger.LogError($"[MonsterSize] Could not set {item.ItemId}'s body_length to {size}; it keeps {item.BodyLength} " +
                                     "(HorseComponent.BodyLength setter missing after an engine update?)");
            }

            foreach (string monsterId in sizes.Keys)
            {
                if (!matched.Contains(monsterId))
                    _logger.LogWarning($"[MonsterSize] Monster {monsterId} declares {MonsterSizeConfig.AttributeName}=\"{sizes[monsterId]}\" but no Horse " +
                                       "item names it (Monster ids are case-sensitive); nothing was resized for it.");
            }
            _logger.LogInfo($"[MonsterSize] {sizes.Count} Monster size(s), {written.Count} Horse item(s) resized: {string.Join(", ", written)}");
            return written.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MonsterSize] Applying Monster sizes stopped: {ex.GetType().Name}: {ex.Message}. " +
                             $"{written.Count} item(s) written before it keep their new size ({string.Join(", ", written)}); " +
                             "every other mount keeps its item's body_length.");
            return written.Count;
        }
    }

    /// <summary>Ids compare ordinally, as the engine resolves them; a Monster with no id is skipped.</summary>
    private Dictionary<string, int> ParseSizes(IReadOnlyList<KeyValuePair<string, string>> declared)
    {
        var sizes = new Dictionary<string, int>(StringComparer.Ordinal);
        int refused = 0;
        foreach (KeyValuePair<string, string> entry in declared)
        {
            if (string.IsNullOrEmpty(entry.Key))
                continue;
            if (TryParseBodyLength(entry.Value, out int value))
            {
                sizes[entry.Key] = value;
                continue;
            }
            refused++;
            _logger.LogWarning($"[MonsterSize] Monster {entry.Key}: {MonsterSizeConfig.AttributeName}=\"{entry.Value}\" is not a whole number " +
                               $"from {MonsterSizeConfig.MinBodyLength} to {MonsterSizeConfig.MaxBodyLength}; its mounts keep their items' body_length.");
        }
        if (refused > 0)
            _logger.LogWarning($"[MonsterSize] {refused} {MonsterSizeConfig.AttributeName} value(s) refused (see the warnings above); " +
                               "those Monsters' mounts keep their items' body_length.");
        return sizes;
    }
}
