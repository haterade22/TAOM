using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Stamina.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The crew steps of the command trial (#1503): which steps exist and when they are armed, which troops they add to the
/// battle, and the rule that turns a watch of one beast's crew into a pass or a fail.
/// </summary>
[TestClass]
public class CrewTrialRulesTests
{
    private static readonly int[] EightWhoShot = { 4, 6, 5, 8, 6, 5, 6, 6 };

    // ---- the rows ----

    [TestMethod]
    public void TheCrewSteps_AreKnownByName_AndNeedTheSeat()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter,crew_mumakil,crew_howdah", out var steps, out var error), error);

        CollectionAssert.AreEqual(new[] { "enter", "crew_mumakil", "crew_howdah" }, steps.Select(s => s.Name).ToArray());
        foreach (var step in steps.Skip(1))
            CollectionAssert.AreEqual(new[] { "enter" }, step.Requires.ToArray(), step.Name);
    }

    [TestMethod]
    public void ACrewStep_WithoutEnter_IsRefused()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("crew_mumakil", out _, out var error));

        StringAssert.Contains(error, "needs step 'enter'");
    }

    // A crew battle has beasts in the cavalry company and another enemy. The Seat's own steps were proved without them, and
    // let_go_on_death kills the smallest company with no hero in it, which in a crew battle holds the three beast riders.
    [DataTestMethod]
    [DataRow("enter,let_go_on_death,crew_mumakil", "let_go_on_death")]
    [DataRow("enter,fly,crew_howdah", "fly")]
    [DataRow("enter,hero_falls,crew_mumakil,crew_howdah", "hero_falls")]
    [DataRow("refuse_in_deployment,enter,crew_mumakil", "refuse_in_deployment")]
    public void ACrewStep_BesideAStepOtherThanEnter_IsRefused_NamingThatStep(string list, string other)
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose(list, out var steps, out var error));

        Assert.AreEqual(0, steps.Count);
        StringAssert.Contains(error, "crew step");
        StringAssert.Contains(error, "'" + other + "'");
    }

    // A list with no steps runs the Seat's and Marshal's Word's rows in the battle they were proved in: no beast is on that field.
    [TestMethod]
    public void ARunWithNoStepList_DoesNotArmTheCrewSteps()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(null, out var steps, out var error), error);

        Assert.IsFalse(steps.Any(s => CrewTrialRows.Rows.Any(c => c.Name == s.Name)));
        CollectionAssert.AreEqual(SeatTrialSteps.All.Select(s => s.Name).ToArray(), steps.Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void KnownSteps_AreTheDefaultOnesThenTheCrewOnesThenTheStaminaOnes_EachNameOnce()
    {
        var known = SeatTrialSteps.Known.Select(s => s.Name).ToArray();

        CollectionAssert.AreEqual(SeatTrialSteps.All.Concat(CrewTrialRows.Rows).Concat(StaminaTrialRows.Rows).Select(s => s.Name).ToArray(), known);
        Assert.AreEqual(known.Length, known.Distinct().Count());
    }

    [TestMethod]
    public void AnUnknownStep_IsNamedWithEveryKnownStep()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("enter,crew_oliphaunt", out _, out var error));

        StringAssert.Contains(error, "unknown step 'crew_oliphaunt'");
        StringAssert.Contains(error, "crew_howdah");
    }

    // ---- the roster ----

    [DataTestMethod]
    [DataRow("enter,crew_mumakil")]
    [DataRow("enter,crew_howdah")]
    [DataRow(" Enter , CREW_MUMAKIL , crew_howdah ")]
    public void AListThatNamesACrewStep_AddsTheBeastsToThePlayersArmy(string steps)
    {
        var roster = SeatTrialRoster.PlayerFor(steps);

        CollectionAssert.AreEqual(SeatTrialRoster.Player.Concat(CrewTrialRows.Beasts).ToArray(), roster.ToArray());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("enter,fly")]
    [DataRow("advance_throwers")]
    public void AListWithNoCrewStep_FieldsNoBeast(string? steps)
    {
        var roster = SeatTrialRoster.PlayerFor(steps);

        Assert.IsFalse(roster.Any(g => CrewTrialRows.Beasts.Any(b => b.Id == g.Id)), steps);
    }

    [TestMethod]
    public void TheBeasts_AreOneMumakilAndTwoElephants()
    {
        CollectionAssert.AreEqual(new[] { ("fow_harad_mumakil_rider", 1), ("fow_harad_elephant_rider", 2) },
            CrewTrialRows.Beasts.Select(g => (g.Id, g.Count)).ToArray());
    }

    // The trial's orc archers killed the mumakil in 38 s (run 20261120-132358), so a crew battle fields no enemy archer.
    [TestMethod]
    public void AListThatNamesACrewStep_MeetsThreeHundredMeleeOrcs_AndNoArcher()
    {
        var enemy = SeatTrialRoster.EnemyFor("enter,crew_howdah");

        CollectionAssert.AreEqual(new[] { ("fow_mordor_orc_fighter", 300) }, enemy.Select(g => (g.Id, g.Count)).ToArray());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("enter,fly")]
    [DataRow("advance_throwers")]
    public void AListWithNoCrewStep_MeetsTheUsualEnemy(string? steps)
    {
        CollectionAssert.AreEqual(SeatTrialRoster.Enemy.ToArray(), SeatTrialRoster.EnemyFor(steps).ToArray());
    }

    [TestMethod]
    public void AMissingEnemyTroop_OfTheUsualArmyOnly_DoesNotRefuseACrewBattle()
    {
        bool Exists(string id) => id != "fow_mordor_orc_hunter";

        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, Exists, "enter,crew_mumakil").Count);
        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, Exists, "enter,fly").ToList(), "character:fow_mordor_orc_hunter");
    }

    [TestMethod]
    public void AListThatNamesAStaminaStep_MeetsTheCrewEnemy_AndFieldsNoBeast()
    {
        var enemy = SeatTrialRoster.EnemyFor("stamina_gate,stamina_walk");

        CollectionAssert.AreEqual(CrewTrialRows.Enemy.ToArray(), enemy.ToArray());
        CollectionAssert.AreEqual(SeatTrialRoster.Player.ToArray(), SeatTrialRoster.PlayerFor("stamina_gate,stamina_walk").ToArray());
    }

    [TestMethod]
    public void AMissingOrcHunter_DoesNotRefuseAStaminaBattle()
    {
        bool Exists(string id) => id != "fow_mordor_orc_hunter";

        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, Exists, "stamina_fight").Count);
        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, Exists, "enter,fly").ToList(), "character:fow_mordor_orc_hunter");
    }

    [TestMethod]
    public void AMissingOrcFighter_RefusesAStaminaBattle()
    {
        bool Exists(string id) => id != "fow_mordor_orc_fighter";

        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, Exists, "stamina_gate").ToList(), "character:fow_mordor_orc_fighter");
    }

    [TestMethod]
    public void AMissingBeastTroop_RefusesTheStart_OnlyWhenACrewStepIsNamed()
    {
        bool Exists(string id) => id != "fow_harad_mumakil_rider";

        CollectionAssert.Contains(SeatTrialRoster.Missing(_ => true, Exists, "enter,crew_mumakil").ToList(), "character:fow_harad_mumakil_rider");
        Assert.AreEqual(0, SeatTrialRoster.Missing(_ => true, Exists, "enter,fly").Count);
    }

    // The beast is the rider's mount, so a troop that loses its mount in the data fields no crew, and the step then fails on no-crew in the game.
    [DataTestMethod]
    [DataRow("fow_harad_mumakil_rider", "Item.taom_mumakil")]
    [DataRow("fow_harad_elephant_rider", "Item.taom_war_elephant")]
    public void EachBeastTroop_IsInTheFordsTroopFile_AndRidesItsBeast(string troopId, string mount)
    {
        var file = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fords_of_war", "fords_troops.xml");
        var troop = XDocument.Load(file).Descendants("NPCCharacter").Single(c => (string?)c.Attribute("id") == troopId);

        var horses = troop.Descendants("equipment").Where(e => (string?)e.Attribute("slot") == "Horse").Select(e => (string?)e.Attribute("id")).ToList();
        Assert.IsTrue(horses.Count > 0, troopId + " has no Horse slot");
        Assert.IsTrue(horses.All(h => h == mount), troopId + " rides " + string.Join(", ", horses));
    }

    // ---- the watch ----

    [TestMethod]
    public void EveryoneShot_NeedsAnArrowFromEverySeat()
    {
        Assert.IsTrue(CrewTrialRules.EveryoneShot(EightWhoShot));
        Assert.IsFalse(CrewTrialRules.EveryoneShot(new[] { 4, 0, 5 }));
        Assert.IsFalse(CrewTrialRules.EveryoneShot(new int[0]));
    }

    [TestMethod]
    public void TheWatch_GoesOnUntilEveryoneShot_AndTheShortestWatchHasPassed()
    {
        Assert.IsFalse(CrewTrialRules.WatchIsOver(CrewTrialRules.MinWatchSeconds - 1f, everyoneShot: true, beastAlive: true));
        Assert.IsTrue(CrewTrialRules.WatchIsOver(CrewTrialRules.MinWatchSeconds, everyoneShot: true, beastAlive: true));
        Assert.IsFalse(CrewTrialRules.WatchIsOver(CrewTrialRules.WatchLimitSeconds - 1f, everyoneShot: false, beastAlive: true));
    }

    [TestMethod]
    public void TheWatch_EndsAtItsLimit_OrWhenTheBeastIsGone()
    {
        Assert.IsTrue(CrewTrialRules.WatchIsOver(CrewTrialRules.WatchLimitSeconds, everyoneShot: false, beastAlive: true));
        Assert.IsTrue(CrewTrialRules.WatchIsOver(1f, everyoneShot: false, beastAlive: false));
    }

    [TestMethod]
    public void TheWatch_WithATimeThatIsNoNumber_Ends()
    {
        Assert.IsTrue(CrewTrialRules.WatchIsOver(float.NaN, everyoneShot: false, beastAlive: true));
    }

    // ---- the verdict ----

    [TestMethod]
    public void ACrewThatStayedShotAndRode_Passes()
    {
        Assert.IsTrue(CrewTrialRules.Passes(seats: 8, EightWhoShot, rescues: 0, moved: 42f));
    }

    [TestMethod]
    public void AShotListThatIsNotOneNumberForEachSeat_Fails()
    {
        Assert.IsFalse(CrewTrialRules.Passes(seats: 9, EightWhoShot, rescues: 0, moved: 42f));
    }

    [TestMethod]
    public void ABeastWithNoSeat_Fails()
    {
        Assert.IsFalse(CrewTrialRules.Passes(seats: 0, new int[0], rescues: 0, moved: 42f));
    }

    [TestMethod]
    public void AnArcherWhoNeverShot_Fails()
    {
        Assert.IsFalse(CrewTrialRules.Passes(seats: 2, new[] { 9, 0 }, rescues: 0, moved: 42f));
    }

    // The rescue teleport is the sign that the hold failed (CrewSeatChecks.Held).
    [TestMethod]
    public void OneRescueTeleport_Fails()
    {
        Assert.IsFalse(CrewTrialRules.Passes(seats: 8, EightWhoShot, rescues: 1, moved: 42f));
    }

    // A crew on a beast that stood still proves nothing about the moving case, which is the one #1482 was about.
    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(9.9f)]
    [DataRow(float.NaN)]
    public void ABeastThatDidNotMoveFarEnough_Fails(float moved)
    {
        Assert.IsFalse(CrewTrialRules.Passes(seats: 8, EightWhoShot, rescues: 0, moved));
    }

    [TestMethod]
    public void TheMoveLimit_IsTenMetres()
    {
        Assert.AreEqual(10f, CrewTrialRules.MovedMetres);
        Assert.IsTrue(CrewTrialRules.Passes(seats: 8, EightWhoShot, rescues: 0, moved: 10f));
    }

    [TestMethod]
    public void TheFacts_NameEverySeatsShots_InSeatOrder()
    {
        var facts = CrewTrialRules.Facts(seats: 3, new[] { 4, 0, 12 }, rescues: 2, moved: 31.26f, beastAlive: false, watched: 44.04f, pictures: 3);

        Assert.AreEqual("seats=3 shots=4,0,12 rescues=2 moved=31.3 beast=gone watched=44.0 pictures=3", facts);
    }

    [TestMethod]
    public void TheFacts_OfABeastWithNoSeat_SayNone()
    {
        var facts = CrewTrialRules.Facts(seats: 0, new int[0], rescues: 0, moved: 0f, beastAlive: true, watched: 0f, pictures: 0);

        StringAssert.Contains(facts, "shots=none");
        StringAssert.Contains(facts, "beast=alive");
    }

    [TestMethod]
    public void ThePictures_AreTakenInTheFirstWatch_TheFirstOneClose()
    {
        var times = CrewTrialRules.PictureSeconds;

        Assert.IsTrue(times.Count >= 2);
        CollectionAssert.AreEqual(times.OrderBy(t => t).ToArray(), times.ToArray());
        Assert.IsTrue(times.Last() <= CrewTrialRules.MinWatchSeconds, "the shortest watch still takes every picture");
        Assert.AreEqual("hud_crew_mumakil_2.bmp", CrewTrialRules.PictureName("crew_mumakil", 2));
    }
}
