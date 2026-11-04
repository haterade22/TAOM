using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatSessionTests
{
    [TestMethod]
    public void LostInClearedWaves_OutlivesClear_AndANewRunStartsAtZero()
    {
        var session = new LongDefeatSession();
        Assert.AreEqual(0, session.LostInClearedWaves);

        var run = NewRun();
        session.Fill(run);
        run.Stats.DefenderLost();
        run.Stats.CloseWave(1, 10f, 0);
        Assert.AreEqual(1, session.LostInClearedWaves);

        session.Clear();
        Assert.AreEqual(1, session.LostInClearedWaves);

        session.Fill(NewRun());
        Assert.AreEqual(0, session.LostInClearedWaves);
    }

    private static SurvivalRun NewRun()
    {
        var waves = new WavePlan(
            new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) },
            new WaveScaling(10, 300));
        var shop = new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) });
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default,
            RunOptions.Default);
        var run = new SurvivalRun(plan);
        run.Begin();
        return run;
    }

    [TestMethod]
    public void Speed_StartsAtOne_StepsThroughTheFourLevels_AndIgnoresAnythingElse()
    {
        var session = new LongDefeatSession();
        Assert.AreEqual(1, session.Speed);
        Assert.AreEqual(1, session.MaxSpeed);

        var seen = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            session.CycleSpeed();
            seen.Add(session.Speed);
        }

        CollectionAssert.AreEqual(new[] { 2, 4, 10, 1, 2 }, seen);
        session.SetSpeed(3);
        Assert.AreEqual(2, session.Speed, "3 is no level");
        session.SetSpeed(10);
        Assert.AreEqual(10, session.Speed);
    }

    [TestMethod]
    public void Speed_ReadsOneOnceTheRunHasAResult_AndMaxSpeedKeepsItsValue()
    {
        var run = NewRun();
        var session = new LongDefeatSession();
        session.Fill(run);
        session.SetSpeed(10);
        Assert.AreEqual(10, session.Speed);

        run.Abandon();

        Assert.IsNotNull(session.Result);
        Assert.AreEqual(1, session.Speed, "a fight with no rules must not run at 10x behind the end panel");
        Assert.AreEqual(10, session.MaxSpeed);

        session.Clear();
        Assert.AreEqual(1, session.Speed);
        Assert.AreEqual(10, session.MaxSpeed);

        session.Fill(NewRun());
        Assert.AreEqual(1, session.Speed);
        Assert.AreEqual(1, session.MaxSpeed);
    }

    [TestMethod]
    public void SpeedLevels_AreExactlyTheOnesTheFordsRuleAccepts_InStepOrder()
    {
        var session = new LongDefeatSession();
        var stepped = new List<int> { session.Speed };
        for (var i = 1; i < LongDefeatSession.SpeedLevels.Length; i++)
        {
            session.CycleSpeed();
            stepped.Add(session.Speed);
        }

        CollectionAssert.AreEqual(LongDefeatSession.SpeedLevels, stepped);
        session.CycleSpeed();
        Assert.AreEqual(1, session.Speed);
        Assert.IsFalse(TAOM.Features.FordsOfWar.Domain.FordsGameSpeed.IsLevel(3));
    }

    [TestMethod]
    public void MaxSpeed_IsTheHighestSetThisRun_SurvivesClear_AndANewRunResetsBoth()
    {
        var session = new LongDefeatSession();
        session.Fill(NewRun());
        session.SetSpeed(4);
        session.SetSpeed(2);
        Assert.AreEqual(2, session.Speed);
        Assert.AreEqual(4, session.MaxSpeed);

        session.Clear();
        Assert.AreEqual(2, session.Speed, "the HUD still reads it after the mission has gone");
        Assert.AreEqual(4, session.MaxSpeed);

        session.Fill(NewRun());
        Assert.AreEqual(1, session.Speed);
        Assert.AreEqual(1, session.MaxSpeed);
    }

    [TestMethod]
    public void Fault_SurvivesClear_AndResetsAtTheNextFill()
    {
        var session = new LongDefeatSession();
        Assert.IsNull(session.Fault);
        session.Fill(NewRun());

        session.RecordFault("OnMissionTick");
        session.Clear();
        Assert.AreEqual("OnMissionTick", session.Fault, "the unattended driver reads it after the mission has gone");

        session.Fill(NewRun());
        Assert.IsNull(session.Fault);
    }

    [TestMethod]
    public void Inactive_RefusesEveryCommand_AndShowsNothing()
    {
        var session = new LongDefeatSession();

        Assert.IsFalse(session.IsActive);
        Assert.IsNull(session.GetView());
        Assert.IsNull(session.Result);
        Assert.AreEqual(BuyResult.RunOver, session.TryBuy("militia"));
        Assert.AreEqual(StartResult.RunOver, session.TryStartWave());
    }

    [TestMethod]
    public void Filled_ForwardsTheViewAndTheCommands()
    {
        var session = new LongDefeatSession();
        session.Fill(NewRun());

        Assert.IsTrue(session.IsActive);
        Assert.AreEqual(600, session.GetView()!.Gold);
        Assert.AreEqual(BuyResult.Ok, session.TryBuy("militia"));
        Assert.AreEqual(420, session.GetView()!.Gold);
        Assert.AreEqual(BuyResult.UnknownOffer, session.TryBuy("nothing"));
        Assert.AreEqual(StartResult.Ok, session.TryStartWave());
    }

    [TestMethod]
    public void Result_SurvivesClear_AndResetsAtTheNextFill()
    {
        var session = new LongDefeatSession();
        var run = NewRun();
        session.Fill(run);
        Assert.IsNull(session.Result);

        run.Abandon();
        Assert.AreEqual(RunEndReason.Abandoned, session.Result!.Reason, "visible while the run is still held");

        session.Clear();
        Assert.IsFalse(session.IsActive);
        Assert.IsNull(session.GetView());
        Assert.AreEqual(RunEndReason.Abandoned, session.Result!.Reason, "the client reads it after the mission has gone");

        session.Fill(NewRun());
        Assert.IsNull(session.Result, "a new run starts with no result");
    }

    [TestMethod]
    public void Clear_WithNoResult_LeavesNone_AndTwiceIsSafe()
    {
        var session = new LongDefeatSession();
        session.Fill(NewRun());

        session.Clear();
        session.Clear();

        Assert.IsNull(session.Result);
    }

    [TestMethod]
    public void LiveSecondsAndReportPath_SurviveClear_AndResetAtTheNextFill()
    {
        var session = new LongDefeatSession();
        session.Fill(NewRun());
        session.AddLiveSeconds(12.5f);
        session.AddLiveSeconds(7.5f);
        session.RecordReport(@"E:\logs\report.json");

        session.Clear();
        Assert.AreEqual(20f, session.LiveSeconds, 0.0001f, "the unattended driver reads them after the mission has gone");
        Assert.AreEqual(@"E:\logs\report.json", session.ReportPath);

        session.Fill(NewRun());
        Assert.AreEqual(0f, session.LiveSeconds);
        Assert.IsNull(session.ReportPath);
    }

    [TestMethod]
    public void AddLiveSeconds_IgnoresNaNInfinityAndNegativeValues()
    {
        var session = new LongDefeatSession();
        session.Fill(NewRun());

        session.AddLiveSeconds(float.NaN);
        session.AddLiveSeconds(float.PositiveInfinity);
        session.AddLiveSeconds(-4f);
        session.AddLiveSeconds(0f);
        session.AddLiveSeconds(3f);

        Assert.AreEqual(3f, session.LiveSeconds);
    }

    [TestMethod]
    public void Best_SurvivesClear_AndResetsAtTheNextFill()
    {
        var session = new LongDefeatSession();
        Assert.IsNull(session.Best);
        session.Fill(NewRun());

        session.RecordBest(new BestRun(3, 250));
        session.Clear();
        Assert.AreEqual(new BestRun(3, 250), session.Best, "the end panel reads it after the mission has gone");

        session.Fill(NewRun());
        Assert.IsNull(session.Best);
    }
}
