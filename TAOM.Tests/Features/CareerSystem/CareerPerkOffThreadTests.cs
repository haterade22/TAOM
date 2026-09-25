using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The player falling ends every live ability: the buff tracker clears, the expiry list the mission
/// tick walks is emptied, and buffed allies recompute their stats. <c>OnAgentRemoved</c> is native's to
/// place, and a v1.4.8 player log caught it off the main thread (#634), so that teardown is parked for
/// the next mission tick instead of emptying the list the tick iterates from another thread.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CareerPerkOffThreadTests
{
    private const string HeroId = "main_hero";
    private CareerPerkMissionBehavior _sut = null!;

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        CareerAbilityBuffTracker.ClearAll();
        _sut = new CareerPerkMissionBehavior(
            Substitute.For<ICareerDataService>(),
            Substitute.For<ICareerAbilityService>(),
            Substitute.For<IAbilityActivationController>(),
            Substitute.For<IAbilityEffectExecutor>(),
            Substitute.For<ICareerAgentStatService>(),
            Substitute.For<ICareerConfigProvider>(),
            Substitute.For<IModLogger>());
        _sut.OnMissionTick(0.01f); // the behavior marks the main thread itself
        CareerAbilityBuffTracker.SetBuff(HeroId, new ActiveBuffs());
    }

    [TestCleanup]
    public void Cleanup()
    {
        CareerAbilityBuffTracker.ClearAll();
        MissionThreadGuard.ResetForTests();
    }

    [TestMethod]
    public void EndAbilitiesForFallenHero_OnTheMainThread_ClearsAtOnce()
    {
        _sut.EndAbilitiesForFallenHero(HeroId);

        Assert.IsNull(CareerAbilityBuffTracker.GetBuff(HeroId));
    }

    [TestMethod]
    public void EndAbilitiesForFallenHero_OffTheMainThread_WaitsForTheNextMissionTick()
    {
        OnWorker(() => _sut.EndAbilitiesForFallenHero(HeroId));

        Assert.IsNotNull(CareerAbilityBuffTracker.GetBuff(HeroId), "the worker must not tear the abilities down");

        _sut.OnMissionTick(0.01f);

        Assert.IsNull(CareerAbilityBuffTracker.GetBuff(HeroId));
    }

    [TestMethod]
    public void OnEndMission_DropsAParkedTeardown()
    {
        OnWorker(() => _sut.EndAbilitiesForFallenHero(HeroId));

        _sut.OnEndMissionInternal();
        CareerAbilityBuffTracker.SetBuff(HeroId, new ActiveBuffs()); // the next mission's ability
        _sut.OnMissionTick(0.01f);

        Assert.IsNotNull(CareerAbilityBuffTracker.GetBuff(HeroId), "last mission's death must not end this mission's ability");
    }
}
