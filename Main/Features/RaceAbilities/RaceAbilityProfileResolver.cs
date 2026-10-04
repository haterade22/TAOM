using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// A soldier's ability profile, from his race and culture: his race's profile when it has one; otherwise,
// for a soldier of the human race only, his culture's. Race names are checked against the engine's
// registry on first use (the registry is engine state, empty when the config loads) and turned into ids;
// the maps are keyed by id, so this never calls GetRaceNameFromId, whose "human" fallback for an unknown
// id is why other resolvers validate an id first (DreadRegistry). An unknown id simply misses every map.
// Culture names are not checked here: no engine registry is reachable, so the shipped keys are pinned by
// RaceAbilitiesLiveKeyTests instead. Also answers who counts as kin. The maps are built once and then only
// read; a concurrent first build at worst builds them twice.
public class RaceAbilityProfileResolver
{
    private static readonly HashSet<int> NoKinRaces = new HashSet<int>();

    private readonly IRaceAbilitiesConfigProvider _configProvider;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private volatile Maps? _maps;

    public RaceAbilityProfileResolver(IRaceAbilitiesConfigProvider configProvider, IRaceManager raceManager, IModLogger logger)
    {
        _configProvider = configProvider;
        _raceManager = raceManager;
        _logger = logger;
    }

    public RaceAbilityTierScaling TierScaling => _configProvider.GetConfig().TierScaling;

    // race_abilities.json's own "enabled" switch, beside the MCM one.
    public bool ConfigEnabled => _configProvider.GetConfig().Enabled;

    // The outline cap and the spark effect.
    public RaceAbilityVisualsConfig VisualsConfig => _configProvider.GetConfig().Visuals;

    public RaceAbilityProfile? Resolve(int? raceId, string? cultureId)
    {
        if (!raceId.HasValue || !_configProvider.GetConfig().Enabled)
            return null;
        var maps = _maps ?? Build();
        if (maps.ByRaceId.TryGetValue(raceId.Value, out var byRace))
            return byRace;
        if (raceId == maps.HumanRaceId && !string.IsNullOrEmpty(cultureId) && maps.ByCulture.TryGetValue(cultureId!, out var byCulture))
            return byCulture;
        return null;
    }

    // Kin of a soldier with this profile: anyone with the same profile, or of a race the profile names.
    public bool IsKin(RaceAbilityProfile profile, int? otherRaceId, RaceAbilityProfile? otherProfile) =>
        ReferenceEquals(profile, otherProfile) || (otherRaceId.HasValue && KinRaces(profile).Contains(otherRaceId.Value));

    public HashSet<int> KinRaces(RaceAbilityProfile profile)
    {
        var maps = _maps ?? Build();
        return maps.KinRaceIds.TryGetValue(profile, out var ids) ? ids : NoKinRaces;
    }

    private Maps Build()
    {
        var config = _configProvider.GetConfig();
        var maps = new Maps();
        foreach (var pair in config.Races)
        {
            if (!_raceManager.IsValidRaceName(pair.Key))
            {
                _logger.LogWarning($"RaceAbilityProfileResolver: race '{pair.Key}' in race_abilities.json is not a known race, skipped");
                continue;
            }
            maps.ByRaceId[_raceManager.GetRaceIdFromName(pair.Key)] = pair.Value;
        }

        if (_raceManager.IsValidRaceName("human"))
            maps.HumanRaceId = _raceManager.GetRaceIdFromName("human");
        else if (config.Cultures.Count > 0)
            _logger.LogWarning("RaceAbilityProfileResolver: the engine has no race named 'human', so no culture profile applies");
        foreach (var pair in config.Cultures)
            maps.ByCulture[pair.Key] = pair.Value;

        foreach (var profile in AllProfiles(config))
        {
            if (maps.KinRaceIds.ContainsKey(profile))
                continue;
            var ids = new HashSet<int>();
            foreach (var name in profile.KinRaces)
            {
                if (_raceManager.IsValidRaceName(name))
                    ids.Add(_raceManager.GetRaceIdFromName(name));
                else
                    _logger.LogWarning($"RaceAbilityProfileResolver: kin race '{name}' of ability '{profile.AbilityId}' is not a known race, skipped");
            }
            maps.KinRaceIds[profile] = ids;
        }

        _maps = maps;
        return maps;
    }

    private static IEnumerable<RaceAbilityProfile> AllProfiles(RaceAbilitiesConfig config)
    {
        foreach (var profile in config.Races.Values)
            yield return profile;
        foreach (var profile in config.Cultures.Values)
            yield return profile;
    }

    private sealed class Maps
    {
        public Dictionary<int, RaceAbilityProfile> ByRaceId { get; } = new Dictionary<int, RaceAbilityProfile>();

        public Dictionary<string, RaceAbilityProfile> ByCulture { get; } = new Dictionary<string, RaceAbilityProfile>(System.StringComparer.Ordinal);

        // RaceAbilityProfile keeps reference equality, so each validated profile object is one key.
        public Dictionary<RaceAbilityProfile, HashSet<int>> KinRaceIds { get; } = new Dictionary<RaceAbilityProfile, HashSet<int>>();

        public int? HumanRaceId { get; set; }
    }
}
