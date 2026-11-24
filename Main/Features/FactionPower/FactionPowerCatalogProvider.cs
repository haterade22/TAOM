using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;

namespace TAOM.Features.FactionPower;

/// <summary>Reads factions.json once, through the shared provider, and keeps the catalog for the process. An empty read is not kept.</summary>
public sealed class FactionPowerCatalogProvider : IFactionPowerCatalog
{
    private readonly IFactionConfigProvider _factions;
    private readonly IModLogger _logger;
    private volatile IReadOnlyList<FactionPowerEntry>? _entries;

    public FactionPowerCatalogProvider(IFactionConfigProvider factions, IModLogger logger)
    {
        _factions = factions;
        _logger = logger;
    }

    public IReadOnlyList<FactionPowerEntry> Entries
    {
        get
        {
            var entries = _entries;
            if (entries != null) return entries;
            entries = FactionPowerCatalog.Build(_factions.LoadFactions(), _logger.LogWarning);
            if (entries.Count > 0) _entries = entries;
            return entries;
        }
    }
}
