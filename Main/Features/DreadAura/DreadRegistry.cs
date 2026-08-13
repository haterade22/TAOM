using System;
using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.NazgulFamily;

namespace TAOM.Features.DreadAura;

/// <summary>
/// Resolves who projects dread and who resists it. Both answers come from one lazily-built table
/// keyed by race id, because the FaceGen race registry is engine state that is not populated when
/// this singleton is constructed at IoC time (the <c>RaceCombatModifiersResolver</c> pattern).
/// </summary>
public sealed class DreadRegistry : IDreadRegistry
{
    /// <summary>The only hero set name the config understands, resolving to
    /// <see cref="INazgulRegistry"/>. Kept as a name rather than a bool so the config reads as a
    /// list of lore groups and a second group is a one-line addition.</summary>
    private const string NazgulHeroSet = "nazgul_nine";

    private readonly IDreadAuraConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly INazgulRegistry _nazgul;
    private readonly IModLogger _logger;
    private readonly object _buildGate = new object();

    // Reference assignment is atomic on net472, so the unlocked read is safe; the lock only
    // prevents duplicate builds when several agents spawn on the same frame.
    private volatile Tables _tables;

    public DreadRegistry(
        IDreadAuraConfigProvider configProvider,
        IRaceManager raceManager,
        INazgulRegistry nazgul,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _nazgul = nazgul;
        _logger = logger;
    }

    public bool IsDreadSource(string heroStringId, int? raceId)
    {
        var tables = _tables ?? Build();

        // Axis 1 — hero StringId. This is the axis that finds the Nine: eight of them carry no
        // race attribute in TAOM's data and Khamûl is race="orc", so race alone would miss them
        // and "orc" would over-match every orc in the game.
        if (!string.IsNullOrEmpty(heroStringId))
        {
            if (tables.HeroIds.Contains(heroStringId))
                return true;

            if (tables.IncludesNazgulSet && _nazgul.IsWraith(heroStringId))
                return true;
        }

        // Axis 2 — FaceGen race, matched against a table keyed by race ID.
        //
        // The id keying is the whole defence against csharp-architecture.md's
        // "Lookup Functions With Fallbacks" trap. Names are resolved to ids ONCE, in Build, behind
        // an IsValidRaceName gate; nothing on this path ever calls GetRaceNameFromId, so its
        // "human" coercion of an unknown id cannot route a corrupt agent into a real row. An
        // IsValidRaceId call here would be dead weight: an invalid id simply misses the set.
        // Keep the table id-keyed. A name-keyed rewrite would reopen the trap.
        return raceId.HasValue && tables.SourceRaceIds.Contains(raceId.Value);
    }

    public float ResolveResist(int? targetRaceId)
    {
        if (!targetRaceId.HasValue)
            return 1f;

        // Id-keyed for the same reason as IsDreadSource above: no name lookup on the hot path, so
        // no fallback coercion. An unrecognised id misses the table and takes no resistance.
        var tables = _tables ?? Build();
        return tables.ResistByRaceId.TryGetValue(targetRaceId.Value, out var resist) ? resist : 1f;
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
                        $"DreadRegistry: heroSets entry '{setName}' is not a known hero set — entry skipped");
                }
            }

            var sourceRaceIds = new HashSet<int>();
            if (config.Races != null)
            {
                foreach (var name in config.Races)
                {
                    if (string.IsNullOrEmpty(name) || !_raceManager.IsValidRaceName(name))
                    {
                        _logger.LogWarning(
                            $"DreadRegistry: races entry '{name}' is not a known race name — entry skipped");
                        continue;
                    }

                    sourceRaceIds.Add(_raceManager.GetRaceIdFromName(name));
                }
            }

            var resistByRaceId = new Dictionary<int, float>();
            if (config.RaceResist != null)
            {
                foreach (var pair in config.RaceResist)
                {
                    if (string.IsNullOrEmpty(pair.Key) || !_raceManager.IsValidRaceName(pair.Key))
                    {
                        _logger.LogWarning(
                            $"DreadRegistry: raceResist key '{pair.Key}' is not a known race name — entry skipped");
                        continue;
                    }

                    // The config provider already range-checks these; re-checking here costs one
                    // comparison on a table built once per process and keeps the hot path free of
                    // any value the resolver did not vet itself.
                    if (!FiniteFloatValidator.IsFiniteInRange(pair.Value, 0f, 1f))
                    {
                        _logger.LogWarning(
                            $"DreadRegistry: raceResist['{pair.Key}'] = {pair.Value} is outside [0, 1] — entry skipped");
                        continue;
                    }

                    resistByRaceId[_raceManager.GetRaceIdFromName(pair.Key)] = pair.Value;
                }
            }

            _tables = new Tables(heroIds, includesNazgulSet, sourceRaceIds, resistByRaceId);
            return _tables;
        }
    }

    private sealed class Tables
    {
        public Tables(
            HashSet<string> heroIds,
            bool includesNazgulSet,
            HashSet<int> sourceRaceIds,
            Dictionary<int, float> resistByRaceId)
        {
            HeroIds = heroIds;
            IncludesNazgulSet = includesNazgulSet;
            SourceRaceIds = sourceRaceIds;
            ResistByRaceId = resistByRaceId;
        }

        public HashSet<string> HeroIds { get; }

        public bool IncludesNazgulSet { get; }

        public HashSet<int> SourceRaceIds { get; }

        public Dictionary<int, float> ResistByRaceId { get; }
    }
}
