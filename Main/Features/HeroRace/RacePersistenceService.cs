using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
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
    private readonly INazgulRegistry _nazgul;
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
    // Set by ResetForNewCampaign, cleared by a load: a new campaign's empty map is expected, so
    // RestoreHeroRaces only warns about missing race data on a loaded save.
    private bool _isNewCampaign;
    // Set when the engine hands SyncData a load. It does so only when the save has a record for the behavior
    // (CampaignBehaviorDataStore.LoadBehaviorData), so an empty map without it is an older save, not a lost map.
    private bool _loadedFromRecord;

    // #1011 check lines (#1072). The outcome latch is once per (id, result, key) for the process, so each save and
    // each load gets its own serial as the key. Never reset them: a reused key would hide a later campaign's line.
    internal const string CaptureCheckId = "taom.race-persist.capture";
    internal const string RestoreCheckId = "taom.race-persist.restore";
    private static readonly IFormatProvider Invariant = System.Globalization.CultureInfo.InvariantCulture;
    private readonly IOutcomeLog _outcomes;
    private int _saveSerial;
    private int _loadSerial;

    public int CapturedRaceCount => _heroRaceMap.Count;

    public RacePersistenceService(IHeroRosterAdapter heroRosterAdapter, IRaceManager raceManager, IModLogger logger,
        INazgulRegistry nazgul, IOutcomeLog outcomes)
    {
        _heroRosterAdapter = heroRosterAdapter;
        _raceManager = raceManager;
        _logger = logger;
        _nazgul = nazgul;
        _outcomes = outcomes;
    }

    // A capture is only as trustworthy as the race table it was taken against. Below this many
    // races the table cannot be TAOM's, so the capture describes a world that does not have our
    // races rather than a world where nobody has one. Two is the smallest count that can express
    // "human and something else", which is the minimum a real TAOM load produces.
    private const int MinimumTrustworthyRaceCount = 2;

    public void CaptureHeroRaces() => Capture(out _, out _);

    // The session-launch capture above writes no line, or a load alone would pass the save check.
    public void CaptureHeroRacesForSave()
    {
        var key = (++_saveSerial).ToString(Invariant);
        if (Capture(out var raceCount, out var dead))
            _outcomes.Pass(CaptureCheckId, key, string.Format(Invariant,
                "heroes={0} dead={1}", _heroRaceMap.Count, dead));
        else
            _outcomes.Fail(CaptureCheckId, key, string.Format(Invariant,
                "reason=race-table races={0} kept={1}", raceCount, _heroRaceMap.Count));
    }

    // False when the race table is refused; dead counts the dead heroes the map took (first occurrence per id).
    private bool Capture(out int raceCount, out int dead)
    {
        dead = 0;
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
        raceCount = raceNames?.Count ?? 0;
        if (raceCount < MinimumTrustworthyRaceCount)
        {
            _logger.LogWarning(
                $"RacePersistenceService: refusing to capture hero races against a {raceCount}-race table " +
                "(TAOM's races are not loaded here — a co-op host without our modules looks exactly like this). " +
                "Keeping the existing race map; capturing now would restore every hero as human on a full client.");
            return false;
        }

        _heroRaceMap = new Dictionary<string, int>();
        _raceNameLegend = string.Join(";", raceNames);

        // #1011: the roster holds alive AND dead or disabled heroes; the engine resets both on load.
        var heroes = _heroRosterAdapter.GetAllHeroRaces();
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
                if (!hero.IsAlive)
                    dead++;
            }
        }
        return true;
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
        _isNewCampaign = true;
        _loadedFromRecord = false;
    }

    /// <summary>
    /// Clears the race map, the legend and the new-campaign flag (#1035). The behavior's constructor calls it for every
    /// campaign, before any SyncData, so a save that carries no record for the behavior (the engine then skips its
    /// SyncData) starts clean instead of inheriting the previous campaign's races.
    /// </summary>
    public void ResetForNewSession()
    {
        _heroRaceMap = new Dictionary<string, int>();
        _raceNameLegend = "";
        _isNewCampaign = false;
        _loadedFromRecord = false;
    }

    public void RestoreHeroRaces()
    {
        var key = (++_loadSerial).ToString(Invariant);
        if (_heroRaceMap.Count == 0)
        {
            if (_isNewCampaign)
            {
                _logger.LogInfo("RacePersistenceService: new campaign, no race data to restore yet.");
                _outcomes.Skip(RestoreCheckId, key, "reason=new-campaign");
                return;
            }
            _logger.LogWarning("RacePersistenceService: No saved race data found. " +
                "This is expected on first load with a pre-TAOM save — heroes will use " +
                "their XML-defined races. Race data will be captured on next save.");
            // A save written with this behavior carries the map, so an empty one from its record is a lost map.
            if (_loadedFromRecord)
                _outcomes.Fail(RestoreCheckId, key, "reason=no-saved-data");
            else
                _outcomes.Skip(RestoreCheckId, key, "reason=no-record");
            return;
        }

        var restoredCount = 0;
        var skippedInvalid = 0;
        var keptWraiths = 0;
        var deadRestored = 0;
        // The race each map hero should have once the restore is done; wraiths and invalid races have none.
        var expected = new Dictionary<string, int>(StringComparer.Ordinal);
        var heroes = _heroRosterAdapter.GetAllHeroRaces();
        // Issue #330 — legend path: translate the saved index through the save-time name list to
        // the CURRENT id, so a skins.xml merge-order shift between save and load can't remap races.
        var legend = string.IsNullOrEmpty(_raceNameLegend) ? null : _raceNameLegend.Split(';');

        foreach (var hero in heroes)
        {
            if (!_heroRaceMap.TryGetValue(hero.StringId, out var savedRace))
                continue;

            // #644: a Ringwraith's race comes from its XML (nazghul). A save written before #644
            // captured the old race (human for six, uruk for three), so restoring it would undo the
            // data change on every existing campaign. Three runtime writers can reach a wraith: the
            // player's face editor and the campaign.import_main_hero cheat (a Player Switcher
            // wraith), and co-op join reconciliation (JoinReconciliationService, on the hero a
            // joiner is handed). Each edit is deliberately dropped on load: a wraith stays a wraith
            // (Mike, 2026-09-23).
            if (_nazgul.IsWraith(hero.StringId))
            {
                keptWraiths++;
                continue;
            }

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
                expected[hero.StringId] = currentId;
                if (hero.Race != currentId)
                {
                    _heroRosterAdapter.SetHeroRace(hero.StringId, currentId);
                    restoredCount++;
                    if (!hero.IsAlive)
                        deadRestored++;
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
                if (!hero.IsAlive)
                    deadRestored++;
            }
            expected[hero.StringId] = savedRace;
        }

        if (keptWraiths > 0)
            _logger.LogInfo($"RacePersistenceService: kept the XML race for {keptWraiths} Ringwraiths (#644).");
        _logger.LogInfo($"RacePersistenceService: Restored race for {restoredCount} heroes.");

        var counts = string.Format(Invariant, "restored={0} dead_restored={1} wraiths={2} skipped={3}",
            restoredCount, deadRestored, keptWraiths, skippedInvalid);
        var saved = _heroRaceMap.Count.ToString(Invariant);
        var mismatches = CountMismatches(expected, out var first);
        if (mismatches == 0)
            _outcomes.Pass(RestoreCheckId, key, counts + " mismatch=0 saved=" + saved);
        else
            _outcomes.Fail(RestoreCheckId, key, string.Format(Invariant,
                "mismatch={0} first={1} {2} saved={3}", mismatches, first, counts, saved));
    }

    // Reads every race back after the restore. Only the first hero with an id is compared: that is the one
    // SetHeroRace reaches (Hero.FindFirst over the same Campaign.Characters order). first is "<id>:<expected>-><actual>".
    private int CountMismatches(Dictionary<string, int> expected, out string first)
    {
        first = null;
        var mismatches = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hero in _heroRosterAdapter.GetAllHeroRaces())
        {
            if (!seen.Add(hero.StringId) || !expected.TryGetValue(hero.StringId, out var want) || hero.Race == want)
                continue;
            mismatches++;
            first ??= string.Format(Invariant, "{0}:{1}->{2}", hero.StringId, want, hero.Race);
        }
        return mismatches;
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
            _isNewCampaign = false;
            _loadedFromRecord = true;
        }
        dataStore.SyncData("_taom_heroRaceMap", ref _heroRaceMap);
        dataStore.SyncData("_taom_raceNameLegend", ref _raceNameLegend);
    }
}
