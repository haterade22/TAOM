using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Tests.Features.LongDefeat;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>What the story mission logic asks each tick: has the run a verdict yet, and has the end cue lasted long enough to leave.</summary>
[TestClass]
public class StoryEndWatchTests
{
    private static RunResult Ended(RunEndReason reason) => new(reason, 1, 1, 7);

    private static (StoryEndWatch Watch, RecordingOutcomeLog Outcomes) Watch(StorySide side = StorySide.Defending)
    {
        var outcomes = new RecordingOutcomeLog();
        return (new StoryEndWatch(side, Substitute.For<IModLogger>(), outcomes, "s1"), outcomes);
    }

    [TestMethod]
    public void Step_BeforeTheRunHasAResult_AsksNothingAndWritesNothing()
    {
        var (watch, outcomes) = Watch();

        Assert.IsFalse(watch.Step(1f, null, false));
        Assert.AreEqual(0, outcomes.Lines.Count);
    }

    [TestMethod]
    public void Step_AWin_WritesOnePassLineWithTheVerdict()
    {
        var (watch, outcomes) = Watch();

        watch.Step(0.1f, Ended(RunEndReason.TargetReached), false);
        watch.Step(0.1f, Ended(RunEndReason.TargetReached), false);

        Assert.AreEqual(1, outcomes.Lines.Count, string.Join("\n", outcomes.Lines));
        Assert.AreEqual($"pass {StoryEndWatch.EndedId} s1 verdict=Won side=Defending reason=TargetReached waves=1", outcomes.Lines[0]);
    }

    [TestMethod]
    public void Step_ALoss_WritesAPassLineNamingTheLoss()
    {
        var (watch, outcomes) = Watch();

        watch.Step(0.1f, Ended(RunEndReason.DefendersDead), false);

        Assert.AreEqual($"pass {StoryEndWatch.EndedId} s1 verdict=Lost side=Defending reason=DefendersDead waves=1", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Step_TheAttackingSide_ReadsTheKeepFallingAsAWin()
    {
        var (watch, outcomes) = Watch(StorySide.Attacking);

        watch.Step(0.1f, Ended(RunEndReason.KeepLost), false);

        StringAssert.Contains(outcomes.Lines.Single(), "verdict=Won side=Attacking reason=KeepLost");
    }

    [TestMethod]
    public void Step_AStalemate_WritesASkipLine_AndStillEndsTheMission()
    {
        var (watch, outcomes) = Watch();

        Assert.IsFalse(watch.Step(0.1f, Ended(RunEndReason.Stalemate), false));
        Assert.IsTrue(watch.Step(StoryEndWatch.EndDelaySeconds, Ended(RunEndReason.Stalemate), false));

        Assert.AreEqual($"skip {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=Stalemate waves=1", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Step_AFault_WritesAFailLine_AndNeverAsksToEnd_BecauseTheHostIsAlreadyLeaving()
    {
        var (watch, outcomes) = Watch();

        Assert.IsFalse(watch.Step(0.1f, Ended(RunEndReason.Abandoned), true));
        Assert.IsFalse(watch.Step(60f, Ended(RunEndReason.Abandoned), true));

        Assert.AreEqual($"fail {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=fault", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Step_AFaultWithNoResult_WritesTheFailLine()
    {
        var (watch, outcomes) = Watch();

        Assert.IsFalse(watch.Step(0.1f, null, true));

        StringAssert.Contains(outcomes.Lines.Single(), "verdict=Undecided");
        StringAssert.StartsWith(outcomes.Lines.Single(), "fail ");
    }

    [TestMethod]
    public void Step_LeavesFiveSecondsAfterTheResultAppeared()
    {
        var (watch, _) = Watch();

        Assert.IsFalse(watch.Step(2f, Ended(RunEndReason.TargetReached), false));
        Assert.IsFalse(watch.Step(2.9f, Ended(RunEndReason.TargetReached), false));
        Assert.IsTrue(watch.Step(0.1f, Ended(RunEndReason.TargetReached), false));
    }

    [TestMethod]
    public void Step_CountsTheDelayOnlyFromTheResult_NotFromTheMissionStart()
    {
        var (watch, _) = Watch();

        Assert.IsFalse(watch.Step(100f, null, false));
        Assert.IsFalse(watch.Step(1f, Ended(RunEndReason.TargetReached), false));
    }

    [TestMethod]
    public void Step_ABadDelta_IsNotCounted()
    {
        var (watch, _) = Watch();
        var result = Ended(RunEndReason.TargetReached);

        Assert.IsFalse(watch.Step(float.NaN, result, false));
        Assert.IsFalse(watch.Step(-10f, result, false));
        Assert.IsFalse(watch.Step(0f, result, false));
        Assert.IsFalse(watch.Step(float.PositiveInfinity, result, false));
        Assert.IsFalse(watch.Step(4.9f, result, false));
        Assert.IsTrue(watch.Step(0.2f, result, false));
    }

    [TestMethod]
    public void Step_AskingToEnd_HappensOnce()
    {
        var (watch, outcomes) = Watch();
        var result = Ended(RunEndReason.TargetReached);

        Assert.IsTrue(watch.Step(5f, result, false));
        Assert.IsFalse(watch.Step(5f, result, false));
        Assert.AreEqual(1, outcomes.Lines.Count);
    }

    [TestMethod]
    public void Left_BeforeAnyVerdict_WritesOneSkipLine()
    {
        var (watch, outcomes) = Watch();

        watch.Left(null, false);
        watch.Left(null, false);

        Assert.AreEqual($"skip {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=left", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Left_AfterTheVerdictWasWritten_AddsNothing()
    {
        var (watch, outcomes) = Watch();
        watch.Step(0.1f, Ended(RunEndReason.TargetReached), false);

        watch.Left(null, false);

        Assert.AreEqual(1, outcomes.Lines.Count);
    }

    [TestMethod]
    public void Left_ARetreatThatLeftAnAbandonedResult_WritesThePlansLoss()
    {
        var (watch, outcomes) = Watch();

        watch.Left(Ended(RunEndReason.Abandoned), false);

        Assert.AreEqual($"pass {StoryEndWatch.EndedId} s1 verdict=Lost side=Defending reason=Abandoned waves=1", outcomes.Lines.Single());
        Assert.AreEqual(StoryOutcome.Lost, watch.Verdict);
    }

    [TestMethod]
    public void Left_AResultTheStepNeverSaw_WritesItsVerdict()
    {
        var (watch, outcomes) = Watch();

        watch.Left(Ended(RunEndReason.TargetReached), false);

        StringAssert.Contains(outcomes.Lines.Single(), "verdict=Won side=Defending reason=TargetReached");
    }

    [TestMethod]
    public void Left_AStalemateResult_WritesASkipWithTheReason()
    {
        var (watch, outcomes) = Watch();

        watch.Left(Ended(RunEndReason.Stalemate), false);

        Assert.AreEqual($"skip {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=Stalemate waves=1", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Left_AFault_WritesTheFailLine_EvenWithAnAbandonedResult()
    {
        var (watch, outcomes) = Watch();

        watch.Left(Ended(RunEndReason.Abandoned), true);
        watch.Left(Ended(RunEndReason.Abandoned), true);

        Assert.AreEqual($"fail {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=fault", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Left_AFaultWithNoResult_WritesTheFailLine()
    {
        var (watch, outcomes) = Watch();

        watch.Left(null, true);

        StringAssert.StartsWith(outcomes.Lines.Single(), "fail ");
    }

    [TestMethod]
    public void Left_AfterAVerdictWasWritten_IgnoresALaterFaultOrResult()
    {
        var (watch, outcomes) = Watch();
        watch.Step(0.1f, Ended(RunEndReason.TargetReached), false);

        watch.Left(Ended(RunEndReason.Abandoned), true);

        Assert.AreEqual(1, outcomes.Lines.Count);
        StringAssert.StartsWith(outcomes.Lines[0], "pass ");
    }

    [TestMethod]
    public void Left_ASessionTheHostClearedAfterAFault_StillReadsTheFault()
    {
        // The engine's order in a hosted run: the host records the fault, then shuts down (Abandon and Clear), then the mission ends and this logic is told.
        var session = new LongDefeatSession();
        session.Fill(NewRun());
        session.RecordFault("Tick");
        session.Clear();
        var (watch, outcomes) = Watch();

        watch.Left(session.Result, session.Fault != null);

        Assert.AreEqual($"fail {StoryEndWatch.EndedId} s1 verdict=Undecided side=Defending reason=fault", outcomes.Lines.Single());
    }

    [TestMethod]
    public void Left_ASessionTheHostAbandonedAndCleared_WritesTheLoss()
    {
        // A retreat: the host's Shutdown abandons the run and clears the session before the mission's own exit tells this logic.
        var run = NewRun();
        var session = new LongDefeatSession();
        session.Fill(run);
        run.Abandon();
        session.Clear();
        var (watch, outcomes) = Watch();

        watch.Left(session.Result, session.Fault != null);

        StringAssert.Contains(outcomes.Lines.Single(), "verdict=Lost side=Defending reason=Abandoned");
        StringAssert.StartsWith(outcomes.Lines.Single(), "pass ");
    }

    private static SurvivalRun NewRun()
    {
        var waves = new WavePlan(new[] { new WaveSpec(10, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var plan = new RunPlan("helms", new[] { new GarrisonRow("rohan", FormationSlot.Infantry, 5) }, waves, new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    [TestMethod]
    public void Step_AfterLeft_WritesNothingAndAsksNothing()
    {
        var (watch, outcomes) = Watch();
        watch.Left(null, false);

        Assert.IsFalse(watch.Step(10f, Ended(RunEndReason.TargetReached), false));
        Assert.AreEqual(1, outcomes.Lines.Count);
    }
}
