using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.RaceAge;

public class RaceAgeBehavior : CampaignBehaviorBase
{
    private readonly IRaceAgeService _raceAgeService;
    private readonly IHeroAgeAdapter _heroAgeAdapter;
    private readonly IModLogger _logger;

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
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnDailyTick()
    {
        var heroes = _heroAgeAdapter.GetAllAliveHeroAges();
        foreach (var hero in heroes)
        {
            if (_raceAgeService.ShouldDieOfOldAge(hero.Race, hero.Age))
            {
                _logger.LogInfo($"RaceAge: Hero {hero.HeroId} (race {hero.Race}) died of old age at {hero.Age:F0}");
                _heroAgeAdapter.KillByOldAge(hero.HeroId);
            }
        }
    }
}
