using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Marshal's Word's field trial steps (build-spec section 11) against the stand-in: each step passes on a world where the feature works and
/// fails with its own facts when the one thing it proves is broken. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialMarshalStepsTests
{
    private static readonly string[] Rows =
    {
        "queue_two_moves", "direct_order_clears", "array_keeps_offsets", "arrive_together", "volley_holds", "volley_looses", "mark_keeps_order",
        "outline_clears", "volley_massed", "square_side_kept", "spacing_one_rider", "hold_line_proof", "hold_line_slot", "hold_line_strike", "hold_line_flag",
    };

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;
    private List<string> _finished = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
        _finished = new List<string>();
    }

    private CommandTrialScript Run(params string[] steps)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(string.Join(",", steps), out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 400f);
        Assert.IsTrue(script.IsDone, "the run ended");
        return script;
    }

    private string Line(string step) =>
        _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private void AssertPasses(string step)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=PASS", line);
    }

    private void AssertFails(string step, params string[] mentions)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    // ---- The table ----

    [TestMethod]
    public void TheRows_AreTheTablesRows_InItsOrder_AfterTheSeatsRows()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        CollectionAssert.AreEqual(Rows, names.Skip(names.Count - Rows.Length).ToArray());
        CollectionAssert.AreEqual(Rows, MarshalTrialRows.Rows.Select(s => s.Name).ToArray());
    }

    [TestMethod]
    public void EveryRow_HasAHandler_AndABudget_AndOnlyTheReleaseNeedsAnother()
    {
        foreach (var step in Rows)
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), step);
            var row = MarshalTrialRows.Rows.Single(r => r.Name == step);
            Assert.IsTrue(row.BudgetSeconds >= 30f, step);
            CollectionAssert.AreEqual(step == "volley_looses" ? new[] { "volley_holds" } : new string[0], row.Requires.ToArray(), step);
        }

        Assert.IsFalse(SeatTrialSteps.TryChoose("volley_looses", out _, out var error));
        StringAssert.Contains(error, "volley_holds");
    }

    [TestMethod]
    public void TheMeleeMarkStep_IsGone_BecauseOnlyARangedCompanyOnStopIsJudged()
    {
        Assert.IsFalse(SeatTrialSteps.TryChoose("mark_melee_on_move", out _, out var error));
        StringAssert.Contains(error, "mark_melee_on_move");
    }

    [TestMethod]
    public void TheMarshalSteps_NeedNoSeatStep_SoEachStageRunsAsItsOwnSubset()
    {
        foreach (var step in Rows.Where(r => r != "volley_looses"))
            Assert.IsTrue(SeatTrialSteps.TryChoose(step, out _, out var error), error);
    }

    // ---- A world that works ----

    [TestMethod]
    public void EveryStep_PassesOnAWorldThatWorks_InOneRun()
    {
        Run(Rows);

        foreach (var step in Rows)
            AssertPasses(step);
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void EveryWrite_RunsInTheMissionTick_AfterItsMarker()
    {
        Run(Rows);

        Assert.IsTrue(_world.Writes.Count > 12, "the world saw the writes: " + _world.Writes.Count);
        CollectionAssert.AreEqual(Array.Empty<string>(), _world.WritesOutsideTheMissionTick.ToArray(),
            "an agent, formation or mission flag write runs in a step's code only after yield return Verdict.InMissionTick");
    }

    // ---- M1 ----

    [TestMethod]
    public void QueueTwoMoves_Passes_AndTheLineNamesTheFactsItJudged()
    {
        Run("queue_two_moves");

        var line = Line("queue_two_moves");
        Assert.IsTrue(float.Parse(Fact(line, "second_at")) - float.Parse(Fact(line, "first_at")) >= MarshalTrialRules.LegGapSeconds, line);
        Assert.AreEqual("0", Fact(line, "by_patience"));
        Assert.AreEqual("0", Fact(line, "left"));
        Assert.AreEqual("1", Fact(line, "captured"), "the capture stored the second Move, and only it: the first Move was a direct order");
        Assert.AreEqual("2", Fact(line, "arrival"), "the wait for the first Move and the queued Move both ended with reason=arrival");
    }

    [TestMethod]
    public void QueueTwoMoves_WhenBothStepsEndOtherwiseThanByArrival_Fails_SoTwoFinishedStepsAreNotTwoArrivals()
    {
        _world.StepsEndOtherwise = true;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "arrival=0");
        Assert.AreEqual("2", Fact(Line("queue_two_moves"), "finished"), "the steps did finish: only their reason is wrong");
    }

    [TestMethod]
    public void QueueTwoMoves_WhenTheKeyArmsOverAPanelTheEngineShut_Fails_AnOrderWithThePanelShutIsNotACapture()
    {
        _world.PanelShutsWhenTheKeyIsHeld = true;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "reason=not-armed-at-order", "panel=0", "armed=1");
    }

    [TestMethod]
    public void QueueTwoMoves_TheFirstMoveShutsThePanel_SoTheStepOpensItAgainBeforeItHoldsTheKey()
    {
        Run("queue_two_moves");

        AssertPasses("queue_two_moves");
        Assert.AreEqual(2, _world.Writes.Count(w => w == "open-panel"), "opened for the first Move, and again for the queued one: " + string.Join(",", _world.Writes));
    }

    [TestMethod]
    public void QueueTwoMoves_WhenTheOrderIsNotCaptured_Fails_SoTheSecondMoveReplacedTheFirst()
    {
        _world.QueueCaptures = false;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "captured=0");
    }

    [TestMethod]
    public void QueueTwoMoves_WhenTheKeyNeverArms_FailsNamingTheKey()
    {
        _world.QueueKeyArms = false;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "reason=queue-armed-never");
    }

    [TestMethod]
    public void QueueTwoMoves_WhenAStepEndsByPatience_Fails()
    {
        _world.StepsEndByPatience = true;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "by_patience=");
    }

    [TestMethod]
    public void QueueTwoMoves_WithNoCompany_Fails()
    {
        _world.Companies.Clear();

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "reason=no-company");
    }

    [TestMethod]
    public void DirectOrderClears_Passes_WithAQueuedStepBeforeAndNoneAfter()
    {
        Run("direct_order_clears");

        var line = Line("direct_order_clears");
        AssertPasses("direct_order_clears");
        Assert.IsTrue(int.Parse(Fact(line, "before")) >= 1);
        Assert.AreEqual("0", Fact(line, "after"));
    }

    [TestMethod]
    public void DirectOrderClears_WhenTheDirectOrderKeepsTheQueue_Fails()
    {
        _world.DirectOrderClearsTheQueue = false;

        Run("direct_order_clears");

        AssertFails("direct_order_clears");
        Assert.AreNotEqual("0", Fact(Line("direct_order_clears"), "after"));
    }

    [TestMethod]
    public void DirectOrderClears_TheFirstMoveIsDirect_AndOnlyTheSecondIsCaptured()
    {
        Run("direct_order_clears");

        AssertPasses("direct_order_clears");
        Assert.AreEqual("1", Fact(Line("direct_order_clears"), "captured"), "a leftover key from an earlier step would take the first Move as well");
    }

    [TestMethod]
    public void DirectOrderClears_WhenTheKeyArmsOverAPanelTheEngineShut_Fails()
    {
        _world.PanelShutsWhenTheKeyIsHeld = true;

        Run("direct_order_clears");

        AssertFails("direct_order_clears", "reason=not-armed-at-order", "panel=0");
    }

    [TestMethod]
    public void DirectOrderClears_WhenNothingWasQueued_Fails_SoAnEmptyQueueIsNotAClear()
    {
        _world.QueueCaptures = false;

        Run("direct_order_clears");

        AssertFails("direct_order_clears", "before=0");
    }

    // ---- M2 ----

    [TestMethod]
    public void ArrayKeepsOffsets_Passes_AfterAStaggerAndADragOnTheirOwnFront()
    {
        Run("array_keeps_offsets");

        var line = Line("array_keeps_offsets");
        AssertPasses("array_keeps_offsets");
        Assert.IsTrue(float.Parse(Fact(line, "stagger")) >= MarshalTrialRules.StaggerFloorMetres, line);
        Assert.IsTrue(float.Parse(Fact(line, "shift")) <= MarshalTrialRules.OffsetToleranceMetres, line);
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenTheCompaniesAreLaidOutSideBySide_FailsOnTheShift()
    {
        _world.ArrayKeepsTheOffsets = false;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets");
        Assert.IsTrue(float.Parse(Fact(Line("array_keeps_offsets"), "shift")) > MarshalTrialRules.OffsetToleranceMetres);
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenTheLayoutIsComputedAndReportedButNeverApplied_Fails()
    {
        // The takeover counts its commit before the controller gives the order, so a build that computes the layout and drops it still counts an array.
        _world.ArrayLayoutIsNeverApplied = true;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets", "arrays=1");
    }

    [TestMethod]
    public void QueueTwoMoves_WithTheEnginesSlowTimeOnAnOpenPanel_StillReachesBothPoints_BecauseTheStepShutsThePanelBeforeTheWalk()
    {
        // The run's measured pace was 3.5 m/s: two legs of 25 m take 14 s at full speed and 57 s at a quarter of it, over the step's 45 s.
        _world.SlowDownOnOrder = true;
        _world.Companies[0].Speed = 3.5f;

        Run("queue_two_moves");

        AssertPasses("queue_two_moves");
        Assert.IsTrue(float.Parse(Fact(Line("queue_two_moves"), "second_at")) < 25f, Line("queue_two_moves"));
    }

    [TestMethod]
    public void ArrayKeepsOffsets_Passes_OnlyWithTheOrderPointsMovedByTheDraggedShift_OfAtLeastFifteenMetres()
    {
        Run("array_keeps_offsets");

        var line = Line("array_keeps_offsets");
        AssertPasses("array_keeps_offsets");
        Assert.IsTrue(float.Parse(Fact(line, "planned")) >= MarshalTrialRules.ShiftFloorMetres, line);
        Assert.IsTrue(float.Parse(Fact(line, "miss")) <= MarshalTrialRules.OffsetToleranceMetres, line);
        Assert.AreEqual(-MarshalTrialRules.ShiftMetres, _world.Companies[0].Y, 0.5f, "the company really stood where the shift put it");
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenTheDragPlannedIsUnderFifteenMetres_Fails_EvenIfEveryCompanyFollowedIt()
    {
        _world.FrontDragShiftScale = 0.5f;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets", "planned=10.0", "miss=0.0");
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenTheOrderPointsMoveByHalfTheShift_Fails_OnTheMiss()
    {
        _world.ArrayShiftScale = 0.5f;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets");
        Assert.IsTrue(float.Parse(Fact(Line("array_keeps_offsets"), "miss")) > MarshalTrialRules.OffsetToleranceMetres);
        Assert.IsTrue(float.Parse(Fact(Line("array_keeps_offsets"), "shift")) <= MarshalTrialRules.OffsetToleranceMetres, "the layout held: only the translation is wrong");
    }

    [TestMethod]
    public void ArrayKeepsOffsets_AnEarlierStepsWalkStillUnderWay_IsWaitedOutBeforeAnyOffsetIsRead()
    {
        // The first company is still walking to a place 60 m on. The solver keeps the destination of a company on its way (6.2), so an offset read
        // before the walk ends is read against a place the array does not keep.
        _world.Companies[0].TargetX = 60f;
        _world.Companies[0].TargetY = 0f;
        _world.Companies[0].Order = "Move";

        Run("array_keeps_offsets");

        AssertPasses("array_keeps_offsets");
        Assert.IsTrue(float.Parse(Fact(Line("array_keeps_offsets"), "shift")) <= MarshalTrialRules.OffsetToleranceMetres);
    }

    [TestMethod]
    public void ArrayKeepsOffsets_TheStaggerIsMadeByTheStep_WhereverTheCompaniesStoodBefore()
    {
        foreach (var secondAt in new[] { -14f, 40f, 90f })
        {
            Setup();
            _world.Companies[1].X = secondAt;

            Run("array_keeps_offsets");

            var line = Line("array_keeps_offsets");
            AssertPasses("array_keeps_offsets");
            Assert.AreEqual(MarshalTrialRules.LegMetres, float.Parse(Fact(line, "stagger")), 0.2f, "second company at " + secondAt + ": " + line);
        }
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenACompanyNeverStands_FailsNamingThatBeforeAnyOrder()
    {
        _world.Companies[0].Speed = 0f;
        _world.Companies[0].TargetX = 60f;
        _world.Companies[0].TargetY = 0f;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets", "reason=not-settled", "first=0", "second=1");
        CollectionAssert.DoesNotContain(_world.Writes, "marshal-drag");
    }

    [TestMethod]
    public void ArrayKeepsOffsets_NamesTheShiftOfTheOrderPointsBesideTheShiftOfTheStandingPlaces()
    {
        Run("array_keeps_offsets");

        Assert.IsTrue(float.Parse(Fact(Line("array_keeps_offsets"), "ordered")) <= MarshalTrialRules.OffsetToleranceMetres, Line("array_keeps_offsets"));
    }

    [TestMethod]
    public void ArriveTogether_WaitsForCompaniesOnTheirWayToStand_BeforeItPlansTheDrag()
    {
        _world.Companies[0].TargetX = 60f;
        _world.Companies[0].TargetY = 0f;

        Run("arrive_together");

        AssertPasses("arrive_together");
        Assert.IsFalse(_world.PlannedWhileWalking, "the plan counts the walks from the order points, and the march measures them from where the companies stand");
    }

    [TestMethod]
    public void ArrayKeepsOffsets_WhenTheTakeoverReportsNoCommit_Fails_EvenIfTheOffsetsHeld()
    {
        _world.ArrayIsReported = false;

        Run("array_keeps_offsets");

        AssertFails("array_keeps_offsets", "arrays=0");
    }

    [TestMethod]
    public void ArriveTogether_Passes_WhenTheMarchEndsWithAPass()
    {
        Run("arrive_together");

        AssertPasses("arrive_together");
        StringAssert.Contains(Line("arrive_together"), "conclusion=pass");
    }

    [TestMethod]
    public void ArriveTogether_ANoTestSkip_FailsTheStep_SoAWalkThatProvesNothingIsNotAPass()
    {
        _world.MarchEnds = "no-test";

        Run("arrive_together");

        AssertFails("arrive_together", "conclusion=no-test");
    }

    [TestMethod]
    public void ArriveTogether_AFailedSpread_FailsTheStep()
    {
        _world.MarchEnds = "fail";

        Run("arrive_together");

        AssertFails("arrive_together", "conclusion=fail");
    }

    [TestMethod]
    public void ArriveTogether_AMarchThatNeverEnds_FailsWithNoConclusion()
    {
        _world.MarchNeverEnds = true;

        Run("arrive_together");

        AssertFails("arrive_together", "conclusion=none");
    }

    [TestMethod]
    public void ArriveTogether_WithNoPlanThatFitsTheContract_FailsBeforeTheDrag_AndNamesHowFarApartTheCompaniesStand()
    {
        _world.PlanExists = false;

        Run("arrive_together");

        AssertFails("arrive_together", "reason=no-plan", "apart=");
        CollectionAssert.DoesNotContain(_world.Writes, "marshal-drag");
    }

    [TestMethod]
    public void ArriveTogether_TwoCompaniesAsCloseAsAFreshBattleLeavesThem_AreStoodInAColumnFirst_AndThenGetAPlan()
    {
        Run("arrive_together");

        AssertPasses("arrive_together");
        var first = _world.Companies[0];
        var second = _world.Companies[1];
        Assert.AreEqual(MarshalTrialRules.ColumnBehindMetres, first.X - second.X, MarshalTuning.ArrivalSlack);
        Assert.IsTrue(_world.Writes.IndexOf("marshal-move") >= 0 && _world.Writes.IndexOf("marshal-move") < _world.Writes.IndexOf("marshal-drag"), "the column is made before the drag");
    }

    [TestMethod]
    public void ArriveTogether_StagesTheColumnWhereverTheSecondCompanyStoodBefore()
    {
        foreach (var secondAt in new[] { -14f, 5f, 40f, 90f })
        {
            Setup();
            _world.Companies[1].X = secondAt;

            Run("arrive_together");

            AssertPasses("arrive_together");
        }
    }

    [TestMethod]
    public void ArriveTogether_WhenTheSecondCompanyNeverReachesItsPlaceInTheColumn_FailsNotSettledBeforeTheDrag()
    {
        _world.Companies[1].Speed = 0f;

        Run("arrive_together");

        AssertFails("arrive_together", "reason=not-settled");
        CollectionAssert.DoesNotContain(_world.Writes, "marshal-drag");
    }

    [TestMethod]
    public void ArriveTogether_CompaniesNotInLine_Fail()
    {
        _world.Companies[1].Line = false;

        Run("arrive_together");

        AssertFails("arrive_together", "reason=not-line");
    }

    [TestMethod]
    public void ArriveTogether_ANewConclusionIsRequired_AnOldPassDoesNotCount()
    {
        _world.MarchNeverEnds = true;
        Assert.AreEqual("none", _world.MarshalMarchConclusion);

        Run("arrive_together");

        AssertFails("arrive_together");
    }

    // ---- M3 ----

    [TestMethod]
    public void VolleyHolds_Passes_WhenTheHoldCheckPassesWithFiveOrMoreHolding()
    {
        Run("volley_holds");

        var line = Line("volley_holds");
        AssertPasses("volley_holds");
        Assert.IsTrue(int.Parse(Fact(line, "held")) >= MarshalTuning.VolleyMinHolding, line);
    }

    [TestMethod]
    public void VolleyHolds_NamesTheMostHoldingItSaw_BesideTheWindowsSmallestCountItCallsHeld()
    {
        Run("volley_holds");

        var line = Line("volley_holds");
        Assert.IsTrue(int.Parse(Fact(line, "most_holding")) >= int.Parse(Fact(line, "held")), line);
    }

    [TestMethod]
    public void VolleyHolds_WhenTheArchersLooseWhileHolding_Fails()
    {
        _world.HoldVerdictPasses = false;

        Run("volley_holds");

        AssertFails("volley_holds");
        StringAssert.Contains(Line("volley_holds"), "shots=4");
    }

    [TestMethod]
    public void VolleyHolds_WhenNobodyHolds_FailsWithTheMostHoldingItSaw()
    {
        _world.ArchersHold = false;

        Run("volley_holds");

        AssertFails("volley_holds", "reason=no-window", "most_holding=0");
    }

    [TestMethod]
    public void VolleyHolds_WhenTheKeyChangesNothing_FailsNamingTheMode()
    {
        _world.FireKeyWorks = false;

        Run("volley_holds");

        AssertFails("volley_holds", "reason=mode-not-set");
    }

    [TestMethod]
    public void VolleyHolds_WithNoArchers_Fails()
    {
        _world.ArchersExist = false;

        Run("volley_holds");

        AssertFails("volley_holds", "reason=no-archers");
    }

    [TestMethod]
    public void VolleyHolds_WalksTheArchersTowardTheNearestEnemyFirst_ThenCountsTheirFreeFire_ThenHolds()
    {
        _world.EnemyStartMetres = 300f;

        Run("volley_holds");

        var line = Line("volley_holds");
        AssertPasses("volley_holds");
        var moved = _world.Writes.IndexOf("marshal-move");
        Assert.IsTrue(moved >= 0, "the archers were ordered forward");
        Assert.IsTrue(_world.Companies[0].X > 50f, "the archers walked about the half of the gap the enemy left them: " + _world.Companies[0].X);
        Assert.IsFalse(float.IsNaN(float.Parse(Fact(line, "gap"))), line);
    }

    [TestMethod]
    public void VolleyHolds_CountsTheShotsOfTheFreeCompanyForTenMissionSeconds_AndNamesThemAndTheirShooters()
    {
        _world.FreeShotsPerSecond = 3f;
        _world.FreeShooters = 9;

        Run("volley_holds");

        var line = Line("volley_holds");
        Assert.IsTrue(int.Parse(Fact(line, "free_shots")) is >= 27 and <= 36, line);
        Assert.AreEqual("9", Fact(line, "free_shooters"));
    }

    [TestMethod]
    public void VolleyHolds_AControlThatSawNoShot_StillWritesZero_AndTheHoldIsJudgedOnItsOwn()
    {
        _world.FreeShotsPerSecond = 0f;

        Run("volley_holds");

        AssertPasses("volley_holds");
        Assert.AreEqual("0", Fact(Line("volley_holds"), "free_shots"));
        Assert.AreEqual("0", Fact(Line("volley_holds"), "free_shooters"));
    }

    [TestMethod]
    public void VolleyHolds_TheControlRunsBeforeTheModeChanges_SoItCountsFreeFireAndNotTheHold()
    {
        Run("volley_holds");

        Assert.AreEqual(FireMode.OnWord, _world.MarshalFireMode(0));
        Assert.IsTrue(int.Parse(Fact(Line("volley_holds"), "free_shots")) > 0, "the shots were counted while the mode was free");
    }

    [TestMethod]
    public void VolleyHolds_NamesThePeakIdleAndThePeakAside_BesideTheHolders()
    {
        _world.IdleCount = 31;
        _world.AsideCount = 4;

        Run("volley_holds");

        var line = Line("volley_holds");
        Assert.AreEqual("31", Fact(line, "peak_idle"));
        Assert.AreEqual("4", Fact(line, "peak_aside"));
    }

    [TestMethod]
    public void VolleyHolds_NamesTheControlAndTheCensusAlsoWhenNoWindowCame()
    {
        _world.ArchersHold = false;

        Run("volley_holds");

        AssertFails("volley_holds", "reason=no-window", "free_shots=", "free_shooters=", "peak_idle=", "peak_aside=");
    }

    [TestMethod]
    public void VolleyHolds_WithNoEnemy_FailsBeforeAnyOrder()
    {
        _world.EnemyUnits = 0;

        Run("volley_holds");

        AssertFails("volley_holds", "reason=no-enemy");
        CollectionAssert.DoesNotContain(_world.Writes, "marshal-move");
    }

    [TestMethod]
    public void VolleyHolds_ACompanyAlreadyOnTheWord_FailsBeforeTheControl_BecauseItIsNotAFreeControl()
    {
        _world.MarshalPressFire();

        Run("volley_holds");

        AssertFails("volley_holds", "reason=not-free", "mode=OnWord");
    }

    [TestMethod]
    public void VolleyLooses_Passes_WhenTheReleaseCheckPasses_AndTheModeGoesBackToFree()
    {
        Run("volley_holds", "volley_looses");

        AssertPasses("volley_looses");
        Assert.AreEqual(FireMode.Free, _world.MarshalFireMode(0));
    }

    [TestMethod]
    public void VolleyLooses_WhenTheWordLoosesNoOne_Fails()
    {
        _world.WordLoosesThem = false;

        Run("volley_holds", "volley_looses");

        AssertPasses("volley_holds");
        AssertFails("volley_looses");
    }

    // ---- M4 ----

    [TestMethod]
    public void MarkKeepsOrder_Passes_WithTheOrderStillStopAndTheMarkRead()
    {
        Run("mark_keeps_order");

        var line = Line("mark_keeps_order");
        AssertPasses("mark_keeps_order");
        Assert.AreEqual("Stop", Fact(line, "order"));
        StringAssert.Contains(line, "onTarget=6 of=8");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheChargeIsNotTurnedIntoAMark_FailsOnTheOrder()
    {
        _world.MarkTakes = false;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "marked=0", "order=Charge");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheKeyNeverArms_FailsNamingTheKey()
    {
        _world.MarkKeyArms = false;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=mark-armed-never");
    }

    [TestMethod]
    public void MarkKeepsOrder_TheStopShutsThePanel_SoTheStepOpensItAgainBeforeItHoldsTheKey()
    {
        Run("mark_keeps_order");

        AssertPasses("mark_keeps_order");
        Assert.AreEqual(2, _world.Writes.Count(w => w == "open-panel"), string.Join(",", _world.Writes));
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheKeyArmsOverAPanelTheEngineShut_Fails()
    {
        _world.PanelShutsWhenTheKeyIsHeld = true;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=not-armed-at-order", "panel=0", "armed=1");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenNoReadFollows_Fails_SoTheFactsAlwaysCarryOnTarget()
    {
        _world.MarkIsRead = false;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "onTarget=none");
    }

    [TestMethod]
    public void MarkKeepsOrder_WithNoEnemy_Fails()
    {
        _world.EnemyUnits = 0;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=no-enemy");
    }

    [TestMethod]
    public void MarkKeepsOrder_ChoosesTheArchers_AndPassesOnTheRangedOnStopVerdictTheWatchWrote()
    {
        Run("mark_keeps_order");

        var line = Line("mark_keeps_order");
        AssertPasses("mark_keeps_order");
        StringAssert.Contains(line, "context=RangedOnStop");
        StringAssert.Contains(line, "effect=pass");
        StringAssert.Contains(line, "onTarget=6 of=8");
        Assert.IsTrue(float.Parse(Fact(line, "distance")) <= MarshalTrialRules.ArcherReachMetres, line);
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheWatchWritesAFail_FailsTheStep_WithTheSampleItJudged()
    {
        _world.MarkOnTarget = 1;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "effect=fail", "onTarget=1 of=8", "marked=1", "order=Stop");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenFewerThanThreeSampledSoldiersHaveATarget_FailsWithItsOwnReason_NeverAPass()
    {
        _world.MarkSampled = 2;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=too-few-targets", "of=2", "effect=none");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheWatchNeverJudges_FailsWithNoVerdict()
    {
        _world.MarkIsJudged = false;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=no-verdict", "effect=none");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheCompanyReadsAsAnotherContext_FailsWithTheContextItRead()
    {
        _world.MarkContextOverride = MarkContext.Other;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=wrong-context", "context=Other");
    }

    [TestMethod]
    public void MarkKeepsOrder_WaitsForTheEnemyToComeIntoReach_BeforeItMarks()
    {
        _world.EnemyStartMetres = 400f;
        _world.EnemyClosingSpeed = 5f;

        Run("mark_keeps_order");

        AssertPasses("mark_keeps_order");
        Assert.IsTrue(float.Parse(Fact(Line("mark_keeps_order"), "distance")) <= MarshalTrialRules.ArcherReachMetres);
        Assert.IsTrue(_world.Time > 60f, "the enemy was 400 m away and closed at 5 m/s: the mark waited for it. Time " + _world.Time);
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheEnemyNeverComesIntoReach_FailsNamingTheDistance_AndMarksNothing()
    {
        _world.EnemyStartMetres = 1000f;
        _world.EnemyClosingSpeed = 0f;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=enemy-out-of-reach", "distance=1000", "reach=70");
        CollectionAssert.DoesNotContain(_world.Writes, "marshal-charge");
    }

    [TestMethod]
    public void MarkKeepsOrder_WithNoRangedCompany_Fails()
    {
        _world.Companies[0].Ranged = false;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "reason=no-archers");
    }

    [TestMethod]
    public void MarkKeepsOrder_ShutsThePanelBeforeItWaitsForTheVerdict_SoTheEnginesSlowTimeCannotOutlastTheWait()
    {
        // The watch judges after 4 mission seconds, which an open panel stretches to 16 real ones, over the step's 15.
        _world.SlowDownOnOrder = true;

        Run("mark_keeps_order");

        AssertPasses("mark_keeps_order");
        Assert.IsFalse(_world.PanelOpen);
    }

    [TestMethod]
    public void OutlineClears_Passes_WithSomethingPaintedOpenAndNothingLeftClosed()
    {
        Run("outline_clears");

        var line = Line("outline_clears");
        AssertPasses("outline_clears");
        Assert.AreEqual("0", Fact(line, "left"));
        Assert.IsTrue(int.Parse(Fact(line, "painted")) > 0);
    }

    [TestMethod]
    public void OutlineClears_WhenTheClosingPassDoesNotClear_Fails()
    {
        _world.OutlinesClearWhenTheStepCloses = false;

        Run("outline_clears");

        AssertFails("outline_clears");
        Assert.AreNotEqual("0", Fact(Line("outline_clears"), "left"));
    }

    [TestMethod]
    public void OutlineClears_WhenNothingWasEverPainted_Fails_SoAnEmptyDictionaryIsNotAClear()
    {
        _world.OutlinesPaint = false;

        Run("outline_clears");

        AssertFails("outline_clears", "painted=0");
    }

    // ---- Keys never outlive their step ----

    [TestMethod]
    public void AStepThatFailsInsideAWait_LeavesNoKeyHeld_SoTheNextStepIsNotCapturedByIt()
    {
        _world.QueueKeyArms = false;
        Run("queue_two_moves");
        AssertFails("queue_two_moves", "reason=queue-armed-never");
        Assert.IsFalse(_world.AnyKeyHeld, "key 540 stays held after the failure and arms the next step's first Move");

        Setup();
        _world.MarkKeyArms = false;
        Run("mark_keeps_order");
        Assert.IsFalse(_world.AnyKeyHeld);

        Setup();
        _world.WidthsKeyArms = false;
        Run("arrive_together");
        AssertFails("arrive_together", "widths-armed-never");
        Assert.IsFalse(_world.AnyKeyHeld);
    }

    [TestMethod]
    public void TheQueueSteps_AfterAFailedOne_StillGiveTheirFirstMoveDirectly()
    {
        // The first run's accident: queue_two_moves failed holding key 540, and direct_order_clears then passed because the held key took its first Move.
        _world.QueueKeyArms = false;
        Run("queue_two_moves");
        _world.QueueKeyArms = true;
        _finished.Clear();
        _log = new ListLogger();

        Run("direct_order_clears");

        AssertPasses("direct_order_clears");
        Assert.AreEqual("1", Fact(Line("direct_order_clears"), "captured"));
    }

    // ---- The session ----

    [TestMethod]
    public void EveryStep_WhenTheSessionNeverGoesLive_TimesOutOnTheBattleWait()
    {
        _world.MarshalSessionLive = false;

        Run("outline_clears");

        AssertFails("outline_clears", "reason=timeout waiting=marshal-battle");
    }

    [TestMethod]
    public void EveryStep_WhenTheSessionFaulted_FailsNamingIt()
    {
        _world.MarshalSessionFaulted = true;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "reason=session-faulted");
    }

    [TestMethod]
    public void QueueTwoMoves_WhenAStepStaysQueuedAfterBothArrivals_FailsOnTheQueueLeft()
    {
        _world.PhantomQueuedStep = true;

        Run("queue_two_moves");

        AssertFails("queue_two_moves", "left=1");
        Assert.AreEqual("0", Fact(Line("queue_two_moves"), "by_patience"));
    }

    [TestMethod]
    public void VolleyHolds_AVerdictThatPassedWithFewerThanFiveHolding_Fails()
    {
        _world.VerdictHolding = 3;

        Run("volley_holds");

        AssertFails("volley_holds", "held=3");
    }

    [TestMethod]
    public void VolleyLooses_AReleaseVerdictFromBeforeTheWord_IsNotThisWordsVerdict()
    {
        _world.StaleReleaseVerdict = true;
        _world.WordIsLost = true;

        Run("volley_holds", "volley_looses");

        AssertFails("volley_looses", "judged=0");
    }

    [TestMethod]
    public void MarkKeepsOrder_WhenTheMarkAlsoMovesTheCompany_FailsOnTheOrder()
    {
        _world.MarkAlsoCharges = true;

        Run("mark_keeps_order");

        AssertFails("mark_keeps_order", "marked=1", "order=Charge");
    }
}
