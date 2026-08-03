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
    // Issue #330 — race names in FaceGen index order at CAPTURE time, ";"-joined (the engine's own
    // GetRaceIds delimiter). The ints in _heroRaceMap are positions in the merged skins.xml <race>
    // list, which shifts when a race is inserted/removed/reordered or the module set changes —
    // IsValidRaceId can't detect a shift (the stale index is still in range). The legend lets
    // RestoreHeroRaces translate savedInt -> name -> CURRENT id instead of trusting the raw index.
    // Kept as ONE string beside the proven Dictionary<string,int> rather than switching to
    // Dictionary<string,string>, which failed to round-trip IDataStore at ~1000 entries (WotR
    // Momentum, 2026-07-03). Empty legend == pre-#330 save == legacy raw-int restore path.
    private string _raceNameLegend = "";

    public int CapturedRaceCount => _heroRaceMap.Count;

    public RacePersistenceService(IHeroRosterAdapter heroRosterAdapter, IRaceManager raceManager, IModLogger logger)
    {
        _heroRosterAdapter = heroRosterAdapter;
        _raceManager = raceManager;
        _logger = logger;
    }

    // A capture is only as trustworthy as the race table it was taken against. Below this many
    // races the table cannot be TAOM's, so the capture describes a world that does not have our
    // races rather than a world where nobody has one. Two is the smallest count that can express
    // "human and something else", which is the minimum a real TAOM load produces.
    private const int MinimumTrustworthyRaceCount = 2;

    public void CaptureHeroRaces()
    {
        // Multiplayer field report 2026-08-03 §1 — refuse to capture against a degenerate race
        // table. A co-op host running WITHOUT TAOM's modules has one race ("human") in its FaceGen,
        // so every hero there reads back as 0. Capturing that writes legend="human" + {all heroes: 0},
        // which rides the host->client save transfer and makes RestoreHeroRaces force EVERY hero in
        // the world to human on a full 15-race client — each individual value being perfectly valid,
        // which is why no per-value validation catches it. The race COUNT is the only tell.
        //
        // Skip, don't clear: leaving the prior map and legend intact means a good capture already in
        // memory survives the bad host, and a genuinely empty state stays empty (RestoreHeroRaces
        // then takes its "no saved data" path and heroes keep their XML races).
        var raceNames = _raceManager.GetOrderedRaceNames();
        var raceCount = raceNames?.Count ?? 0;
        if (raceCount < MinimumTrustworthyRaceCount)
        {
            _logger.LogWarning(
                $"RacePersistenceService: refusing to capture hero races against a {raceCount}-race table " +
                "(TAOM's races are not loaded here — a co-op host without our modules looks exactly like this). " +
                "Keeping the existing race map; capturing now would restore every hero as human on a full client.");
            return;
        }

        _heroRaceMap = new Dictionary<string, int>();
        _raceNameLegend = string.Join(";", raceNames);

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
        _raceNameLegend = "";
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
        // Issue #330 — legend path: translate the saved index through the save-time name list to
        // the CURRENT id, so a skins.xml merge-order shift between save and load can't remap races.
        var legend = string.IsNullOrEmpty(_raceNameLegend) ? null : _raceNameLegend.Split(';');

        foreach (var hero in heroes)
        {
            if (!_heroRaceMap.TryGetValue(hero.StringId, out var savedRace))
                continue;

            if (legend != null)
            {
                if (savedRace < 0 || savedRace >= legend.Length)
                {
                    skippedInvalid++;
                    _logger.LogWarning($"RacePersistenceService: saved race {savedRace} for hero '{hero.StringId}' is outside the save's {legend.Length}-entry race legend (corrupt save data?); falling back to current XML race.");
                    continue;
                }
                var raceName = legend[savedRace];
                // Validate-before-lookup (csharp-architecture.md): GetRaceIdFromName falls back to
                // 0/human for unknown names — consulting it unguarded would silently restore a
                // removed race as human instead of keeping the hero's current XML race.
                if (!_raceManager.IsValidRaceName(raceName))
                {
                    skippedInvalid++;
                    _logger.LogWarning($"RacePersistenceService: saved race '{raceName}' for hero '{hero.StringId}' no longer exists in the loaded module set (race-mod removed?); falling back to current XML race.");
                    continue;
                }
                var currentId = _raceManager.GetRaceIdFromName(raceName);
                if (hero.Race != currentId)
                {
                    _heroRosterAdapter.SetHeroRace(hero.StringId, currentId);
                    restoredCount++;
                }
                continue;
            }

            // Legacy path — pre-#330 save with no legend: restore the raw index (today's behavior).
            if (hero.Race != savedRace)
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
        // Issue #330 — clear-on-load. SyncData with an absent key leaves the ref value UNCHANGED,
        // so loading an older-format (or pre-TAOM) save after a newer session in the same process
        // would silently keep the previous campaign's map/legend and restore them onto colliding
        // StringIds (#130-R1 bug class, previously only handled for new campaigns).
        if (dataStore.IsLoading)
        {
            _heroRaceMap = new Dictionary<string, int>();
            _raceNameLegend = "";
        }
        dataStore.SyncData("_taom_heroRaceMap", ref _heroRaceMap);
        dataStore.SyncData("_taom_raceNameLegend", ref _raceNameLegend);
    }
}
