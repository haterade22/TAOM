using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Features.SignatureStrikes;

/// <summary>
/// Resolves signature-strike identity from one lazily-built table per signature (the DreadRegistry
/// pattern). Race names resolve to ids ONCE, behind an <c>IsValidRaceName</c> gate, because the
/// FaceGen race registry is engine state that is not populated when this singleton is constructed
/// at IoC time. Nothing on the hot path ever calls <c>GetRaceNameFromId</c>, whose "human"
/// coercion of an unknown id is the csharp-architecture.md "Lookup Functions With Fallbacks" trap:
/// an invalid id simply misses the id-keyed set. Keep the tables id-keyed.
///
/// One table per config signature, in config order, including a signature whose every race name
/// failed: the index this returns is the index the service reads its profiles by.
/// </summary>
public sealed class SignatureStrikeRegistry : ISignatureStrikeRegistry
{
    /// <summary>The one compiled hero set, resolved through <see cref="INazgulRegistry"/>. A name
    /// rather than a bool so the config reads as a list of lore groups (DreadRegistry idiom).</summary>
    private const string NazgulHeroSet = "nazgul_nine";

    private readonly ISignatureStrikesConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly INazgulRegistry _nazgul;
    private readonly IModLogger _logger;
    private readonly object _buildGate = new object();

    // Reference assignment is atomic on net472, so the unlocked read is safe; the lock only
    // prevents duplicate builds when several agents spawn on the same frame.
    private volatile SignatureTable[]? _tables;

    public SignatureStrikeRegistry(
        ISignatureStrikesConfigProvider configProvider,
        IRaceManager raceManager,
        INazgulRegistry nazgul,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _nazgul = nazgul;
        _logger = logger;
    }

    public int? ResolveSignatureIndex(string? heroStringId, int? raceId)
    {
        var tables = _tables ?? Build();

        if (!string.IsNullOrEmpty(heroStringId))
        {
            for (var i = 0; i < tables.Length; i++)
            {
                if (tables[i].HeroIds.Contains(heroStringId!)
                    || (tables[i].IncludesNazgulSet && _nazgul.IsWraith(heroStringId!)))
                    return i;
            }
        }

        if (raceId.HasValue)
        {
            for (var i = 0; i < tables.Length; i++)
            {
                if (tables[i].RaceIds.Contains(raceId.Value))
                    return i;
            }
        }

        return null;
    }

    public string GetSignatureId(int signatureIndex)
    {
        var tables = _tables ?? Build();
        return signatureIndex >= 0 && signatureIndex < tables.Length ? tables[signatureIndex].Id : "";
    }

    private SignatureTable[] Build()
    {
        lock (_buildGate)
        {
            if (_tables != null)
                return _tables;

            var signatures = _configProvider.GetConfig().Signatures ?? new List<SignatureConfig>();
            var tables = new SignatureTable[signatures.Count];
            // First owner of each hero id, hero set and race name, to warn about an overlap once.
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < signatures.Count; i++)
                tables[i] = BuildTable(signatures[i], owners);

            _tables = tables;
            return tables;
        }
    }

    private SignatureTable BuildTable(SignatureConfig? signature, Dictionary<string, string> owners)
    {
        var id = signature?.Id ?? "";
        var heroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var raceIds = new HashSet<int>();
        var includesNazgulSet = false;
        if (signature == null)
            return new SignatureTable(id, heroIds, includesNazgulSet, raceIds);

        foreach (var heroId in signature.HeroIds ?? new List<string>())
        {
            if (!string.IsNullOrEmpty(heroId) && heroIds.Add(heroId))
                WarnIfOwned(owners, "hero id", heroId, id);
        }

        foreach (var set in signature.HeroSets ?? new List<string>())
        {
            if (string.Equals(set, NazgulHeroSet, StringComparison.OrdinalIgnoreCase))
            {
                includesNazgulSet = true;
                WarnIfOwned(owners, "hero set", NazgulHeroSet, id);
                continue;
            }

            // Parsed-but-unresolvable: skip + warn, never default-route.
            _logger.LogWarning($"SignatureStrikeRegistry: signature '{id}' heroSets entry '{set}' is not a known hero set (only '{NazgulHeroSet}'), entry skipped");
        }

        foreach (var name in signature.Races ?? new List<string>())
        {
            if (string.IsNullOrEmpty(name) || !_raceManager.IsValidRaceName(name))
            {
                _logger.LogWarning($"SignatureStrikeRegistry: signature '{id}' races entry '{name}' is not a known race name, entry skipped");
                continue;
            }

            if (raceIds.Add(_raceManager.GetRaceIdFromName(name)))
                WarnIfOwned(owners, "race", name, id);
        }

        return new SignatureTable(id, heroIds, includesNazgulSet, raceIds);
    }

    private void WarnIfOwned(Dictionary<string, string> owners, string axis, string value, string signatureId)
    {
        var key = axis + ":" + value;
        if (owners.TryGetValue(key, out var first))
        {
            _logger.LogWarning($"SignatureStrikeRegistry: {axis} '{value}' is listed by signatures '{first}' and '{signatureId}'; '{first}' wins");
            return;
        }

        owners[key] = signatureId;
    }

    private sealed class SignatureTable
    {
        public SignatureTable(string id, HashSet<string> heroIds, bool includesNazgulSet, HashSet<int> raceIds)
        {
            Id = id;
            HeroIds = heroIds;
            IncludesNazgulSet = includesNazgulSet;
            RaceIds = raceIds;
        }

        public string Id { get; }

        public HashSet<string> HeroIds { get; }

        public bool IncludesNazgulSet { get; }

        public HashSet<int> RaceIds { get; }
    }
}
