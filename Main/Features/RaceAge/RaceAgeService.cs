using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Features.RaceAge;

public class RaceAgeService : IRaceAgeService
{
    private readonly IRaceManager _raceManager;
    private readonly Dictionary<string, RaceAgeEntry> _races;
    private readonly Dictionary<int, RaceAgeEntry> _raceIdCache = new Dictionary<int, RaceAgeEntry>();
    private readonly RaceAgeEntry _defaultEntry;

    public RaceAgeService(IRaceAgeConfigProvider configProvider, IRaceManager raceManager)
    {
        _raceManager = raceManager;

        var config = configProvider.LoadConfig();
        _races = config.Races;

        if (!_races.TryGetValue(config.DefaultRace, out _defaultEntry))
        {
            _defaultEntry = new RaceAgeEntry();
        }
    }

    public int GetMaxAge(int raceId) => GetEntry(raceId).MaxAge;
    public int GetBecomeOldAge(int raceId) => GetEntry(raceId).BecomeOld;
    public int GetComesOfAge(int raceId) => GetEntry(raceId).ComesOfAge;
    public int GetMiddleAge(int raceId) => GetEntry(raceId).MiddleAge;
    public int GetFertilityEndAge(int raceId) => GetEntry(raceId).FertilityEnd;
    public float GetFertilityModifier(int raceId) => GetEntry(raceId).FertilityMod;
    public bool IsImmortal(int raceId) => GetEntry(raceId).Immortal;

    public bool ShouldDieOfOldAge(int raceId, float currentAge)
    {
        var entry = GetEntry(raceId);
        if (entry.Immortal) return false;
        return currentAge > entry.MaxAge;
    }

    private RaceAgeEntry GetEntry(int raceId)
    {
        if (_raceIdCache.TryGetValue(raceId, out var cached))
            return cached;

        // Phase 9b #131 P2 — validate BEFORE the lookup. _raceManager.GetRaceNameFromId returns
        // "human" as fallback for unknown IDs (RaceManager.cs:126-131). Without this guard, an
        // invalid raceId would resolve to the human RaceAgeEntry — silently treating dwarves/elves
        // with corrupted IDs as humans for ALL age + fertility calculations. See
        // feedback_validate_before_lookup_with_fallback.md (Codex review #33).
        if (!_raceManager.IsValidRaceId(raceId))
        {
            _raceIdCache[raceId] = _defaultEntry;
            return _defaultEntry;
        }

        var raceName = _raceManager.GetRaceNameFromId(raceId);
        var entry = (raceName != null && _races.TryGetValue(raceName, out var found))
            ? found
            : _defaultEntry;

        _raceIdCache[raceId] = entry;
        return entry;
    }

    // Phase 9b #131 P1 R1 — clear cache between campaigns. If race integer IDs shift (HeroRace #130
    // would have caused this), cache served entries mapped from old integer→name pairings to wrong
    // RaceAgeEntry. Now exposed for the behavior to call on session launch.
    public void ResetCache()
    {
        _raceIdCache.Clear();
    }
}
