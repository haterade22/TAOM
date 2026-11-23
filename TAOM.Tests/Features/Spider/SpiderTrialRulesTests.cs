using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Spider;
using TAOM.Features.Stamina.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Spider;

/// <summary>
/// The spider gait step of the command trial (#1541): when it exists and is armed, which troops it adds to the battle, and the
/// rule that turns the speeds of a stand, a charge and a stop into a pass or a fail.
/// </summary>
[TestClass]
public class SpiderTrialRulesTests
{
    // ---- the rows ----

    [TestMethod]
    public void TheSpiderStep_IsKnownByName_NeedsTheSeat_AndHasTheBudgetOfTheCrewSteps()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter,spider_gaits", out var steps, out var error), error);

        CollectionAssert.AreEqual(new[] { "enter", "spider_gaits" }, steps.Select(s => s.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "enter" }, steps[1].Requires.ToArray());
        Assert.AreEqual(240f, steps[1].BudgetSeconds);
    }

    [TestMethod]
    public void TheSpiderStep_WithoutEnter_IsRefused()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("spider_gaits", out _, out var error));

        StringAssert.Contains(error, "needs step 'enter'");
    }

    [TestMethod]
    public void TheSpiderStep_IsKnown_ButNotInTheListARunWithNoStepsArms()
    {
        Assert.IsTrue(SeatTrialSteps.Known.Any(s => s.Name == "spider_gaits"));
        Assert.IsFalse(SeatTrialSteps.All.Any(s => s.Name == "spider_gaits"));
        Assert.IsTrue(SeatTrialSteps.TryChoose(null, out var steps, out var error), error);
        Assert.IsFalse(steps.Any(s => s.Name == "spider_gaits"));
    }

    [DataTestMethod]
    [DataRow("enter,fly,spider_gaits", "fly")]
    [DataRow("enter,let_go_on_death,spider_gaits", "let_go_on_death")]
    [DataRow("enter,crew_mumakil,spider_gaits", "crew_mumakil")]
    [DataRow("enter,stamina_gate,spider_gaits", "stamina_gate")]
    [DataRow("refuse_in_deployment,enter,spider_gaits", "refuse_in_deployment")]
    public void TheSpiderStep_BesideAStepOtherThanEnter_IsRefused_NamingThatStep(string list, string other)
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose(list, out var steps, out var error));

        Assert.AreEqual(0, steps.Count);
        StringAssert.Contains(error, "spider step");
        StringAssert.Contains(error, "'" + other + "'");
    }

    [TestMethod]
    public void TheSpiderRule_NamesTheFirstOtherStep_AndNothingForEnterAlone()
    {
        Assert.IsNull(SpiderTrialRows.StepThatCannotShareASpiderBattle(new[] { "enter", "spider_gaits" }));
        Assert.IsNull(SpiderTrialRows.StepThatCannotShareASpiderBattle(new[] { "enter", "fly", "crew_howdah" }));
        Assert.AreEqual("fly", SpiderTrialRows.StepThatCannotShareASpiderBattle(new[] { "enter", "fly", "spider_gaits" }));
    }

    // ---- the roster ----

    [DataTestMethod]
    [DataRow("enter,spider_gaits")]
    [DataRow(" Enter , SPIDER_GAITS ")]
    public void AListThatNamesTheSpiderStep_AddsTwelveSpiderRidersToThePlayersArmy(string steps)
    {
        var roster = SeatTrialRoster.PlayerFor(steps);

        CollectionAssert.AreEqual(SeatTrialRoster.Player.Concat(SpiderTrialRows.Riders).ToArray(), roster.ToArray());
        CollectionAssert.AreEqual(new[] { ("fow_goblin_spider_rider", 12) }, SpiderTrialRows.Riders.Select(g => (g.Id, g.Count)).ToArray());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("enter,fly")]
    [DataRow("enter,crew_mumakil")]
    [DataRow("stamina_gate")]
    public void AListWithNoSpiderStep_FieldsNoSpiderRider(string? steps)
    {
        var roster = SeatTrialRoster.PlayerFor(steps);

        Assert.IsFalse(roster.Any(g => SpiderTrialRows.Riders.Any(r => r.Id == g.Id)), steps);
    }

    [TestMethod]
    public void AListThatNamesTheSpiderStep_MeetsTheMeleeOrcs_AndNoArcher()
    {
        CollectionAssert.AreEqual(CrewTrialRows.Enemy.ToArray(), SeatTrialRoster.EnemyFor("enter,spider_gaits").ToArray());
    }

    [TestMethod]
    public void AMissingSpiderRiderTroop_RefusesTheStart_OnlyWhenTheSpiderStepIsNamed()
    {
        bool Exists(string id) => id != "fow_goblin_spider_rider";

        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, Exists, "enter,spider_gaits").ToList(), "character:fow_goblin_spider_rider");
        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, Exists, "enter,fly").Count);
    }

    // The spider is the rider's mount, so a troop that loses its mount in the data fields no spider, and the step then fails on no-spiders in the game.
    [TestMethod]
    public void TheSpiderRiderTroop_IsInTheFordsTroopFile_AndHasASpiderMountInEveryBattleSet()
    {
        var file = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fords_of_war", "fords_troops.xml");
        var troop = XDocument.Load(file).Descendants("NPCCharacter").Single(c => (string?)c.Attribute("id") == "fow_goblin_spider_rider");

        var horses = troop.Descendants("equipment").Where(e => (string?)e.Attribute("slot") == "Horse").Select(e => (string?)e.Attribute("id")).ToList();
        Assert.IsTrue(horses.Count > 0, "no Horse slot");
        Assert.IsTrue(horses.All(h => h != null && h.StartsWith("Item.spider_mount_")), string.Join(", ", horses));
    }

    // ---- the verdict ----

    [TestMethod]
    public void SpidersThatStoodAndRan_Pass()
    {
        Assert.IsTrue(SpiderTrialRules.Passes(riders: 12, stand: 0.2f, top: 8.4f, pictures: 4));
    }

    [TestMethod]
    public void NoRider_Fails()
    {
        Assert.IsFalse(SpiderTrialRules.Passes(riders: 0, stand: 0f, top: 8.4f, pictures: 4));
    }

    [DataTestMethod]
    [DataRow(2.9f)]
    [DataRow(0f)]
    [DataRow(float.NaN)]
    [DataRow(float.NegativeInfinity)]
    public void SpidersThatDidNotRunFastEnough_Fail(float top)
    {
        Assert.IsFalse(SpiderTrialRules.Passes(riders: 12, stand: 0.2f, top, pictures: 4));
    }

    [DataTestMethod]
    [DataRow(1.6f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void SpidersThatMovedWhileHeld_Fail(float stand)
    {
        Assert.IsFalse(SpiderTrialRules.Passes(riders: 12, stand, top: 8.4f, pictures: 4));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(5)]
    public void ARunWithoutFourPictures_Fails(int pictures)
    {
        Assert.IsFalse(SpiderTrialRules.Passes(riders: 12, stand: 0.2f, top: 8.4f, pictures));
    }

    [TestMethod]
    public void TheSpeedLimits_AreOnePointFiveAtMostWhenHeld_AndThreeAtLeastWhenCharged()
    {
        Assert.AreEqual(1.5f, SpiderTrialRules.StandAtMostMetresPerSecond);
        Assert.AreEqual(3f, SpiderTrialRules.TopAtLeastMetresPerSecond);
        Assert.IsTrue(SpiderTrialRules.Passes(riders: 1, stand: 1.5f, top: 3f, pictures: 4));
    }

    // The riders may be in a fight by the end of the stop wait, so neither fact is in the rule: a person reads them.
    [TestMethod]
    public void TheStopSpeed_AndTheMountedCount_AreNotInTheRule()
    {
        var rule = typeof(SpiderTrialRules).GetMethod("Passes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        CollectionAssert.AreEqual(new[] { "riders", "stand", "top", "pictures" }, rule.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    public void TheFacts_AreKeyValueWords_WithOneDecimalAndTheInvariantCulture()
    {
        var current = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var facts = SpiderTrialRules.Facts(riders: 12, mounted: 9, stand: 0.04f, top: 8.46f, stop: 1.25f, pictures: 4);

            Assert.AreEqual("riders=12 mounted=9 stand=0.0 top=8.5 stop=1.3 pictures=4", facts);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = current;
        }
    }

    [TestMethod]
    public void ThePhases_AndPictures_FitInsideTheBudget()
    {
        var longest = SpiderTrialRules.SpiderWaitSeconds + SpiderTrialRules.StandSeconds + SpiderTrialRules.ChargeSeconds + SpiderTrialRules.StopSeconds
            + 3f * SeatTrialRules.ShortWaitSeconds;

        Assert.IsTrue(SpiderTrialRows.Rows[0].BudgetSeconds >= longest + 10f, "240 s for a run of up to " + longest + " s");
        Assert.AreEqual(SpiderTrialRules.PictureCount, 2 + SpiderTrialRules.ChargePictureSeconds.Count);
        Assert.IsTrue(SpiderTrialRules.ChargePictureSeconds.All(t => t < SpiderTrialRules.ChargeSeconds));
        Assert.IsTrue(SpiderTrialRules.StopWindowSeconds < SpiderTrialRules.StopSeconds);
        Assert.IsTrue(SpiderTrialRules.StandWindowSeconds < SpiderTrialRules.StandSeconds);
    }

    [TestMethod]
    public void ThePictureName_IsOneTheRunnerTurnsIntoAPng()
    {
        Assert.AreEqual("hud_spider_gaits_2.bmp", SeatTrialRules.PictureName("spider_gaits", 2));
    }
}
