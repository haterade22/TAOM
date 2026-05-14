using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public class RacePersistenceService : IRacePersistenceService
{
    private readonly IHeroRosterAdapter _heroRosterAdapter;
    // Phase 9b #171 — injected for validate-before-restore on save data; without this guard,
    // an obsolete race ID (e.g., from a removed race-mod) flows into GetRaceNameFromId → "human"
    // fallback gets permanently session-cached, silently breaking lifespan/fertility everywhere.
    private readonly IRaceManager _raceManager;
    private readonly IModLogger _logger;
    private Dictionary<string, int> _heroRaceMap = new();

    public int CapturedRaceCount => _heroRaceMap.Count;

    public RacePersistenceService(IHeroRosterAdapter heroRosterAdapter, IRaceManager raceManager, IModLogger logger)
    {
        _heroRosterAdapter = heroRosterAdapter;
        _raceManager = raceManager;
        _logger = logger;
    }

    public void CaptureHeroRaces()
    {
        _heroRaceMap = new Dictionary<string, int>();

        var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();
        foreach (var hero in heroes)
        {
            // Phase 9b #130 P2 — capture ALL races including 0 (human). Pre-fix the `> 0` guard
            // skipped humans to keep the map small; but a hero deliberately reset to human (race=0)
            // by Patch3_SetRace/CharacterCreation/NamedCompanions wouldn't get captured, and the
            // stale non-human entry from a prior capture would silently revert the human assignment
            // on next load. Capture all races now (cost: one int per hero — negligible).
            if (!_heroRaceMap.ContainsKey(hero.StringId))
            {
                _heroRaceMap[hero.StringId] = hero.Race;
            }
        }
    }

    public void ResetForNewCampaign()
    {
        // Phase 9b #130 R1 — clear singleton on new campaign in same process. SyncData on an
        // absent-key load (fresh save) leaves _heroRaceMap unchanged → prior campaign's map
        // carries over → RestoreHeroRaces silently overwrites new heroes with old race assignments
        // for colliding StringIds (every common vanilla lord uses stable IDs like "lord_1_1").
        if (_heroRaceMap.Count > 0)
            _logger.LogInfo($"RacePersistenceService: ResetForNewCampaign clearing {_heroRaceMap.Count} stale race entries from prior campaign.");
        _heroRaceMap = new Dictionary<string, int>();
    }

    public void RestoreHeroRaces()
    {
        if (_heroRaceMap.Count == 0)
        {
            _logger.LogWarning("RacePersistenceService: No saved race data found. " +
                "This is expected on first load with a pre-TAOM save — heroes will use " +
                "their XML-defined races. Race data will be captured on next save.");
            return;
        }

        var restoredCount = 0;
        var skippedInvalid = 0;
        var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();

        foreach (var hero in heroes)
        {
            if (_heroRaceMap.TryGetValue(hero.StringId, out var savedRace) && hero.Race != savedRace)
            {
                // Phase 9b #171 P1 — validate-before-restore. Save predating a race-mod removal
                // can persist int IDs that no longer correspond to a valid race; on restore the
                // bad ID would flow into RaceManager.GetRaceNameFromId → "human" fallback gets
                // cached PERMANENTLY for the session, silently breaking elven immortality, dwarf
                // aging, etc. See feedback_validate_before_lookup_with_fallback.md.
                if (savedRace != 0 && !_raceManager.IsValidRaceId(savedRace))
                {
                    skippedInvalid++;
                    _logger.LogWarning($"RacePersistenceService: skipping invalid saved race {savedRace} for hero '{hero.StringId}' (race-mod removed?); falling back to current XML race.");
                    continue;
                }
                _heroRosterAdapter.SetHeroRace(hero.StringId, savedRace);
                restoredCount++;
            }
        }

        _logger.LogInfo($"RacePersistenceService: Restored race for {restoredCount} heroes.");
    }

    public void SyncRaceData(IDataStore dataStore)
    {
        dataStore.SyncData("_taom_heroRaceMap", ref _heroRaceMap);
    }
}
