using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.RaceAge;

public class RaceAgeBehavior : CampaignBehaviorBase
{
    private readonly IRaceAgeService _raceAgeService;
    private readonly IHeroAgeAdapter _heroAgeAdapter;
    private readonly IModLogger _logger;
    private readonly List<HeroAgeInfo> _deathList = new List<HeroAgeInfo>();

    private readonly ICoopSessionProvider _coopSession;

    public RaceAgeBehavior(
        IRaceAgeService raceAgeService,
        IHeroAgeAdapter heroAgeAdapter,
        IModLogger logger,
        ICoopSessionProvider coopSession)
    {
        _raceAgeService = raceAgeService;
        _heroAgeAdapter = heroAgeAdapter;
        _logger = logger;
        _coopSession = coopSession;
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

    // internal for TAOM.Tests (InternalsVisibleTo, see TAOM.csproj).
    internal void OnDailyTick()
    {
        // CO-OP: host-only. Kills heroes of old age. The global DailyTickEvent DOES fire on a
        // BannerlordCoop client, so an ungated run kills the same heroes locally a second time and
        // desyncs the roster against the host that already replicated the deaths.
        if (!_coopSession.IsAuthority) return;

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
            // Kill first, announce second. The engine defers the death of a hero who is in a
            // MapEvent/siege, so a pre-kill log announced deaths that hadn't happened — and the
            // hero, still over max age, was re-announced on the next daily tick (16 duplicate
            // announcements in the 2026-07-26 session log).
            if (_heroAgeAdapter.KillByOldAge(hero.HeroId))
                _logger.LogInfo($"RaceAge: Hero {hero.HeroId} (race {hero.Race}) died of old age at {hero.Age:F0}");
        }
    }
}
