using System.Collections.Generic;
using TAOM.Core.Domain;
using TAOM.Features.RaceAge.Models;

namespace TAOM.Features.RaceAge;

public class RaceAgeService : IRaceAgeService
{
    private readonly IRaceManager _raceManager;
    private readonly Dictionary<string, RaceAgeEntry> _races;
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
        if (IsImmortal(raceId)) return false;
        return currentAge > GetMaxAge(raceId);
    }

    private RaceAgeEntry GetEntry(int raceId)
    {
        var raceName = _raceManager.GetRaceNameFromId(raceId);
        if (raceName != null && _races.TryGetValue(raceName, out var entry))
        {
            return entry;
        }
        return _defaultEntry;
    }
}
