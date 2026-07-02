using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Features.CombatMechanics;

public class RaceCombatModifiersResolver : IRaceCombatModifiersResolver
{
    private readonly ICombatMechanicsConfigProvider _configProvider;
    private readonly ICombatMechanicsSettingsProvider _settings;
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private readonly object _buildGate = new object();

    // Built lazily on first resolve — the race registry is engine state that isn't populated when
    // the singleton is constructed at IoC time. Reference assignment is atomic on net472, so the
    // unlocked read is safe; the lock only prevents duplicate builds.
    private volatile Dictionary<int, RaceCombatModifiers> _byRaceId;

    public RaceCombatModifiersResolver(
        ICombatMechanicsConfigProvider configProvider,
        ICombatMechanicsSettingsProvider settings,
        IRaceManager raceManager,
        IModLogger logger)
    {
        _configProvider = configProvider;
        _settings = settings;
        _raceManager = raceManager;
        _logger = logger;
    }

    public RaceCombatModifiers Resolve(int? raceId)
    {
        if (!_settings.RaceCombatModifiersEnabled)
            return RaceCombatModifiers.Neutral;

        // Validate BEFORE any name lookup — GetRaceNameFromId coerces unknown ids to "human"
        // (csharp-architecture.md "Lookup Functions With Fallbacks"), which would silently hand
        // an invalid agent the human row.
        if (!raceId.HasValue || !_raceManager.IsValidRaceId(raceId.Value))
            return RaceCombatModifiers.Neutral;

        var map = _byRaceId ?? BuildMap();
        return map.TryGetValue(raceId.Value, out var modifiers) ? modifiers : RaceCombatModifiers.Neutral;
    }

    private Dictionary<int, RaceCombatModifiers> BuildMap()
    {
        lock (_buildGate)
        {
            if (_byRaceId != null)
                return _byRaceId;

            var map = new Dictionary<int, RaceCombatModifiers>();
            foreach (var pair in _configProvider.GetConfig().RaceModifiers)
            {
                // Unknown key = parsed-but-unresolvable (the M1 trap): skip + warn, never
                // default-route.
                if (string.IsNullOrEmpty(pair.Key) || !_raceManager.IsValidRaceName(pair.Key))
                {
                    _logger.LogWarning($"RaceCombatModifiersResolver: raceModifiers key '{pair.Key}' is not a known race name — entry skipped");
                    continue;
                }

                map[_raceManager.GetRaceIdFromName(pair.Key)] = pair.Value ?? RaceCombatModifiers.Neutral;
            }

            _byRaceId = map;
            return map;
        }
    }
}
