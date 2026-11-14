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
/// The opening split at the director (Mike, 2026-11-12): one split in each calm once the garrison or a pack is in, again after a pack musters or a new body is adopted, the hold handed back at the first
/// contact with a <c>split</c> line, a skip when the stalemate charge ends the hold or the hold was short, a fail when the scene gives no places, and nothing at all without a <c>split</c> block.
/// </summary>
[TestClass]
public class LongDefeatDirectorSplitTests
{
    private static readonly SplitShares Shares = new(0.7f, 0.7f);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public void Ticks(int n, float dt = 1f)
        {
            for (var i = 0; i < n; i++)
                Director.Tick(dt);
        }

        public void PumpUntil(System.Func<bool> done, int limit = 400, float dt = 1f)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(dt);
            Assert.IsTrue(done(), "the condition never came true");
        }

        public RunPhase Phase => Session.GetView()!.Phase;

        /// <summary>The split verdicts as "result key detail".</summary>
        public List<string> Verdicts() => Outcomes.Lines.Where(l => l.Split(' ')[1] == LongDefeatSplitChecks.SplitId).Select(l => l.Replace(" " + LongDefeatSplitChecks.SplitId, string.Empty)).ToList();

        public List<string> SplitLines() => Log.Lines.Where(l => l.Contains("[Split]")).ToList();

        public void InPreparationWithTheGarrison() => PumpUntil(() => Phase == RunPhase.Preparation && Field.Defenders >= 40 && Field.Count("Split") > 0);

        public void StartWave()
        {
            var refreshes = Field.Count("Refresh");
            Assert.AreEqual(StartResult.Ok, Session.TryStartWave());
            PumpUntil(() => Field.Count("Refresh") > refreshes);
        }
    }

    private static Rig NewRig(SplitShares? split = null, bool noSplit = false, RunRules? rules = null, IReadOnlyList<UpgradeStep>? upgrades = null)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }) { Upgrades = upgrades ?? System.Array.Empty<UpgradeStep>() },
            (rules ?? RunRules.Default) with { PreparationSeconds = 60f, IntermissionSeconds = 60f },
            new RunOptions { Autoplay = false })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Split = noSplit ? null : split ?? Shares,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, null, rig.Field);
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheSplit_WaitsForTheGarrison_ThenRunsOnce_WithThePlansShares()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();

        Assert.AreEqual(40, rig.Field.Defenders, "the garrison was all in before the split");
        rig.Ticks(5);
        Assert.AreEqual(1, rig.Field.Count("Split"), "one split in a calm");
        Assert.AreEqual(Shares, rig.Field.Splits[0]);
        var line = rig.SplitLines().Single();
        StringAssert.Contains(line, "[Split] preparation: wall 21 at (226.8, 147.7, 9.9), gate 9");
        StringAssert.Contains(line, "close 0,0,0");
    }

    [TestMethod]
    public void NoSplitBlock_NeverSplits_NorReadsTheAttackers()
    {
        var rig = NewRig(noSplit: true);
        Assert.IsTrue(rig.Director.Start());
        rig.PumpUntil(() => rig.Phase == RunPhase.Preparation && rig.Field.Defenders >= 40);
        rig.StartWave();
        rig.Ticks(10);

        Assert.AreEqual(0, rig.Field.Count("Split"));
        Assert.AreEqual(0, rig.Field.CloseReads);
        Assert.AreEqual(0, rig.Verdicts().Count);
    }

    [TestMethod]
    public void APack_MusteredWhileHeld_SplitsAgain_AndKeepsTheHoldsClock()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.Ticks(15);

        Assert.AreEqual(BuyResult.Ok, rig.Session.TryBuy("militia"));
        rig.PumpUntil(() => rig.Field.Count("Split") == 2);

        Assert.IsTrue(rig.Field.Defenders >= 52, "the pack was in before the second split");
        StringAssert.Contains(rig.SplitLines().Last(), "[Split] a pack mustered:");
        rig.Ticks(10);
        rig.StartWave();
        rig.Ticks(3);
        rig.Field.CloseAttackers[1] = 2;
        rig.Ticks(1);

        var verdict = rig.Verdicts().Single();
        StringAssert.StartsWith(verdict, "pass w1 ");
        Assert.IsFalse(verdict.Contains("short-hold"), "the hold counts from the first split: " + verdict);
    }

    [TestMethod]
    public void TheFirstContact_HandsTheGroupsBack_AndPasses_WhenEveryGroupStoodAtItsPlace()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.Ticks(25);
        rig.StartWave();
        Assert.IsTrue(rig.SplitLines().Any(l => l.Contains("[Split] wave 1 starts: wall 21 median 6.5 m high 18")), string.Join("\n", rig.SplitLines()));

        rig.Ticks(5);
        Assert.AreEqual(0, rig.Field.Count("ReleaseSplit"), "no attacker at the walls: still held");
        Assert.IsTrue(rig.Field.CloseReads > 0, "the attackers are read in the wave");

        rig.Field.CloseAttackers[0] = 1;
        rig.Ticks(1);

        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"));
        var verdict = rig.Verdicts().Single();
        StringAssert.StartsWith(verdict, "pass w1 ");
        StringAssert.Contains(verdict, "wall=21 wall_median=6.5 wall_high=18");
        StringAssert.Contains(verdict, "released=4 kept=0 close=1,0,0");
        StringAssert.Contains(rig.SplitLines().Last(), "contact in wave 1: close 0,0,0 to 1,0,0");
        rig.Ticks(5);
        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"), "released once");
    }

    [TestMethod]
    public void AGroupFarFromItsPlace_Fails()
    {
        var rig = NewRig();
        rig.Field.SplitGroups[1] = new LdSplitGroup("gate", 9, LongDefeatSplitChecks.FootReach + 5f, 0, false);
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.Ticks(25);
        rig.StartWave();
        rig.Ticks(2);
        rig.Field.CloseAttackers[2] = 2;
        rig.Ticks(1);

        StringAssert.StartsWith(rig.Verdicts().Single(), "fail w1 ");
    }

    [TestMethod]
    public void AShortHold_IsASkip()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();
        rig.Field.CloseAttackers[0] = 1;
        rig.Ticks(1);

        var verdict = rig.Verdicts().Single();
        StringAssert.StartsWith(verdict, "skip w1 ");
        StringAssert.EndsWith(verdict, "reason=short-hold");
    }

    [TestMethod]
    public void TheStalemateCharge_EndsTheHold_WithASkip_AndNoReleaseCall()
    {
        var rig = NewRig(rules: new RunRules { StragglerCount = 0 });
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();
        rig.PumpUntil(() => rig.Field.AttackersStanding >= 10);
        while (rig.Field.AttackersStanding > 3)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        rig.PumpUntil(() => rig.Field.Calls.Contains("Charge"), dt: 30f);

        Assert.AreEqual(0, rig.Field.Count("ReleaseSplit"));
        var verdict = rig.Verdicts().Single();
        StringAssert.StartsWith(verdict, "skip w1 ");
        StringAssert.EndsWith(verdict, "reason=charge");
        rig.Field.CloseAttackers[0] = 2;
        rig.Ticks(2);
        Assert.AreEqual(1, rig.Verdicts().Count, "a dropped hold writes no second line");
    }

    [TestMethod]
    public void ASceneWithNoPlaces_FailsOnce_AndTheDefendersStayWithTheAi()
    {
        var rig = NewRig();
        rig.Field.SplitAnswer = LdSplitResult.Refused("no wall segment off the gate's side");
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.Ticks(10);
        rig.StartWave();
        rig.Field.CloseAttackers[0] = 2;
        rig.Ticks(3);

        Assert.AreEqual(1, rig.Field.Count("Split"), "no retry in the same calm");
        Assert.AreEqual(0, rig.Field.Count("ReleaseSplit"));
        Assert.AreEqual(0, rig.Field.CloseReads, "nothing held: no read");
        Assert.AreEqual("fail w1 w=1 reason=no-places problem=no_wall_segment_off_the_gate's_side", rig.Verdicts().Single());
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Split] not split (preparation): no wall segment off the gate's side")));
    }

    [TestMethod]
    public void APartialSplit_HoldsThePairItCouldPlace_AndFailsForTheOther()
    {
        var rig = NewRig();
        rig.Field.SplitAnswer = rig.Field.SplitAnswer with { HighAt = null, LowArchers = 0, HighArchers = 0 };
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();

        Assert.AreEqual("fail w1 w=1 reason=no-places problem=no_places_for_the_archers", rig.Verdicts().Single());
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Split] preparation: no places for the archers; that pair stays with the AI")));
        Assert.IsTrue(rig.SplitLines().Any(l => l.Contains("[Split] preparation: wall 21 at")), "the infantry pair is still split and held");
    }

    [TestMethod]
    public void ANewBody_WhileHeld_SplitsAgain()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        var splits = rig.Field.Count("Split");

        rig.Field.MainBody = 2;
        rig.Ticks(1);

        Assert.AreEqual(splits + 1, rig.Field.Count("Split"));
        StringAssert.Contains(rig.SplitLines().Last(), "[Split] a new body:");
        Assert.IsTrue(rig.Field.Index("Split") < rig.Field.Calls.LastIndexOf("Split"));
        Assert.IsTrue(rig.Field.Calls.LastIndexOf("Adopt:False") < rig.Field.Calls.LastIndexOf("Split"), "the split follows the adoption that released it");
    }

    [TestMethod]
    public void AnUpgrade_WhileHeld_SplitsAgain()
    {
        var step = new UpgradeStep("fow_militia>fow_axe", "{=K_A}Axemen", "fow_militia", "fow_axe", 15);
        var rig = NewRig(upgrades: new[] { step });
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        var splits = rig.Field.Count("Split");

        Assert.AreEqual(UpgradeResult.Ok, rig.Session.TryUpgrade(step.Id));
        rig.Ticks(1, 0.1f);

        Assert.AreEqual(1, rig.Field.Replaces.Count);
        Assert.AreEqual(splits + 1, rig.Field.Count("Split"), "a fade can empty a held group, and Formation.RemoveUnit then hands it to the AI");
        StringAssert.Contains(rig.SplitLines().Last(), "[Split] an upgrade:");
    }

    [TestMethod]
    public void AnUpgradeThatReplacedNobody_WhileHeld_DoesNotSplitAgain()
    {
        var step = new UpgradeStep("fow_militia>fow_axe", "{=K_A}Axemen", "fow_militia", "fow_axe", 15);
        var rig = NewRig(upgrades: new[] { step });
        rig.Field.StandingByTroop["fow_militia"] = 30;
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        var splits = rig.Field.Count("Split");
        rig.Field.ReplaceAnswer = new LdUpgradeReport(0, 0, 0, null);

        Assert.AreEqual(UpgradeResult.Ok, rig.Session.TryUpgrade(step.Id));
        rig.Ticks(1, 0.1f);

        Assert.AreEqual(1, rig.Field.Replaces.Count);
        Assert.AreEqual(splits, rig.Field.Count("Split"), "no soldier faded, so no group was emptied");
    }

    [TestMethod]
    public void TheNextCalm_SplitsAgain_ForTheNextWave()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();
        rig.Field.CloseAttackers[0] = 1;
        rig.Ticks(1);
        rig.Field.CloseAttackers[0] = 0;

        for (var round = 0; round < 40 && rig.Phase == RunPhase.Wave; round++)
        {
            while (rig.Field.AttackersStanding > 0)
            {
                rig.Field.AttackersStanding--;
                rig.Director.OnAgentRemoved("A:orc", true);
            }

            rig.Ticks(1);
        }

        Assert.AreEqual(RunPhase.Intermission, rig.Phase);
        rig.PumpUntil(() => rig.Field.Count("Split") == 2, limit: 5);
        StringAssert.Contains(rig.SplitLines().Last(l => l.Contains(": wall ")), "[Split] intermission:");
    }

    // The engine caches the close counts for 5 s, so at a clear the cache can still hold the last wave's counts. Unrefreshed, they hid the next wave's contact as the hold's baseline,
    // or, after a horn within 5 s, released the hold at the new wave's first tick.
    [TestMethod]
    public void AFreshSplit_RefreshesTheEngineCache_SoTheLastWavesCountNeitherHidesNorFakesContact()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();
        rig.Field.CloseAttackers[0] = 2;
        rig.Ticks(1);
        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"));
        rig.Field.CachedClose = new List<int> { 2, 0, 0 };
        rig.Field.CloseAttackers[0] = 0;

        for (var round = 0; round < 40 && rig.Phase == RunPhase.Wave; round++)
        {
            while (rig.Field.AttackersStanding > 0)
            {
                rig.Field.AttackersStanding--;
                rig.Director.OnAgentRemoved("A:orc", true);
            }

            rig.Ticks(1);
        }

        Assert.AreEqual(RunPhase.Intermission, rig.Phase);
        rig.PumpUntil(() => rig.Field.Count("Split") == 2, limit: 5);
        StringAssert.Contains(rig.SplitLines().Last(l => l.Contains(": wall ")), "close 0,0,0");

        rig.StartWave();
        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"), "a horn at once after the split reads the refreshed counts, not the last wave's");
        rig.Field.CloseAttackers[0] = 1;
        rig.Ticks(1);

        Assert.AreEqual(2, rig.Field.Count("ReleaseSplit"), "one attacker at the walls in wave 2 is contact, whatever the cache held at the clear");
        StringAssert.Contains(rig.SplitLines().Last(), "contact in wave 2: close 0,0,0 to 1,0,0");
    }

    [TestMethod]
    public void TheLordsFall_WhileHeld_SplitsAgainOnTheNextTick_AndKeepsTheContactBaseline()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.Ticks(25);
        rig.StartWave();
        var splits = rig.Field.Count("Split");

        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Field.CloseAttackers[0] = 1;
        rig.Ticks(1);

        Assert.AreEqual(splits + 1, rig.Field.Count("Split"), "the engine handed every formation to the AI; the split takes them back");
        Assert.IsTrue(rig.SplitLines().Any(l => l.Contains("[Split] the lord fell:")), string.Join("\n", rig.SplitLines()));
        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"), "the re-split kept the hold's counts, so the risen count is contact");
        StringAssert.StartsWith(rig.Verdicts().Single(), "pass w1 ");
    }

    [TestMethod]
    public void AWaveThatClearsWithNoContact_HandsTheGroupsBack_WithASkip_AndTheNextCalmSplitsAfresh()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();

        for (var round = 0; round < 40 && rig.Phase == RunPhase.Wave; round++)
        {
            while (rig.Field.AttackersStanding > 0)
            {
                rig.Field.AttackersStanding--;
                rig.Director.OnAgentRemoved("A:orc", true);
            }

            rig.Ticks(1);
        }

        Assert.AreEqual(RunPhase.Intermission, rig.Phase);
        Assert.AreEqual(1, rig.Field.Count("ReleaseSplit"));
        var verdict = rig.Verdicts().Single();
        StringAssert.StartsWith(verdict, "skip w1 w=1 wall=21 ");
        StringAssert.EndsWith(verdict, "reason=no-contact");
        rig.PumpUntil(() => rig.Field.Count("Split") == 2, limit: 5);
        StringAssert.Contains(rig.SplitLines().Last(l => l.Contains(": wall ")), "[Split] intermission:");
    }

    [TestMethod]
    public void ARunThatEndsDuringAHold_WritesARunEndedSkip()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.InPreparationWithTheGarrison();
        rig.StartWave();

        rig.Field.Defenders = 0;
        rig.PumpUntil(() => rig.Director.Result != null, limit: 10);

        StringAssert.EndsWith(rig.Verdicts().Single(), "reason=run-ended");
    }
}
