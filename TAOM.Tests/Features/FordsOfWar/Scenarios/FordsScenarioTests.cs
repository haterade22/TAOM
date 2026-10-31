using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FordsOfWar.Scenarios;

/// <summary>
/// The five scenarios of the harness (the 2c prevention): the verdict rules judged on evidence the real producers made, in the
/// engine's call order, with its clocks and geometry. A rule that reads FAIL on working producers, or PASS on a fault, fails here.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FordsScenarioTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    private static string Fails(FordsScenario sc) =>
        string.Join("; ", sc.Rows.SelectMany(r => r.Parts.Where(p => p.Result == FordsResult.Fail).Select(p => r.Check + "." + p.Name + " (" + p.Numbers + ")")));

    // --- S1 ------------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void S1_ACleanMatchJudgedAtTheMenu_HasNoFailInAnyRow()
    {
        var sc = FordsScenarios.Get("S1");

        Assert.AreEqual("", Fails(sc));
        Assert.AreEqual(1, sc.FinalBlocks(1), "one final block, written when the menu came back");
        Assert.IsTrue(sc.Report.Run!.Closed);
    }

    [DataTestMethod]
    [DataRow(1, "installed")]
    [DataRow(1, "polled")]
    [DataRow(1, "flow")]
    [DataRow(1, "cancel")]
    [DataRow(3, "started")]
    [DataRow(3, "ids")]
    [DataRow(3, "spawn")]
    [DataRow(4, "countdown")]
    [DataRow(4, "towers")]
    [DataRow(4, "waves")]
    [DataRow(4, "march")]
    [DataRow(4, "fight")]
    [DataRow(4, "killGold")]
    [DataRow(4, "pan")]
    [DataRow(4, "rotate")]
    [DataRow(4, "zoom")]
    [DataRow(4, "inside")]
    [DataRow(4, "ownership")]
    [DataRow(5, "photo")]
    [DataRow(5, "options")]
    [DataRow(6, "grant")]
    [DataRow(6, "buy")]
    [DataRow(6, "feed")]
    [DataRow(7, "field")]
    [DataRow(7, "died")]
    [DataRow(7, "respawn")]
    [DataRow(8, "orders")]
    [DataRow(8, "hold")]
    [DataRow(9, "lock")]
    [DataRow(9, "melee")]
    [DataRow(10, "ownSide")]
    [DataRow(11, "item")]
    [DataRow(11, "hits")]
    [DataRow(12, "started")]
    [DataRow(12, "restored")]
    [DataRow(12, "returned")]
    [DataRow(13, "capture")]
    [DataRow(13, "contested")]
    [DataRow(14, "keep")]
    [DataRow(14, "result")]
    [DataRow(14, "exit")]
    [DataRow(14, "menu")]
    [DataRow(15, "menu")]
    [DataRow(16, "cap")]
    [DataRow(16, "agents")]
    [DataRow(16, "duration")]
    public void S1_TheNamedPartPasses(int check, string part)
    {
        var sc = FordsScenarios.Get("S1");

        Assert.AreEqual(FordsResult.Pass, sc.Result(check, part), sc.Numbers(check, part));
    }

    [TestMethod]
    public void S1_AHeroBoughtAsTheKeepFalls_IsExcusedAndDoesNotTakeAwayTheOtherHeroesPass()
    {
        var sc = FordsScenarios.Get("S1");

        var fielded = sc.Report.Run!.Heroes.Where(h => h.Kind == "fielded" && h.Side == "West").Select(h => h.HeroId).ToList();
        CollectionAssert.AreEquivalent(new[] { "hero_a", "hero_b", "hero_c" }, fielded);
        Assert.IsFalse(sc.Report.Run.Heroes.Any(h => h.Kind == "spawned" && h.HeroId == "hero_c"), "the keep fell before the spawn");
    }

    [TestMethod]
    public void S1_EastFieldsTheSameHeroId_AndWestsMarkStillLinksToWestsSpawn()
    {
        var sc = FordsScenarios.Get("S1");

        var spawned = sc.Report.Run!.Heroes.Where(h => h.Kind == "spawned" && h.HeroId == "hero_a").Select(h => h.Side).Distinct().OrderBy(s => s).ToList();
        CollectionAssert.AreEqual(new[] { "East", "West" }, spawned);
        Assert.AreEqual(FordsResult.Pass, sc.Result(7, "field"));
    }

    [TestMethod]
    public void S1_AHeldLaneStandsOutsideTowerFire()
    {
        var sc = FordsScenarios.Get("S1");

        var lane = sc.Report.Run!.Lanes.Single(l => l.Side == "West" && l.Lane == 1);
        Assert.IsTrue(lane.HoldSamples >= 5, "settled samples: " + lane.HoldSamples);
        Assert.AreEqual(0, lane.MaxInFireRun);
    }

    // --- S2 ------------------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("tower-dropped", 4, "towers")]
    [DataRow("route-b", 3, "started")]
    [DataRow("route-c", 3, "started")]
    [DataRow("spawn-null", 3, "spawn")]
    [DataRow("hp-drift", 9, "lock")]
    [DataRow("warg-stranded", 12, "restored")]
    [DataRow("warg-not-returned", 12, "returned")]
    [DataRow("exit-slow", 14, "exit")]
    public void S2_EachInjectedFault_FailsItsOwnPart(string fault, int check, string part)
    {
        var sc = FordsScenarios.Get(fault);

        Assert.AreEqual(FordsResult.Fail, sc.Result(check, part), sc.Numbers(check, part));
    }

    [TestMethod]
    public void S2_TheEngineAndRulesHitPointsDrifting_IsNamedInTheRow()
    {
        var sc = FordsScenarios.Get("hp-drift");

        StringAssert.Contains(sc.Numbers(9, "lock"), "tower_east_0_0");
    }

    [TestMethod]
    public void S2_ARouteAFailure_IsFailedByTheStartedPartAndPassedByTheFailurePart()
    {
        var sc = FordsScenarios.Get("route-a-slow-close");

        Assert.AreEqual(FordsResult.Fail, sc.Result(3, "started"));
        Assert.AreEqual(FordsResult.Pass, sc.Result(15, "failure"));
        Assert.AreEqual(FordsResult.Fail, sc.Result(15, "exit"), "20 s from EndGame to the close");
    }

    // --- S3 ------------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void S3_NinetySecondsOfPauseDuringAWargReturn_FailsNothing()
    {
        var sc = FordsScenarios.Get("S3-warg");

        Assert.AreEqual(FordsResult.Pass, sc.Result(12, "returned"), sc.Numbers(12, "returned"));
        Assert.IsFalse(sc.Rows.Single(r => r.Check == 12).Parts.Any(p => p.Result == FordsResult.Fail));
        Assert.AreEqual(90.0, sc.Wall - sc.Mt, 1e-6, "the wall clock ran 90 s ahead of the mission clock");
    }

    [TestMethod]
    public void S3_NinetySecondsOfHitchBeforeRouteAsThreeSecondExit_FailsNothing()
    {
        var sc = FordsScenarios.Get("S3-exit");
        var run = sc.Report.Run!;

        Assert.IsTrue(run.End.ExitDueWall - run.Failure!.Wall > 85.0, "the wall gap between the failure and the exit");
        Assert.AreEqual(2.0, run.End.ExitDueMt!.Value - run.Failure.Mt, 1e-6, "the 3 s clock counts the failure tick, and runs in mission seconds");
        Assert.IsFalse(sc.Rows.Single(r => r.Check == 15).Parts.Any(p => p.Result == FordsResult.Fail), Fails(sc));
        Assert.AreEqual(FordsResult.Pass, sc.Result(15, "exit"));
        Assert.AreEqual(FordsResult.Pass, sc.Result(15, "notice"));
    }

    // --- S4 ------------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void S4_TheCameraPushedIntoTheEdge_StaysInsideEvenWhereTheEngineSaysOutside()
    {
        var sc = FordsScenarios.Get("S4");

        Assert.IsFalse(sc.StrictInside(sc.Camera.X, sc.Camera.Y), "the stand-in is strict on the edge, and the clamp parks the camera on it");
        Assert.AreEqual(-150f, sc.Camera.Y, 0.01f);
        Assert.AreEqual(FordsResult.Pass, sc.Result(4, "inside"), sc.Numbers(4, "inside"));
        Assert.AreEqual(0, sc.Report.Run!.Client.OutsideSamples);
    }

    // --- S5 ------------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void S5_ConsoleStarts_AnUnknownArgumentANamedStartAndADefaultStart_PassTheirParts()
    {
        var sc = FordsScenarios.Get("S5-console");

        Assert.AreEqual(FordsResult.Pass, sc.Result(2, "named"), sc.Numbers(2, "named"));
        Assert.AreEqual(FordsResult.Pass, sc.Result(2, "unknown"));
        Assert.AreEqual(FordsResult.Pass, sc.Result(2, "defaults"), sc.Numbers(2, "defaults"));
        Assert.AreEqual(2, sc.Report.Session.RunsBegun);
    }

    [TestMethod]
    public void S5_AfterAFinalizedRun_ACancelAnAbandonAFaultAndAnUnknownStart_EachChangeTheFinalBlock()
    {
        var sc = FordsScenarios.Get("S5-tail");
        var m = sc.Marks;

        Assert.AreEqual("NOT_SEEN", m["final.1.cancel"]);
        Assert.AreEqual("PASS", m["cancel.1.cancel"]);
        Assert.AreEqual(int.Parse(m["final.blocks"]) + 1, int.Parse(m["cancel.blocks"]), "the cancel rewrote the block");
        Assert.AreEqual(int.Parse(m["cancel.blocks"]) + 1, int.Parse(m["abandon.blocks"]), "the abandoned flow rewrote it");
        Assert.AreEqual("PASS", m["abandon.1.flow"]);
        Assert.AreEqual("FAIL", m["fault.1.flow"], "a faulted flow fails the flow part");
        Assert.IsTrue(int.Parse(m["fault.blocks"]) > int.Parse(m["abandon.blocks"]));
        Assert.AreEqual("NOT_SEEN", m["fault.2.unknown"]);
        Assert.AreEqual("PASS", m["unknown.2.unknown"], "an unknown console start is session evidence the finalized run had not judged");
        Assert.AreEqual(int.Parse(m["fault.blocks"]) + 1, int.Parse(m["unknown.blocks"]));
    }

    [TestMethod]
    public void S5_RouteCFollowedByAMenuReturn_FailsNamedOnceAndGetsNoSecondMenuPass()
    {
        var sc = FordsScenarios.Get("S5-tail");
        var m = sc.Marks;

        Assert.AreEqual("FAIL", m["routeC.2.named"], "the record landed after the run was finalized, and the final block was written again with it");
        Assert.AreEqual("2", m["routeC.blocks"], "the failure's pass and the late record's pass");
        Assert.AreEqual("2", m["menuReturn.blocks"], "the menu return adds no pass to a route C run");
        Assert.IsNull(sc.Report.Run!.End.MenuReturnedWall);
    }
}
