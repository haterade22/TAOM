using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.PlayerPossession;

namespace TAOM.Tests.Features.PlayerPossession;

[TestClass]
public class PlayerPossessionCaptureTests
{
    [TestMethod]
    public void ChoicesFor_ACareerAutoAssignedWithNoMenuPick_CapturesThatCareer()
    {
        var careerData = Substitute.For<ICareerDataService>();
        careerData.GetCareerStringId("main_hero").Returns("avanc_luth_raider");

        var choices = PlayerPossessionBehavior.ChoicesFor("main_hero", "empire", 0, careerData);

        Assert.AreEqual("main_hero", choices.HeroId);
        Assert.AreEqual("empire", choices.CultureId);
        Assert.AreEqual(0, choices.RaceId);
        Assert.AreEqual("avanc_luth_raider", choices.CareerId);
    }

    [TestMethod]
    public void ChoicesFor_AHeroWithNoCareer_CapturesNone()
    {
        var careerData = Substitute.For<ICareerDataService>();
        careerData.GetCareerStringId("main_hero").Returns((string)null);

        var choices = PlayerPossessionBehavior.ChoicesFor("main_hero", "empire", 0, careerData);

        Assert.IsNull(choices.CareerId);
    }

    [TestMethod]
    public void ChoicesFor_APlayerSwitcherLord_CapturesTheLordsCareerNotTheCreationHeros()
    {
        // After a Player Switcher hand-over, the phase-9 hero is the lord, and the created hero still
        // holds its own career row until the store drops it.
        var careerData = Substitute.For<ICareerDataService>();
        careerData.GetCareerStringId("main_hero").Returns("avanc_luth_raider");
        careerData.GetCareerStringId("lord_1_1").Returns("ram_rider");

        var choices = PlayerPossessionBehavior.ChoicesFor("lord_1_1", "erebor", 0, careerData);

        Assert.AreEqual("lord_1_1", choices.HeroId);
        Assert.AreEqual("ram_rider", choices.CareerId);
    }
}
