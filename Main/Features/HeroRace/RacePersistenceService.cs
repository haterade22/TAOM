using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public class RacePersistenceService : IRacePersistenceService
{
    private readonly IHeroRosterAdapter _heroRosterAdapter;
    private readonly IModLogger _logger;
    private Dictionary<string, int> _heroRaceMap = new();

    public int CapturedRaceCount => _heroRaceMap.Count;

    public RacePersistenceService(IHeroRosterAdapter heroRosterAdapter, IModLogger logger)
    {
        _heroRosterAdapter = heroRosterAdapter;
        _logger = logger;
    }

    public void CaptureHeroRaces()
    {
        _heroRaceMap = new Dictionary<string, int>();

        var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();
        foreach (var hero in heroes)
        {
            if (hero.Race > 0 && !_heroRaceMap.ContainsKey(hero.StringId))
            {
                _heroRaceMap[hero.StringId] = hero.Race;
            }
        }
    }

    public void RestoreHeroRaces()
    {
        if (_heroRaceMap.Count == 0)
            return;

        var restoredCount = 0;
        var heroes = _heroRosterAdapter.GetAllAliveHeroRaces();

        foreach (var hero in heroes)
        {
            if (_heroRaceMap.TryGetValue(hero.StringId, out var savedRace) && hero.Race != savedRace)
            {
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
