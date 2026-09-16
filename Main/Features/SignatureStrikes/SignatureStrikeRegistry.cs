using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Resolves signature-strike identity from one lazily-built table (the DreadRegistry pattern).
/// Race names resolve to ids ONCE, behind an <c>IsValidRaceName</c> gate, because the FaceGen
/// race registry is engine state that is not populated when this singleton is constructed at IoC
/// time. Nothing on the hot path ever calls <c>GetRaceNameFromId</c>, whose "human" coercion of
/// an unknown id is the csharp-architecture.md "Lookup Functions With Fallbacks" trap: an invalid
/// id simply misses the id-keyed set. Keep the table id-keyed.
/// </summary>
public sealed class SignatureStrikeRegistry : ISignatureStrikeRegistry
{
    private readonly ISignatureStrikesConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private readonly object _buildGate = new object();

    // Reference assignment is atomic on net472, so the unlocked read is safe; the lock only
    // prevents duplicate builds when several agents spawn on the same frame.
    private volatile Tables? _tables;

    public SignatureStrikeRegistry(
        ISignatureStrikesConfigProvider configProvider,
        IRaceManager raceManager,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _logger = logger;
    }

    public bool IsSignatureAgent(string? heroStringId, int? raceId)
    {
        var tables = _tables ?? Build();

        if (!string.IsNullOrEmpty(heroStringId) && tables.HeroIds.Contains(heroStringId!))
            return true;

        return raceId.HasValue && tables.RaceIds.Contains(raceId.Value);
    }

    private Tables Build()
    {
        lock (_buildGate)
        {
            if (_tables != null)
                return _tables;

            var config = _configProvider.GetConfig();

            var heroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.HeroIds != null)
            {
                foreach (var id in config.HeroIds)
                {
                    if (!string.IsNullOrEmpty(id))
                        heroIds.Add(id);
                }
            }

            var raceIds = new HashSet<int>();
            if (config.Races != null)
            {
                foreach (var name in config.Races)
                {
                    if (string.IsNullOrEmpty(name) || !_raceManager.IsValidRaceName(name))
                    {
                        // Parsed-but-unresolvable: skip + warn, never default-route.
                        _logger.LogWarning(
                            $"SignatureStrikeRegistry: races entry '{name}' is not a known race name, entry skipped");
                        continue;
                    }

                    raceIds.Add(_raceManager.GetRaceIdFromName(name));
                }
            }

            _tables = new Tables(heroIds, raceIds);
            return _tables;
        }
    }

    private sealed class Tables
    {
        public Tables(HashSet<string> heroIds, HashSet<int> raceIds)
        {
            HeroIds = heroIds;
            RaceIds = raceIds;
        }

        public HashSet<string> HeroIds { get; }

        public HashSet<int> RaceIds { get; }
    }
}
