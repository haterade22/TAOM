using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.RaceAge;

public class RaceAgeBehavior : CampaignBehaviorBase
{
    private readonly IRaceAgeService _raceAgeService;
    private readonly IHeroAgeAdapter _heroAgeAdapter;
    private readonly IModLogger _logger;
    private readonly List<HeroAgeInfo> _deathList = new List<HeroAgeInfo>();

    public RaceAgeBehavior(
        IRaceAgeService raceAgeService,
        IHeroAgeAdapter heroAgeAdapter,
        IModLogger logger)
    {
        _raceAgeService = raceAgeService;
        _heroAgeAdapter = heroAgeAdapter;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        // Phase 9b #131 R1 — clear race-id cache on new campaign. Cache integer→RaceAgeEntry
        // mapping is process-wide and would serve stale entries if int IDs shift (HeroRace #130
        // showed this can happen). OnSessionLaunched (not OnNewGameCreated) so load also resets.
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _raceAgeService.ResetCache());
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnDailyTick()
    {
        _deathList.Clear();

        foreach (var hero in _heroAgeAdapter.GetAllAliveHeroAges())
        {
            if (_raceAgeService.ShouldDieOfOldAge(hero.Race, hero.Age))
            {
                _deathList.Add(hero);
            }
        }

        foreach (var hero in _deathList)
        {
            _logger.LogInfo($"RaceAge: Hero {hero.HeroId} (race {hero.Race}) died of old age at {hero.Age:F0}");
            _heroAgeAdapter.KillByOldAge(hero.HeroId);
        }
    }
}
