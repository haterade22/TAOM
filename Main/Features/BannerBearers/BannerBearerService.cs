using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.BannerBearers;

// Banner-bearer policy. Engine-free apart from the FormationClass value-type enum, so the
// whole surface is unit-testable without a live Mission (ADR-008).
public sealed class BannerBearerService : IBannerBearerService
{
    // The engine's bearer arrangement tables are RelativeFormationPosition[6]
    // (BannerBearerLineFormationPositions / Circular / Skein / Square). Asking for more than
    // six leaves the surplus bearers in their original slots — never request past the cap.
    private const int EngineArrangementCap = 6;

    private readonly IBannerBearerConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;

    // ExcludedRaces can't be validated at config-load time (FaceGen's race registry isn't
    // populated yet), and an unknown name fails OPEN — it simply never matches, so a typo
    // silently re-admits the race it was meant to bar. Validate on first use, warn once.
    private bool _excludedRacesChecked;

    public BannerBearerService(
        IBannerBearerConfigProvider configProvider, IRaceManager raceManager, IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _logger = logger;
    }

    public bool IsEnabled => _configProvider.GetConfig().Enabled;

    public int MinimumFormationTroopCount => _configProvider.GetConfig().MinimumFormationTroopCount;

    public int GetDesiredBearerCount(int formationUnitCount, FormationClass formationClass)
    {
        var config = _configProvider.GetConfig();
        if (!config.Enabled) return 0;
        if (formationUnitCount < config.MinimumFormationTroopCount) return 0;

        var perN = GetBannerPerSoldiers(config, formationClass);
        if (perN <= 0) return 0;

        // Every eligible formation fields at least one standard (vanilla's behaviour);
        // bigger formations field proportionally more, up to the cap.
        var desired = Math.Max(1, formationUnitCount / perN);

        var cap = Math.Min(config.MaxBearersPerFormation, EngineArrangementCap);
        if (cap < 1) cap = 1;

        return Math.Min(desired, cap);
    }

    public bool IsRaceAllowed(int raceId)
    {
        var config = _configProvider.GetConfig();
        WarnOnUnknownExcludedRacesOnce(config.ExcludedRaces);

        // Validate BEFORE the lookup: RaceManager.GetRaceNameFromId coerces unknown ids to
        // "human", which is not on the exclusion list, so a lookup-first check would silently
        // admit corrupt race ids (csharp-architecture.md "Validate Before Lookup").
        if (!_raceManager.IsValidRaceId(raceId)) return false;

        var raceName = _raceManager.GetRaceNameFromId(raceId);
        if (string.IsNullOrEmpty(raceName)) return false;

        var excluded = config.ExcludedRaces;
        if (excluded == null) return true;

        foreach (var entry in excluded)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            if (string.Equals(entry, raceName, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    public string? ResolveBannerItemId(string? cultureId)
    {
        var config = _configProvider.GetConfig();
        if (!config.Enabled) return null;

        if (cultureId != null
            && config.CultureBanners != null
            && config.CultureBanners.TryGetValue(cultureId, out var mapped))
        {
            // An explicit empty mapping means "this culture fields no banner".
            return string.IsNullOrEmpty(mapped) ? null : mapped;
        }

        return string.IsNullOrEmpty(config.DefaultBannerItemId) ? null : config.DefaultBannerItemId;
    }

    public string? ResolveMajorityCultureId(IReadOnlyList<string?> cultureIds)
    {
        if (cultureIds == null || cultureIds.Count == 0) return null;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in cultureIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            counts.TryGetValue(id!, out var seen);
            counts[id!] = seen + 1;
        }

        string? best = null;
        var bestCount = 0;
        foreach (var pair in counts)
        {
            // Ordinal tie-break keeps the winner independent of arrangement/enumeration order —
            // a 50/50 formation must not flip its standard between deployments.
            var wins = pair.Value > bestCount
                || (pair.Value == bestCount && best != null && string.CompareOrdinal(pair.Key, best) < 0);
            if (!wins) continue;

            best = pair.Key;
            bestCount = pair.Value;
        }

        return best;
    }

    private void WarnOnUnknownExcludedRacesOnce(List<string> excluded)
    {
        if (_excludedRacesChecked) return;
        _excludedRacesChecked = true;
        if (excluded == null) return;

        foreach (var entry in excluded)
        {
            if (string.IsNullOrEmpty(entry)) continue;
            if (_raceManager.IsValidRaceName(entry)) continue;

            _logger?.LogWarning(
                $"[BannerBearers] excluded race '{entry}' is not a known race id — it will never match, " +
                "so that race stays eligible to carry a banner. Check ExcludedRaces in " +
                "banner_bearers_config.json against LOTRLOME_Armory/ModuleData/skins.xml.");
        }
    }

    private static int GetBannerPerSoldiers(Domain.BannerBearerConfig config, FormationClass formationClass) =>
        formationClass switch
        {
            FormationClass.Infantry => config.InfantryBannerPerSoldiers,
            FormationClass.Ranged => config.RangedBannerPerSoldiers,
            FormationClass.Cavalry => config.CavalryBannerPerSoldiers,
            FormationClass.HorseArcher => config.HorseArcherBannerPerSoldiers,
            _ => config.OtherBannerPerSoldiers,
        };
}
