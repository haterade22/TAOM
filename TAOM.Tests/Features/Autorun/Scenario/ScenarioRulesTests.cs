using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Autorun.Scenario;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.Autorun.Scenario;

[TestClass]
public class ScenarioRulesTests
{
    [TestMethod]
    public void MenuSettled_FourSecondsHeld_IsTrue()
    {
        Assert.IsFalse(ScenarioRules.MenuSettled(3.9));
        Assert.IsTrue(ScenarioRules.MenuSettled(4.0));
        Assert.IsFalse(ScenarioRules.MenuSettled(double.NaN));
    }

    [TestMethod]
    public void MapSettled_FiveSecondsHeld_IsTrue()
    {
        Assert.IsFalse(ScenarioRules.MapSettled(4.9));
        Assert.IsTrue(ScenarioRules.MapSettled(5.0));
        Assert.IsFalse(ScenarioRules.MapSettled(double.NaN));
    }

    [TestMethod]
    public void Waiting_UnderTheLimit_IsTrueAndNaNExpires()
    {
        Assert.IsTrue(ScenarioRules.Waiting(599.0, 600.0));
        Assert.IsFalse(ScenarioRules.Waiting(600.0, 600.0));
        Assert.IsFalse(ScenarioRules.Waiting(double.NaN, 600.0));
        Assert.IsFalse(ScenarioRules.Waiting(1.0, double.NaN));
    }

    [TestMethod]
    public void WithinWallLimit_AtTheLimit_IsTrueAndNaNExceeds()
    {
        Assert.IsTrue(ScenarioRules.WithinWallLimit(3600.0, 3600.0));
        Assert.IsFalse(ScenarioRules.WithinWallLimit(3600.1, 3600.0));
        Assert.IsFalse(ScenarioRules.WithinWallLimit(double.NaN, 3600.0));
        Assert.IsFalse(ScenarioRules.WithinWallLimit(1.0, double.NaN));
    }

    [TestMethod]
    public void StatusDue_FiveSecondsSinceTheLast_IsTrue()
    {
        Assert.IsFalse(ScenarioRules.StatusDue(14.9, 10.0));
        Assert.IsTrue(ScenarioRules.StatusDue(15.0, 10.0));
        Assert.IsFalse(ScenarioRules.StatusDue(double.NaN, 10.0));
    }

    [TestMethod]
    public void OnMap_MapAndMapMenuOnly()
    {
        Assert.IsTrue(ScenarioRules.OnMap(AutorunScreen.Map));
        Assert.IsTrue(ScenarioRules.OnMap(AutorunScreen.MapMenu));
        Assert.IsFalse(ScenarioRules.OnMap(AutorunScreen.MainMenu));
        Assert.IsFalse(ScenarioRules.OnMap(AutorunScreen.Other));
        Assert.IsFalse(ScenarioRules.OnMap(AutorunScreen.CharacterCreation));
        Assert.IsFalse(ScenarioRules.OnMap(AutorunScreen.Mission));
    }

    [TestMethod]
    public void WholeDay_FiniteNonNegative_Floors()
    {
        Assert.AreEqual(0, ScenarioRules.WholeDay(0.99));
        Assert.AreEqual(12, ScenarioRules.WholeDay(12.5));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    [DataRow(-0.5)]
    [DataRow(-2.5)]
    [DataRow(1e12)]
    public void WholeDay_NotARealDay_IsUnknown(double elapsed)
    {
        Assert.AreEqual(ScenarioRules.UnknownDay, ScenarioRules.WholeDay(elapsed));
    }

    [TestMethod]
    public void DaysDone_WantedDaysPassed_IsTrue()
    {
        Assert.IsFalse(ScenarioRules.DaysDone(12, 10, 3));
        Assert.IsTrue(ScenarioRules.DaysDone(13, 10, 3));
        Assert.IsTrue(ScenarioRules.DaysDone(20, 10, 3));
    }

    [TestMethod]
    public void DaysDone_UnknownDay_IsFalse()
    {
        Assert.IsFalse(ScenarioRules.DaysDone(ScenarioRules.UnknownDay, 10, 3));
        Assert.IsFalse(ScenarioRules.DaysDone(13, ScenarioRules.UnknownDay, 3));
    }

    [TestMethod]
    public void TitleLogged_FirstTwentyOnly()
    {
        Assert.IsTrue(ScenarioRules.TitleLogged(1));
        Assert.IsTrue(ScenarioRules.TitleLogged(20));
        Assert.IsFalse(ScenarioRules.TitleLogged(21));
    }

    [TestMethod]
    public void DaysRun_BothKnown_IsTheDifference()
    {
        Assert.AreEqual(3, ScenarioRules.DaysRun(10, 13));
    }

    [TestMethod]
    public void DaysRun_AnUnknownEnd_IsZero()
    {
        Assert.AreEqual(0, ScenarioRules.DaysRun(ScenarioRules.UnknownDay, 13));
        Assert.AreEqual(0, ScenarioRules.DaysRun(10, ScenarioRules.UnknownDay));
        Assert.AreEqual(0, ScenarioRules.DaysRun(10, 9));
    }

    [TestMethod]
    public void MissionReason_WonOrLost_IsNull()
    {
        Assert.IsNull(ScenarioRules.MissionReason(StoryOutcome.Won));
        Assert.IsNull(ScenarioRules.MissionReason(StoryOutcome.Lost));
    }

    [TestMethod]
    public void MissionReason_Undecided_IsMissionUndecided()
    {
        Assert.AreEqual("mission_undecided", ScenarioRules.MissionReason(StoryOutcome.Undecided));
    }

    [TestMethod]
    public void MissionReason_None_IsMissionNoVerdict()
    {
        Assert.AreEqual("mission_no_verdict", ScenarioRules.MissionReason(null));
    }

    [TestMethod]
    public void StoryStepWait_IsTwoMinutes()
    {
        Assert.AreEqual(120.0, ScenarioRules.StoryStepWaitSeconds);
    }
}
