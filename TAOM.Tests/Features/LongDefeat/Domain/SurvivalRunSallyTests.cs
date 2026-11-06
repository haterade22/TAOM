using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The sally in the run (phase2-plan.md Slice 10): a mounted class goes out with each wave's start effect and home with each clear's, the target-reached clear included; a foot class or a run
/// with no class never asks for a move. Whether the point is usable and whether the body is the lord's own are the director's questions, not the run's.
/// </summary>
[TestClass]
public class SurvivalRunSallyTests
{
    private static LdLoadout Rider() => new("cavalry", true, new[] { new LdKitSlot("Horse", "horse_a") });

    private static LdLoadout Foot() => new("archer", false, new[] { new LdKitSlot("Horse", "") });

    private static RunPlan Plan(LdLoadout? loadout, int? stopAfter = 2)
    {
        var small = new WaveSpec(6, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) });
        return new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(Enumerable.Repeat(small, 4).ToList(), new WaveScaling(0, 6)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
            new RunOptions { Seed = 1UL, StopAfterWaves = stopAfter, ClassId = loadout?.ClassId })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            Loadout = loadout,
        };
    }

    // Every attacker dies as it spawns, so the wave clears as soon as its queue is empty.
    private static RunSnapshot Standing() => new(1f, 0, 0, 40, 0, 0, 1000);

    /// <summary>Plays the run to its end, one tick at a time; returns each tick's effects.</summary>
    private static List<RunEffects> Play(RunPlan plan)
    {
        var run = new SurvivalRun(plan);
        var all = new List<RunEffects> { run.Begin() };
        for (var i = 0; i < 400 && run.Result == null; i++)
        {
            var fx = run.Tick(Standing());
            all.Add(fx);
        }

        Assert.IsNotNull(run.Result, "the run never ended");
        return all;
    }

    [TestMethod]
    public void AMountedClass_AsksToGoOut_WithEachWaveStart_AndHomeWithEachClear()
    {
        var all = Play(Plan(Rider()));

        var starts = all.Where(fx => fx.StartWave != null).ToList();
        var clears = all.Where(fx => fx.Events.Any(e => e.Kind == RunEventKind.WaveCleared)).ToList();
        Assert.AreEqual(2, starts.Count);
        Assert.AreEqual(2, clears.Count);
        Assert.IsTrue(starts.All(fx => fx.Sally == SallyMove.Out), "every wave start goes out");
        Assert.IsTrue(clears.All(fx => fx.Sally == SallyMove.Home), "every clear, the last one included, comes home");
    }

    [TestMethod]
    public void TheMoveIsAskedForOnlyOnTheStartAndClearTicks()
    {
        var all = Play(Plan(Rider()));

        var moves = all.Where(fx => fx.Sally != SallyMove.None).ToList();
        Assert.AreEqual(4, moves.Count, "two waves, one out and one home each");
        Assert.IsTrue(all.Where(fx => fx.StartWave == null && !fx.Events.Any(e => e.Kind == RunEventKind.WaveCleared)).All(fx => fx.Sally == SallyMove.None));
    }

    [TestMethod]
    public void AFootClass_AndARunWithNoClass_NeverAskForAMove()
    {
        Assert.IsTrue(Play(Plan(Foot())).All(fx => fx.Sally == SallyMove.None));
        Assert.IsTrue(Play(Plan(null)).All(fx => fx.Sally == SallyMove.None));
    }

    [TestMethod]
    public void TheClearThatEndsTheRun_StillBringsTheLordHome()
    {
        var all = Play(Plan(Rider(), stopAfter: 1));

        var last = all.Last(fx => fx.Events.Any(e => e.Kind == RunEventKind.WaveCleared));
        Assert.AreEqual(SallyMove.Home, last.Sally);
    }

    [TestMethod]
    public void LdPlace_FromAMusterFrame_KeepsThePointAndTheFacing_AndAKeepPointHasNoFacing()
    {
        var place = LdPlace.From(new MusterFrame(1f, 2f, 3f, 0f, -1f));
        var keep = LdPlace.At(new LdPoint(4f, 5f, 6f));

        Assert.AreEqual(new LdPoint(1f, 2f, 3f), place.Point);
        Assert.IsTrue(place.HasFacing);
        Assert.AreEqual(new LdPoint(4f, 5f, 6f), keep.Point);
        Assert.IsFalse(keep.HasFacing, "a keep carries no facing: the lord keeps the one he has");
    }

    [TestMethod]
    public void LdPlace_FlatDistance_IgnoresHeight()
    {
        var place = LdPlace.At(new LdPoint(0f, 0f, 0f));

        Assert.AreEqual(5f, place.FlatDistanceTo(new LdPoint(3f, 4f, 99f)), 1e-4f);
    }

    [TestMethod]
    public void LdPointFacts_AreUsableOnlyWithBothANavmeshAndTheBoundary()
    {
        Assert.IsTrue(new LdPointFacts(true, true).Usable);
        Assert.IsFalse(new LdPointFacts(true, false).Usable);
        Assert.IsFalse(new LdPointFacts(false, true).Usable);
    }
}
