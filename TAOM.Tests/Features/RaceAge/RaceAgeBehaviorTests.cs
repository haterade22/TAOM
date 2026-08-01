using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.RaceAge;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.RaceAge;

// Regression cover for the "died of old age" double-announcement found in the 2026-07-26 session log:
// 238 death lines for 222 distinct heroes — 16 heroes announced twice, one campaign day apart, at the
// same age. Root cause: the behavior logged BEFORE the kill, and KillByOldAge returned void, so it
// could not report that KillCharacterAction.ApplyByOldAge had silently no-opped (it early-returns
// while the hero is in a MapEvent/SiegeEvent, and for the player character). The hero survived the
// day, still matched ShouldDieOfOldAge on the next daily tick, and was announced again.
[TestClass]
public class RaceAgeBehaviorTests
{
    private IRaceAgeService _raceAgeService;
    private IHeroAgeAdapter _heroAgeAdapter;
    private IModLogger _logger;
    private ICoopSessionProvider _coopSession;
    private RaceAgeBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _raceAgeService = Substitute.For<IRaceAgeService>();
        _heroAgeAdapter = Substitute.For<IHeroAgeAdapter>();
        _logger = Substitute.For<IModLogger>();
        // Authority = true reproduces singleplayer / co-op host, which is what every existing
        // assertion in this class is about. The client-stands-down path is pinned separately below.
        _coopSession = Substitute.For<ICoopSessionProvider>();
        _coopSession.IsAuthority.Returns(true);
        _sut = new RaceAgeBehavior(_raceAgeService, _heroAgeAdapter, _logger, _coopSession);
    }

    private void GivenHero(string id, int race, float age, bool shouldDie, bool killSucceeds)
    {
        _heroAgeAdapter.GetAllAliveHeroAges()
            .Returns(new List<HeroAgeInfo> { new HeroAgeInfo(id, race, age) });
        _raceAgeService.ShouldDieOfOldAge(race, age).Returns(shouldDie);
        _heroAgeAdapter.KillByOldAge(id).Returns(killSucceeds);
    }

    [TestMethod]
    public void OnDailyTick_HeroOverMaxAgeAndKillSucceeds_LogsTheDeath()
    {
        GivenHero("lord_MM4_4", race: 4, age: 60f, shouldDie: true, killSucceeds: true);

        _sut.OnDailyTick();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("lord_MM4_4") && s.Contains("died of old age")));
    }

    // The bug: the announcement must not fire when the engine refused the kill.
    [TestMethod]
    public void OnDailyTick_HeroOverMaxAgeButKillNoOps_DoesNotLogTheDeath()
    {
        GivenHero("lord_GB5_6", race: 11, age: 50f, shouldDie: true, killSucceeds: false);

        _sut.OnDailyTick();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("died of old age")));
    }

    // A hero the engine refuses to kill stays over max age and is re-evaluated the next day. Across
    // two ticks the mod must announce the death exactly once — on the tick where it actually landed.
    [TestMethod]
    public void OnDailyTick_KillNoOpsThenSucceedsNextDay_LogsExactlyOnce()
    {
        GivenHero("lord_GB5_6", race: 11, age: 50f, shouldDie: true, killSucceeds: false);
        _sut.OnDailyTick();

        _heroAgeAdapter.KillByOldAge("lord_GB5_6").Returns(true);
        _sut.OnDailyTick();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("died of old age")));
    }

    [TestMethod]
    public void OnDailyTick_HeroUnderMaxAge_NeitherKillsNorLogs()
    {
        GivenHero("lord_EW1_1", race: 0, age: 30f, shouldDie: false, killSucceeds: true);

        _sut.OnDailyTick();

        _heroAgeAdapter.DidNotReceive().KillByOldAge(Arg.Any<string>());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // --- Co-op authority gate (#370 / BannerlordCoop interop) -----------------------------------

    [TestMethod]
    public void OnDailyTick_CoopClient_DoesNotEvaluateAnyHero()
    {
        // BannerlordCoop does NOT suppress the client's global DailyTickEvent (only the per-entity
        // tickers), so without this gate a client re-runs old-age death evaluation on heroes the
        // host has already killed and replicated. Assert the behaviour never even READS the roster,
        // not merely that it kills nobody — an early return is the contract.
        _coopSession.IsAuthority.Returns(false);

        _sut.OnDailyTick();

        _heroAgeAdapter.DidNotReceive().GetAllAliveHeroAges();
    }

    [TestMethod]
    public void OnDailyTick_CoopHostOrSingleplayer_EvaluatesHeroes()
    {
        // The complement, so the gate cannot be "fixed" by disabling the feature outright.
        _coopSession.IsAuthority.Returns(true);
        _heroAgeAdapter.GetAllAliveHeroAges().Returns(new List<HeroAgeInfo>());

        _sut.OnDailyTick();

        _heroAgeAdapter.Received(1).GetAllAliveHeroAges();
    }
}
