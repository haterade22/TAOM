using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The director's half of the upgrades (#1161): it reads the standing defenders by troop between waves and pushes them into the run, carries out each order on the next tick, books what the field
/// replaced, and writes an <c>upgrade</c> line for each order.
/// </summary>
[TestClass]
public class LongDefeatDirectorUpgradeTests
{
    private static readonly UpgradeStep MilitiaToAxe = new("fow_militia>fow_axe", "{=K_A}Axemen", "fow_militia", "fow_axe", 15);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool autoplay = false, int gold = 3000)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75) }) }, new WaveScaling(10, 300)),
            new ShopPlan(gold, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }) { Upgrades = new[] { MilitiaToAxe } },
            RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 60f, GarrisonCap = 30 },
            new RunOptions { Autoplay = autoplay })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
        };
        var effects = new FakeLongDefeatEffects(rig.Field.Calls);
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, effects, rig.Field);
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void BetweenWaves_TheStandingCountsReachTheShop()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());

        rig.Director.Tick(0.1f);

        var row = rig.Session.GetView()!.Upgrades.Single();
        Assert.AreEqual(MilitiaToAxe, row.Step);
        Assert.AreEqual(30, row.Standing);
    }

    [TestMethod]
    public void ABoughtUpgrade_IsCarriedOutOnTheNextTick_BookedAndPassed()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        Assert.AreEqual(UpgradeResult.Ok, rig.Session.TryUpgrade(MilitiaToAxe.Id));
        Assert.AreEqual(0, rig.Field.Replaces.Count, "the order waits for the next tick");
        rig.Director.Tick(0.1f);

        Assert.AreEqual(1, rig.Field.Replaces.Count);
        Assert.AreEqual(("fow_militia", "fow_axe", 30, 1f), rig.Field.Replaces[0]);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatUpgradeChecks.Id), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Outcomes.Of(LongDefeatUpgradeChecks.Id).Single(), "from=fow_militia to=fow_axe ordered=30 found=30 replaced=30 read=30");
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatUpgradeChecks.Id).Single(), "m1u1 ", "the key names the mission: the outcome log is one per process and writes a key once");
        Assert.AreEqual(3000 - 30 * 15, rig.Session.GetView()!.Gold);
        Assert.AreEqual(0, rig.Session.GetView()!.Upgrades.Count, "no militia is left to upgrade");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("INFO ") && l.Contains("[Upgrade]") && l.Contains("replaced 30")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void AShortReplace_PaysTheRestBack_AndStillPasses_WhenEverySoldierFoundWasReplaced()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryUpgrade(MilitiaToAxe.Id);
        rig.Field.ReplaceAnswer = new LdUpgradeReport(28, 28, 28, null);

        rig.Director.Tick(0.1f);

        Assert.AreEqual(3000 - 28 * 15, rig.Session.GetView()!.Gold);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatUpgradeChecks.Id), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ANewSoldierThatDoesNotReadBack_Fails_AndTheLogNamesTheFirstMiss()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryUpgrade(MilitiaToAxe.Id);
        rig.Field.ReplaceAnswer = new LdUpgradeReport(30, 30, 29, null, "(1.0, 2.0, 3.0) stood at (5.0, 2.0, 3.0)");

        rig.Director.Tick(0.1f);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatUpgradeChecks.Id), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Upgrade]") && l.Contains(", first miss (1.0, 2.0, 3.0) stood at (5.0, 2.0, 3.0)")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void ASoldierFadedButNotReplaced_FailsTheCheck_AndHisGoldIsPaidBack()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryUpgrade(MilitiaToAxe.Id);
        rig.Field.ReplaceAnswer = new LdUpgradeReport(30, 29, 29, null);

        rig.Director.Tick(0.1f);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatUpgradeChecks.Id), "found 30 and replaced 29: one new soldier was not active. " + string.Join("\n", rig.Outcomes.Lines));
        Assert.AreEqual(3000 - 29 * 15, rig.Session.GetView()!.Gold);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Upgrade]") && l.Contains("replaced 29") && l.Contains("paid 435")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void AMissingTroop_Fails_AndPaysEverythingBack()
    {
        var rig = NewRig();
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryUpgrade(MilitiaToAxe.Id);
        rig.Field.ReplaceAnswer = new LdUpgradeReport(0, 0, 0, "no troop 'fow_axe'");

        rig.Director.Tick(0.1f);

        Assert.AreEqual(3000, rig.Session.GetView()!.Gold);
        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatUpgradeChecks.Id), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Outcomes.Of(LongDefeatUpgradeChecks.Id).Single(), "problem=no_troop_'fow_axe'");
    }

    [TestMethod]
    public void TheBot_SpendsGoldNoPackCanUse_OnAnUpgrade()
    {
        var rig = NewRig(autoplay: true);
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());

        for (var i = 0; i < 5 && rig.Field.Replaces.Count == 0; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(1, rig.Field.Replaces.Count, "the garrison is at its cap, so the bot's gold goes to the upgrade");
    }
}
