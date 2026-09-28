using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Loads the generated armour_classes.xml (tools/generate_armour_classes.py). Refuses, with a warning,
/// a row with no id, an unknown class or a repeated id, and drops an upgrade link that points at its
/// own row; one summary warning counts the refusals. A missing or unreadable table is an empty one:
/// every piece then falls back to its engine tier (ArmourClassRules.FromEngineTier). Reuse.Singleton.
/// </summary>
public sealed class ArmourClassTableProvider : IArmourClassTableProvider
{
    private const string FileName = "armour_classes.xml";
    private const string Tag = "[ArmourAcquisition]";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private IReadOnlyDictionary<string, ArmourClassEntry>? _entries;

    public ArmourClassTableProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, ArmourClassEntry> GetEntries() => _entries ??= Load();

    private IReadOnlyDictionary<string, ArmourClassEntry> Load()
    {
        var entries = new Dictionary<string, ArmourClassEntry>(StringComparer.Ordinal);
        var path = Path.Combine(_pathService.ModuleDataPath, "armour_acquisition", FileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning($"{Tag} {FileName} is missing at {path}; every armour piece falls back to its engine tier. "
                               + "Regenerate it: python tools/generate_armour_classes.py --apply");
            return entries;
        }

        XElement root;
        try
        {
            root = XDocument.Load(path).Root ?? throw new InvalidDataException("empty document");
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} {FileName} does not parse ({ex.Message}); every armour piece falls back to its engine tier.");
            return entries;
        }

        var refused = 0;
        foreach (var row in root.Elements("Item"))
        {
            var id = row.Attribute("id")?.Value?.Trim();
            var rawClass = row.Attribute("class")?.Value;
            if (id is null || id.Length == 0)
            {
                refused++;
                _logger.LogWarning($"{Tag} {FileName}: a row has no id; refused.");
                continue;
            }
            if (!ArmourClassRules.TryParse(rawClass, out var cls))
            {
                refused++;
                _logger.LogWarning($"{Tag} {FileName}: '{id}' has unknown class '{rawClass}'; refused.");
                continue;
            }
            if (entries.ContainsKey(id))
            {
                refused++;
                _logger.LogWarning($"{Tag} {FileName}: '{id}' is listed twice; keeping the first.");
                continue;
            }
            var next = row.Attribute("next")?.Value?.Trim();
            if (string.Equals(next, id, StringComparison.Ordinal))
            {
                refused++;
                _logger.LogWarning($"{Tag} {FileName}: '{id}' upgrades into itself; the link is dropped.");
                next = null;
            }
            entries[id] = new ArmourClassEntry(id, cls, next);
        }

        if (refused > 0)
            _logger.LogWarning($"{Tag} {FileName}: {refused} row(s) refused or repaired (see above); regenerate the table.");
        _logger.LogInfo($"{Tag} Class table loaded: {entries.Count} piece(s).");
        return entries;
    }
}
