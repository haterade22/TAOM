using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.UncapturableHeroes.Domain;

namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// Resolves who can never be taken prisoner, from one lazily-built table keyed by race id and
/// hero StringId. Built lazily rather than in the constructor because the FaceGen race registry is
/// engine state that is not populated when this singleton is created at IoC time (the same
/// constraint <see cref="DreadAura.DreadRegistry"/> documents).
/// </summary>
public sealed class UncapturableRegistry : IUncapturableRegistry
{
    /// <summary>The only hero set name the config understands, resolving to
    /// <see cref="INazgulRegistry"/>. Kept as a name rather than a bool so the config reads as a
    /// list of lore groups and a second group is a one-line addition.</summary>
    private const string NazgulHeroSet = "nazgul_nine";

    private readonly IUncapturableHeroesConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly INazgulRegistry _nazgul;
    private readonly IModLogger _logger;
    private readonly object _buildGate = new object();

    // Reference assignment is atomic on net472, so the unlocked read is safe; the lock only
    // prevents duplicate builds when several callers arrive on the same frame.
    private volatile Tables? _tables;

    public UncapturableRegistry(
        IUncapturableHeroesConfigProvider configProvider,
        IRaceManager raceManager,
        INazgulRegistry nazgul,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _nazgul = nazgul;
        _logger = logger;
    }

    public bool IsUncapturable(string heroStringId, int? raceId)
    {
        var tables = _tables ?? Build();

        if (!string.IsNullOrEmpty(heroStringId))
        {
            // Axis 0 — the override-out list, evaluated FIRST so it beats the rule and both
            // include lists. This is the escape hatch that lets an author hand one hero back to
            // vanilla capture without deleting the lore group he belongs to.
            if (tables.ExcludeHeroIds.Contains(heroStringId))
                return false;

            // Axis 1 — explicit hero StringId.
            if (tables.HeroIds.Contains(heroStringId))
                return true;

            // Axis 2 — named lore sets. THIS is the axis that finds the Nine: six of them carry no
            // race attribute (so they are vanilla race 0, human) and three are race="uruk", so a
            // race list either misses them or over-matches every uruk lord in the game.
            if (tables.IncludesNazgulSet && _nazgul.IsWraith(heroStringId))
                return true;
        }

        // Axis 3 — THE RULE. FaceGen race, matched against a table keyed by race ID.
        //
        // The id keying is the defence against csharp-architecture.md's "Lookup Functions With
        // Fallbacks" trap. Names are resolved to ids ONCE, in Build, behind an IsValidRaceName
        // gate; nothing on this path ever calls GetRaceNameFromId, so its "human" coercion of an
        // unknown id cannot route a corrupt hero into a real row. An IsValidRaceId call here would
        // be dead weight: an invalid id simply misses the set. Keep the table id-keyed.
        return raceId.HasValue && tables.RaceIds.Contains(raceId.Value);
    }

    private Tables Build()
    {
        lock (_buildGate)
        {
            if (_tables != null)
                return _tables;

            var config = _configProvider.GetConfig();

            var excludeHeroIds = ToIdSet(config.ExcludeHeroIds);
            var heroIds = ToIdSet(config.HeroIds);

            var includesNazgulSet = false;
            if (config.HeroSets != null)
            {
                foreach (var setName in config.HeroSets)
                {
                    if (string.Equals(setName, NazgulHeroSet, StringComparison.OrdinalIgnoreCase))
                    {
                        includesNazgulSet = true;
                        continue;
                    }

                    // Parsed-but-unresolvable (the M1 trap): skip + warn, never default-route.
                    _logger.LogWarning(
                        $"UncapturableRegistry: heroSets entry '{setName}' is not a known hero set — entry skipped");
                }
            }

            var raceIds = new HashSet<int>();
            if (config.UncapturableRaces != null)
            {
                foreach (var name in config.UncapturableRaces)
                {
                    if (string.IsNullOrEmpty(name) || !_raceManager.IsValidRaceName(name))
                    {
                        _logger.LogWarning(
                            $"UncapturableRegistry: uncapturableRaces entry '{name}' is not a known race name — entry skipped");
                        continue;
                    }

                    raceIds.Add(_raceManager.GetRaceIdFromName(name));
                }
            }

            _tables = new Tables(excludeHeroIds, heroIds, includesNazgulSet, raceIds);
            return _tables;
        }
    }

    // OrdinalIgnoreCase matches the codebase convention (NazgulRegistry, InitialChildGeneration).
    // Hero StringIds are lowercase, so this is a defensive superset, not a behaviour change.
    private static HashSet<string> ToIdSet(List<string>? ids)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ids == null)
            return set;

        foreach (var id in ids)
        {
            if (!string.IsNullOrEmpty(id))
                set.Add(id);
        }

        return set;
    }

    private sealed class Tables
    {
        public Tables(
            HashSet<string> excludeHeroIds,
            HashSet<string> heroIds,
            bool includesNazgulSet,
            HashSet<int> raceIds)
        {
            ExcludeHeroIds = excludeHeroIds;
            HeroIds = heroIds;
            IncludesNazgulSet = includesNazgulSet;
            RaceIds = raceIds;
        }

        public HashSet<string> ExcludeHeroIds { get; }

        public HashSet<string> HeroIds { get; }

        public bool IncludesNazgulSet { get; }

        public HashSet<int> RaceIds { get; }
    }
}
