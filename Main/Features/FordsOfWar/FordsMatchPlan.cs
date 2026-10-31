using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// A <see cref="FordsMatchSetup"/> resolved against the shipped config: the match numbers, the map entry (its layout options
/// carry the chosen lane count), the two faction catalogs and the AI profile id the East slot will use. The director takes the
/// plan, so it never reads a file.
/// </summary>
internal sealed record FordsMatchPlan(FordsMatchConfig Config, MapEntry Map,
    IReadOnlyDictionary<MatchSide, FactionCatalog> Factions, string AiProfile, int LaneCount)
{
    private const int MinLanes = 1;
    private const int MaxLanes = 4;
    private const string FallbackProfile = "normal";

    /// <summary>Null, with one error logged, when the scene or either faction is not in the config.</summary>
    public static FordsMatchPlan? Resolve(FordsMatchSetup setup, FordsConfigCatalog catalog, IModLogger logger)
    {
        if (!catalog.TryGetMap(setup.SceneName, out var map))
        {
            logger.LogError($"[FordsOfWar] scene '{setup.SceneName}' is not in maps.json; the match cannot start");
            return null;
        }

        if (!catalog.TryGetFaction(setup.WestFaction, out var west))
        {
            logger.LogError($"[FordsOfWar] faction '{setup.WestFaction}' is not in factions.json; the match cannot start");
            return null;
        }

        if (!catalog.TryGetFaction(setup.EastFaction, out var east))
        {
            logger.LogError($"[FordsOfWar] faction '{setup.EastFaction}' is not in factions.json; the match cannot start");
            return null;
        }

        var config = catalog.Match;
        var lanes = Math.Max(MinLanes, Math.Min(MaxLanes, setup.LaneCount));
        var profile = config.AiProfiles.ContainsKey(setup.AiProfile) ? setup.AiProfile : FallbackProfile;
        var factions = new Dictionary<MatchSide, FactionCatalog> { [MatchSide.West] = west, [MatchSide.East] = east };
        return new FordsMatchPlan(config, map with { Options = map.Options with { LaneCount = lanes } }, factions, profile, lanes);
    }
}
