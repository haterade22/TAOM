using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>The small pure types of the wave fight: stake choices, the roster draw, the day ledger and the spawn frame.</summary>
[TestClass]
public class WaveDomainTests
{
    private static PoolTroop T(string id, int tier) => new(id, tier);

    private static Dictionary<int, int> W(int three, int four, int five) =>
        new() { [3] = three, [4] = four, [5] = five };

    // ---- StakeOptions

    [TestMethod]
    public void StakeOptions_Cap500_GivesAQuarterAHalfAndTheFullCap()
    {
        CollectionAssert.AreEqual(new[] { 120, 250, 500 }, StakeOptions.For(500, 10).ToArray());
    }

    [TestMethod]
    public void StakeOptions_Cap30_DropsTheQuarterUnderTheMinimum()
    {
        CollectionAssert.AreEqual(new[] { 10, 30 }, StakeOptions.For(30, 10).ToArray());
    }

    [TestMethod]
    public void StakeOptions_RoundsEachDownToATen_AndDropsDuplicates()
    {
        CollectionAssert.AreEqual(new[] { 10, 20 }, StakeOptions.For(20, 10).ToArray());
        CollectionAssert.AreEqual(new[] { 10 }, StakeOptions.For(10, 10).ToArray());
        CollectionAssert.AreEqual(new[] { 10, 30 }, StakeOptions.For(35, 10).ToArray(), "35 rounds down to 30, its half 17 to 10");
    }

    [TestMethod]
    public void StakeOptions_CapUnderTheMinimum_GivesNothing()
    {
        Assert.AreEqual(0, StakeOptions.For(9, 10).Count);
        Assert.AreEqual(0, StakeOptions.For(0, 10).Count);
        Assert.AreEqual(0, StakeOptions.For(-100, 10).Count);
    }

    [TestMethod]
    public void StakeOptions_RaisedMinimum_DropsTheSmallerChoices()
    {
        CollectionAssert.AreEqual(new[] { 250, 500 }, StakeOptions.For(500, 200).ToArray());
    }

    [TestMethod]
    public void StakeOptions_HugeCap_DoesNotOverflow()
    {
        var choices = StakeOptions.For(int.MaxValue, 10);

        Assert.AreEqual(3, choices.Count);
        Assert.AreEqual(2_147_483_640, choices[2]);
    }

    // ---- WaveRoster

    private static readonly PoolTroop[] FullPool =
    {
        T("a3", 3), T("b3", 3), T("a4", 4), T("a5", 5), T("a2", 2),
    };

    [TestMethod]
    public void Build_ReturnsExactlyTheCountAsked()
    {
        Assert.AreEqual(8, WaveRoster.Build(FullPool, W(1, 1, 1), 8, n => 0).Count);
    }

    [TestMethod]
    public void Build_OnlyTierThreeWeight_NeverDrawsAHigherTier()
    {
        var random = RunRandom.For(7, 1, 0, 0);

        var ids = WaveRoster.Build(FullPool, W(1, 0, 0), 200, random.Below);

        Assert.IsTrue(ids.All(id => id == "a3" || id == "b3"));
        Assert.IsTrue(ids.Contains("a3") && ids.Contains("b3"), "both tier-3 troops are drawn");
    }

    [TestMethod]
    public void Build_Weights1To3_DrawTierFourThreeTimesAsOften()
    {
        var random = RunRandom.For(11, 1, 0, 0);

        var ids = WaveRoster.Build(FullPool, W(1, 3, 0), 4000, random.Below);

        var fours = ids.Count(id => id == "a4");
        Assert.IsTrue(fours > 2800 && fours < 3200, "tier four drawn " + fours + " of 4000");
        Assert.AreEqual(0, ids.Count(id => id == "a5"));
    }

    [TestMethod]
    public void Build_TierMissingFromThePool_FallsBackToTheNearestLower()
    {
        var pool = new[] { T("x3", 3), T("x4", 4) };

        var ids = WaveRoster.Build(pool, W(0, 0, 1), 5, n => 0);

        CollectionAssert.AreEqual(new[] { "x4", "x4", "x4", "x4", "x4" }, ids.ToArray());
    }

    [TestMethod]
    public void Build_NoLowerTier_FallsBackToTheNearestHigher()
    {
        var pool = new[] { T("x4", 4), T("x5", 5) };

        var ids = WaveRoster.Build(pool, W(1, 0, 0), 3, n => 0);

        CollectionAssert.AreEqual(new[] { "x4", "x4", "x4" }, ids.ToArray());
    }

    [TestMethod]
    public void Build_LowerBeatsHigher_WhenBothAreAvailable()
    {
        var pool = new[] { T("x3", 3), T("x5", 5) };

        var ids = WaveRoster.Build(pool, W(0, 1, 0), 3, n => 0);

        CollectionAssert.AreEqual(new[] { "x3", "x3", "x3" }, ids.ToArray());
    }

    [TestMethod]
    public void Build_EmptyPool_GivesNothing()
    {
        Assert.AreEqual(0, WaveRoster.Build(new PoolTroop[0], W(1, 1, 1), 5, n => 0).Count);
    }

    [TestMethod]
    public void Build_AllZeroWeights_GivesNothing()
    {
        Assert.AreEqual(0, WaveRoster.Build(FullPool, W(0, 0, 0), 5, n => 0).Count);
        Assert.AreEqual(0, WaveRoster.Build(FullPool, new Dictionary<int, int>(), 5, n => 0).Count);
    }

    [TestMethod]
    public void Build_CountZeroOrBelow_GivesNothing()
    {
        Assert.AreEqual(0, WaveRoster.Build(FullPool, W(1, 1, 1), 0, n => 0).Count);
        Assert.AreEqual(0, WaveRoster.Build(FullPool, W(1, 1, 1), -4, n => 0).Count);
    }

    [TestMethod]
    public void Build_ARandomSourceOutOfRange_StillPicksAPoolTroop()
    {
        var wild = WaveRoster.Build(FullPool, W(1, 1, 1), 4, n => 99);
        var negative = WaveRoster.Build(FullPool, W(1, 1, 1), 4, n => -5);

        Assert.AreEqual(4, wild.Count);
        Assert.AreEqual(4, negative.Count);
        Assert.IsTrue(wild.Concat(negative).All(id => FullPool.Any(p => p.Id == id)));
    }

    [TestMethod]
    public void Build_SameDraws_GiveTheSameRoster()
    {
        var first = WaveRoster.Build(FullPool, W(1, 1, 1), 20, RunRandom.For(3, 1, 0, 0).Below);
        var second = WaveRoster.Build(FullPool, W(1, 1, 1), 20, RunRandom.For(3, 1, 0, 0).Below);

        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
    }

    // ---- WaveDayLedger

    [TestMethod]
    public void Ledger_SameTownSameDay_RanToday()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        Assert.IsTrue(ledger.RanToday("town_a", 100));
    }

    [TestMethod]
    public void Ledger_NextDay_DidNotRunToday()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        Assert.IsFalse(ledger.RanToday("town_a", 101));
    }

    [TestMethod]
    public void Ledger_OtherTown_DidNotRunToday()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        Assert.IsFalse(ledger.RanToday("town_b", 100));
    }

    [TestMethod]
    public void Ledger_Export_DropsPastDays()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("old", 99);
        ledger.Record("today", 100);

        var exported = ledger.Export(100);

        Assert.AreEqual(1, exported.Count);
        Assert.AreEqual(100, exported["today"]);
    }

    [TestMethod]
    public void Ledger_ImportNull_LeavesItEmpty_AndItStillWorks()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        ledger.Import(null);

        Assert.IsFalse(ledger.RanToday("town_a", 100));
        ledger.Record("town_b", 100);
        Assert.IsTrue(ledger.RanToday("town_b", 100));
    }

    [TestMethod]
    public void Ledger_Import_ReplacesWhatWasThere()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        ledger.Import(new Dictionary<string, int> { ["town_b"] = 100 });

        Assert.IsTrue(ledger.RanToday("town_b", 100));
        Assert.IsFalse(ledger.RanToday("town_a", 100));
    }

    [TestMethod]
    public void Ledger_Clear_ForgetsEveryTown()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        ledger.Clear();

        Assert.IsFalse(ledger.RanToday("town_a", 100));
        Assert.AreEqual(0, ledger.Export(100).Count);
    }

    [TestMethod]
    public void Ledger_Export_IsACopy()
    {
        var ledger = new WaveDayLedger();
        ledger.Record("town_a", 100);

        ledger.Export(100)["town_a"] = 5;

        Assert.IsTrue(ledger.RanToday("town_a", 100));
    }

    // ---- SpawnPicker

    private static readonly (float X, float Y)[] Frames = { (0f, 0f), (20f, 0f), (0f, 30f), (-40f, 0f) };

    [TestMethod]
    public void Pick_SkipsFramesWithinTwelveMetres()
    {
        // Player at the first frame: that one is 0 m away, the rest are 20, 30 and 40 m.
        Assert.AreEqual(1, SpawnPicker.Pick(Frames, 0f, 0f, 0));
    }

    [TestMethod]
    public void Pick_CyclesByTurnAmongTheFarFrames()
    {
        Assert.AreEqual(1, SpawnPicker.Pick(Frames, 0f, 0f, 0));
        Assert.AreEqual(2, SpawnPicker.Pick(Frames, 0f, 0f, 1));
        Assert.AreEqual(3, SpawnPicker.Pick(Frames, 0f, 0f, 2));
        Assert.AreEqual(1, SpawnPicker.Pick(Frames, 0f, 0f, 3));
        Assert.AreEqual(3, SpawnPicker.Pick(Frames, 0f, 0f, -1));
    }

    [TestMethod]
    public void Pick_ExactlyTwelveMetres_CountsAsFar()
    {
        var frames = new[] { (12f, 0f), (0f, 0f) };

        Assert.AreEqual(0, SpawnPicker.Pick(frames, 0f, 0f, 0));
        Assert.AreEqual(0, SpawnPicker.Pick(frames, 0f, 0f, 1));
    }

    [TestMethod]
    public void Pick_JustUnderTwelveMetres_IsNear()
    {
        var frames = new[] { (11.9f, 0f), (0f, 0f) };

        Assert.AreEqual(0, SpawnPicker.Pick(frames, 0f, 0f, 0), "none is far, so the farthest wins");
    }

    [TestMethod]
    public void Pick_AllFramesNear_TakesTheFarthest()
    {
        var frames = new[] { (1f, 0f), (9f, 0f), (4f, 0f) };

        Assert.AreEqual(1, SpawnPicker.Pick(frames, 0f, 0f, 0));
        Assert.AreEqual(1, SpawnPicker.Pick(frames, 0f, 0f, 5));
    }

    [TestMethod]
    public void Pick_NoFrames_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, SpawnPicker.Pick(new (float X, float Y)[0], 0f, 0f, 0));
    }

    [TestMethod]
    public void Pick_NaNPlayerPosition_FailsTheFarGate_AndStillReturnsAFrame()
    {
        var index = SpawnPicker.Pick(Frames, float.NaN, 0f, 2);

        Assert.AreEqual(0, index, "no frame measures far from a NaN position, so the first frame is the fallback");
    }

    [TestMethod]
    public void Pick_NaNFrame_IsNeverChosenAsFar()
    {
        var frames = new[] { (float.NaN, 0f), (30f, 0f) };

        Assert.AreEqual(1, SpawnPicker.Pick(frames, 0f, 0f, 0));
        Assert.AreEqual(1, SpawnPicker.Pick(frames, 0f, 0f, 1));
    }

    [TestMethod]
    public void StakeOptions_ASmallStakeUnderTheQuarter_TakesItsPlace()
    {
        CollectionAssert.AreEqual(new[] { 120, 500, 1000 }, StakeOptions.For(1000, 10, 125).ToArray());
        CollectionAssert.AreEqual(new[] { 120, 7500, 15000 }, StakeOptions.For(15000, 10, 125).ToArray());
    }

    [TestMethod]
    public void StakeOptions_ASmallStakeAboveTheQuarter_LeavesTheQuarter()
    {
        CollectionAssert.AreEqual(new[] { 120, 250, 500 }, StakeOptions.For(500, 10, 400).ToArray());
    }

    [TestMethod]
    public void StakeOptions_ASmallStakeUnderTheMinimum_FallsBackToTheQuarter()
    {
        CollectionAssert.AreEqual(new[] { 250, 500, 1000 }, StakeOptions.For(1000, 200, 125).ToArray());
    }

    [TestMethod]
    public void StakeOptions_NoSmallStake_IsAQuarterAHalfAndTheWhole()
    {
        CollectionAssert.AreEqual(new[] { 250, 500, 1000 }, StakeOptions.For(1000, 10, 0).ToArray());
    }

    [TestMethod]
    public void StakeOptions_TheQuarterUnderTheMinimum_StillOffersTheSmallStake()
    {
        CollectionAssert.AreEqual(new[] { 250, 300, 600 }, StakeOptions.For(600, 250, 250).ToArray());
        CollectionAssert.AreEqual(new[] { 20, 30 }, StakeOptions.For(30, 11, 20).ToArray());
    }

    [TestMethod]
    public void StakeOptions_ASmallStakeAboveTheLimit_IsNotOffered()
    {
        CollectionAssert.AreEqual(new[] { 20 }, StakeOptions.For(20, 11, 30).ToArray());
        Assert.AreEqual(0, StakeOptions.For(25, 21, 30).Count, "a limit under the minimum offers nothing, the small stake included");
    }
}
