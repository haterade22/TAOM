using System.Linq;
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
    public void OfferWaits_ArePinned()
    {
        Assert.AreEqual(120.0, ScenarioRules.OfferPopupWaitSeconds);
        Assert.AreEqual(2.0, ScenarioRules.OfferPopupSettleSeconds);
    }

    [DataTestMethod]
    [DataRow("defend", "taom_story_offer_defend,taom_story_offer_stand")]
    [DataRow("attack", "taom_story_offer_attack,taom_story_offer_hunt")]
    [DataRow("fate", "taom_story_offer_decline")]
    [DataRow("run", "")]
    public void OfferOptionIds_FollowTheChoice(string choice, string ids)
    {
        Assert.AreEqual(ids, string.Join(",", ScenarioRules.OfferOptionIds(choice)));
    }

    [TestMethod]
    public void OfferOptionIds_ArePinnedToTheMenusOptions()
    {
        var menu = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/StoryMissions/Hooks/StoryOfferBehavior.cs", stripComments: true);

        foreach (var id in new[] { "defend", "attack", "fate" }.SelectMany(ScenarioRules.OfferOptionIds).Append(ScenarioRules.ContinueOptionId))
            StringAssert.Contains(menu, "\"" + id + "\"");
    }

    [TestMethod]
    public void OpenedTheWindow_ReadsTheCheatsOpenedLineOnly()
    {
        Assert.IsTrue(ScenarioRules.OpenedTheWindow("[WarChronicle] Opened: 'breaking' is Active until day 9.", "breaking"));
        Assert.IsFalse(ScenarioRules.OpenedTheWindow("[WarChronicle] Not opened: 'breaking' is Skipped; only a Pending event can be opened.", "breaking"));
        Assert.IsFalse(ScenarioRules.OpenedTheWindow("Cheat mode is disabled!", "breaking"));
        Assert.IsFalse(ScenarioRules.OpenedTheWindow("", "breaking"));
    }

    [TestMethod]
    public void OpenedTheWindow_AResolvedAtOnceEventOrAnotherEventIsNotAnOpenWindow()
    {
        // OpenChronicleEventNow answers "'<id>' <State>." when the event resolved at once as text (no window), "is Active until day" for a window.
        Assert.IsFalse(ScenarioRules.OpenedTheWindow("[WarChronicle] Opened: 'breaking' Occurred.", "breaking"));
        Assert.IsFalse(ScenarioRules.OpenedTheWindow("[WarChronicle] Opened: 'hornburg' is Active until day 9.", "breaking"));
    }

    [TestMethod]
    public void StoryStepWait_IsTwoMinutes()
    {
        Assert.AreEqual(120.0, ScenarioRules.StoryStepWaitSeconds);
    }
}
