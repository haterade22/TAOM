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
/// Phase 3 Slice 7, the posted archers at the director (phase3-plan.md D20, D30a, spike S6): the fill on the tick after a tower level is bought and at every clear before any pack, none in a wave, the
/// garrison cap respected with a short fill named, the once-a-second hold, the held sample 30 s in, the arrows counted by the logic's callback, the <c>posts</c> line at the clear or with <c>ended=run</c>, and the
/// boot test that drops a bad post, the fill that moves a stranger out of the posts formation, and a failed spawn named apart from the cap.
/// </summary>
[TestClass]
public class LongDefeatDirectorPostsTests
{
    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static MusterFrame Post(float x) => new(x, 0f, 30f, 0f, 1f);

    private static IReadOnlyList<LdTower> TwoTowers(int posts = 8) => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, posts).Select(i => Post(i)).ToList(), System.Array.Empty<LdPoint>()),
        new LdTower("wall", Enumerable.Range(0, posts).Select(i => Post(10 + i)).ToList(), System.Array.Empty<LdPoint>()),
    };

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public void Tick(float dt = 1f) => Director.Tick(dt);

        public void Ticks(int n, float dt = 1f)
        {
            for (var i = 0; i < n; i++)
                Director.Tick(dt);
        }

        public void PumpUntil(System.Func<bool> done, int limit = 400)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(1f);
            Assert.IsTrue(done(), "the condition never came true");
        }

        /// <summary>The posts verdicts as "result key detail".</summary>
        public List<string> Posts() => Outcomes.Lines.Where(l => l.Split(' ')[1] == LongDefeatChecks.PostsId).Select(l => l.Replace(" " + LongDefeatChecks.PostsId, string.Empty)).ToList();

        public void ClearWave()
        {
            for (var round = 0; round < 40 && Session.GetView()!.Phase == RunPhase.Wave; round++)
            {
                while (Field.AttackersStanding > 0)
                {
                    Field.AttackersStanding--;
                    Director.OnAgentRemoved("A:orc", true);
                }

                Tick();
            }

            Assert.AreEqual(RunPhase.Intermission, Session.GetView()!.Phase);
        }

        public void StartNextWave()
        {
            var refreshes = Field.Count("Refresh");
            Assert.AreEqual(StartResult.Ok, Session.TryStartWave());
            PumpUntil(() => Field.Count("Refresh") > refreshes);
        }

        public int CallIndex(string prefix) => Field.Calls.FindIndex(c => c.StartsWith(prefix, System.StringComparison.Ordinal));
    }

    private static Rig NewRig(IReadOnlyList<LdTower>? towers = null, RunRules? rules = null, FortOffer? offered = null, int waveSize = 60, FortPlan? fort = null)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(waveSize, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[]
            {
                new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
                new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
            }, fort ?? new FortPlan(null, Towers, null, null)),
            rules ?? RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 0.5f },
            new RunOptions { Autoplay = false })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? FortOffer.Offered,
            Towers = towers ?? TwoTowers(),
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, null, rig.Field);
        return rig;
    }

    /// <summary>A started run with tower level 1 bought in the preparation and the wave begun; the fill has run.</summary>
    private static Rig InWaveOne(RunRules? rules = null, IReadOnlyList<LdTower>? towers = null, System.Action<FakeLongDefeatField>? prepare = null)
    {
        var rig = NewRig(towers, rules);
        prepare?.Invoke(rig.Field);
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- the fill ----

    [TestMethod]
    public void ATowerLevel_IsManned_OnTheTickAfterTheBuy_WithTheLevelsPostsOnEachTower()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        Assert.AreEqual(0, rig.Field.PostFills.Count, "the buy does not touch the engine; the next tick does");
        rig.Tick();

        Assert.AreEqual(1, rig.Field.PostFills.Count);
        Assert.AreEqual("fow_rohan_eastfold_bowman", rig.Field.PostFills[0].Troop);
        Assert.AreEqual(4, rig.Field.PostFills[0].Posts);
        CollectionAssert.AreEquivalent(new[] { 4, 4 }, rig.Field.Archers.Values.ToArray());
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] fill") && l.Contains("hornburg 0 standing + 4 spawned of 4")));
    }

    [TestMethod]
    public void TheFill_IsAppliedBeforeAnyPack_AndAfterTheGateAndBeforeFieldCare()
    {
        var gate = new GatePlan(200, new[] { new GateLevelPlan(50f, 200) });
        var rig = NewRig(fort: new FortPlan(gate, Towers, new CarePlan(0.75f, 150, 1), null));
        Assert.IsTrue(rig.Director.Start());
        rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Preparation && rig.Field.Defenders >= 40);
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyGateLevel());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        Assert.AreEqual(BuyResult.Ok, rig.Session.TryBuy("militia"));
        rig.Tick();

        var gateAt = rig.CallIndex("GateLevel");
        var fillAt = rig.CallIndex("FillPosts");
        var packAt = rig.Field.Calls.FindLastIndex(c => c.StartsWith("Spawn:def:fow_militia", System.StringComparison.Ordinal));
        Assert.IsTrue(gateAt >= 0 && fillAt > gateAt, $"gate {gateAt} before fill {fillAt}");
        Assert.IsTrue(packAt > fillAt, $"fill {fillAt} before the pack {packAt}: the posts come first (D20)");
    }

    [TestMethod]
    public void ATowerLevel_BoughtInAWave_IsRefused_AndNothingIsManned()
    {
        var rig = InWaveOne();
        var fills = rig.Field.PostFills.Count;

        Assert.AreEqual(FortResult.NotOpen, rig.Session.TryBuyTowerLevel());
        rig.Ticks(3);

        Assert.AreEqual(fills, rig.Field.PostFills.Count);
    }

    [TestMethod]
    public void AShortFill_IsNamed_AndTheCheckExpectsOnlyWhatTheFieldCouldMan()
    {
        var rig = NewRig();
        rig.Field.PostsSpawnLimit = 6;
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 4;
        rig.Field.TowerShots["wall"] = 4;

        rig.ClearWave();

        var lines = rig.Posts();
        Assert.AreEqual(2, lines.Count);
        Assert.IsTrue(lines.All(l => l.StartsWith("pass ", System.StringComparison.Ordinal)), string.Join("\n", lines));
        Assert.IsTrue(lines.Any(l => l.Contains("short=2")), "the wall's last two posts: 4 + 2 of 8 asked");
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.PostsId), string.Join("\n", lines));
    }

    [TestMethod]
    public void AClear_RefillsTheDeadPosts_ForFree_BeforeThePacks()
    {
        var rig = InWaveOne();
        rig.Ticks(3);
        rig.Field.KillArchers("hornburg", 3);

        rig.ClearWave();

        Assert.AreEqual(2, rig.Field.PostFills.Count);
        Assert.AreEqual(4, rig.Field.Archers["hornburg"], "the three dead posts are manned again");
        Assert.AreEqual(4, rig.Field.Archers["wall"]);
        Assert.AreEqual(0, rig.Field.PostFills[1].Posts - 4, "the refill asks for the level's four posts a tower");
    }

    // ---- the posts line ----

    [TestMethod]
    public void AWaveWithMannedPostsThatHoldAndShoot_PassesAtTheClear_OnePerTower()
    {
        var rig = InWaveOne();
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 9;
        rig.Field.TowerShots["wall"] = 5;

        rig.ClearWave();

        var lines = rig.Posts();
        Assert.AreEqual(2, lines.Count);
        StringAssert.StartsWith(lines[0], "pass hornburg-w1 ");
        StringAssert.Contains(lines[0], "tower=hornburg w=1 level=1 variant=A formation=4 posts=8 expected=4 manned=4 held=4 sampled=4");
        StringAssert.Contains(lines[0], "shots=9");
        StringAssert.StartsWith(lines[1], "pass wall-w1 ");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] hornburg w1 Pass: ") && l.Contains("held=4 sampled=4") && l.Contains("variant=A")));
    }

    [TestMethod]
    public void TheHeldSample_IsTakenThirtySecondsIn_NotBefore()
    {
        var rig = InWaveOne();
        rig.Ticks(10);
        rig.Field.ArcherDrift = 4f;
        rig.Field.TowerShots["hornburg"] = 1;
        rig.Field.TowerShots["wall"] = 1;
        rig.Ticks(25);
        rig.ClearWave();

        Assert.IsTrue(rig.Posts().All(l => l.StartsWith("fail ", System.StringComparison.Ordinal)), "archers off their posts at the sample fail the line");
    }

    [TestMethod]
    public void AnArcherWhoStoodAndWasThenFarOff_BeforeTheSample_DoesNotFail_WhenTheWaveEndedFirst()
    {
        var rig = InWaveOne();
        rig.Ticks(3);
        rig.Field.ArcherDrift = 4f;
        rig.Field.TowerShots["hornburg"] = 1;
        rig.Field.TowerShots["wall"] = 1;
        rig.ClearWave();

        Assert.IsTrue(rig.Posts().All(l => l.StartsWith("skip ", System.StringComparison.Ordinal) && l.Contains("reason=short-wave")), "the sample was never due");
    }

    [TestMethod]
    public void PostsThatNeverShoot_FailAtTheClear()
    {
        var rig = InWaveOne();
        rig.Ticks(35);

        rig.ClearWave();

        Assert.IsTrue(rig.Posts().All(l => l.StartsWith("fail ", System.StringComparison.Ordinal) && l.Contains("shots=0")));
    }

    [TestMethod]
    public void ARunThatEndsInAWave_WritesTheLineWithEndedRun()
    {
        var rig = InWaveOne();
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 2;
        rig.Field.TowerShots["wall"] = 0;
        rig.Tick();

        rig.Director.Shutdown();

        var lines = rig.Posts();
        Assert.AreEqual(2, lines.Count);
        StringAssert.Contains(lines[0], "ended=run");
        StringAssert.StartsWith(lines[0], "pass ");
        StringAssert.StartsWith(lines[1], "skip ");
        StringAssert.Contains(lines[1], "reason=window-cut");
    }

    [TestMethod]
    public void ARunWithNoTowerLevel_WritesNoPostsLine_AndAsksTheFieldNothing()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.ClearWave();

        Assert.AreEqual(0, rig.Posts().Count);
        Assert.AreEqual(0, rig.Field.Count("ReadPosts"));
        Assert.AreEqual(0, rig.Field.HoldCalls);
    }

    // ---- the hold (D30a) ----

    [TestMethod]
    public void TheHold_IsReasserted_OnceASecond_InAWave_AndNeverBetweenWaves()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Session.TryBuyTowerLevel();
        rig.Ticks(3);
        Assert.AreEqual(0, rig.Field.HoldCalls, "the preparation holds nothing");

        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        var before = rig.Field.HoldCalls;
        rig.Ticks(10);
        Assert.AreEqual(before + 10, rig.Field.HoldCalls, "a tick a second, a hold a second");

        rig.Ticks(5, 0.2f);
        Assert.AreEqual(before + 11, rig.Field.HoldCalls, "five ticks of 0.2 s are one more second");
    }

    [TestMethod]
    public void WhatTheHoldMoved_IsCountedIntoTheLine()
    {
        var rig = InWaveOne(prepare: f =>
        {
            f.HoldResults.Add(new LdPostsHold(1, 2, true));
            f.HoldResults.Add(LdPostsHold.Nothing);
        });
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 3;
        rig.Field.TowerShots["wall"] = 3;

        rig.ClearWave();

        StringAssert.Contains(rig.Posts()[0], "split=2 out=1");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] hold: AI regained yes") && l.Contains("1 agent(s) moved out") && l.Contains("2 post archer(s) moved back")));
    }

    [TestMethod]
    public void TheVariant_IsNamedOnTheLine()
    {
        var rig = NewRig();
        rig.Field.PostsVariantValue = PostsVariant.C;
        Assert.IsTrue(rig.Director.Start());
        rig.Session.TryBuyTowerLevel();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Ticks(35);
        rig.ClearWave();

        Assert.IsTrue(rig.Posts().All(l => l.Contains("variant=C")));
    }

    // ---- the arrows ----

    [TestMethod]
    public void AShot_IsForwardedToTheField_OnlyWhileAWaveWithPostsRuns()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        Assert.IsFalse(rig.Director.WantsPostShots);
        rig.Director.OnPostShot("hornburg");
        Assert.AreEqual(0, rig.Field.PostShotsNoted.Count);

        rig.Session.TryBuyTowerLevel();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        Assert.IsTrue(rig.Director.WantsPostShots);
        rig.Director.OnPostShot("hornburg");
        Assert.AreEqual(1, rig.Field.PostShotsNoted.Count);

        rig.ClearWave();
        Assert.IsFalse(rig.Director.WantsPostShots, "the clear closes the window");
    }

    // ---- the boot test ----

    [TestMethod]
    public void ABadPost_IsDropped_AtTheBoot_AndOnlyTheGoodOnesAreManned()
    {
        var rig = NewRig(TwoTowers(4));
        rig.Field.FortPointFacts = place => new TAOM.Features.LongDefeat.Domain.LdPointFacts(place.X != 1f, true);
        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.Tick();

        Assert.AreEqual(3, rig.Field.PostTowers[0].First(t => t.Id == "hornburg").Posts.Count, "the post at x 1 had no navmesh");
        Assert.AreEqual(3, rig.Field.Archers["hornburg"]);
        Assert.AreEqual(4, rig.Field.Archers["wall"]);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("hornburg.post[1] is unusable and is dropped")));
    }

    [TestMethod]
    public void ATowerWhoseEveryPointFailed_IsDropped_AndWithNoneLeftTheRowIsHidden()
    {
        var rig = NewRig(TwoTowers(4));
        rig.Field.FortPointFacts = _ => new LdPointFacts(false, true);
        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(FortResult.Hidden, rig.Session.TryBuyTowerLevel());
        Assert.AreEqual(0, rig.Field.PostFills.Count);
    }

    [TestMethod]
    public void AStrangerInThePostsFormation_IsMovedOut_AtTheFill_AndTheRowsStayOnSale()
    {
        // The tactic can split units into index 4 between a wave's re-asserts: the fill moves them out as the hold does and mans the posts, instead of giving up after the player paid.
        var rig = NewRig();
        rig.Field.PostsStrangers = 3;
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());

        rig.Tick();

        CollectionAssert.AreEquivalent(new[] { 4, 4 }, rig.Field.Archers.Values.ToArray());
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] fill") && l.Contains("3 soldier(s) that were not post archers moved out")), string.Join(" | ", rig.Log.Lines.Where(l => l.Contains("[Posts]"))));
        rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Wave);
        rig.ClearWave();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel(), "the rows were never taken off sale");
        Assert.IsFalse(rig.Log.Lines.Any(l => l.StartsWith("ERROR", System.StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ASpawnThatFails_IsNamedFailed_NotShort_AndFailsTheLine()
    {
        var rig = NewRig();
        rig.Field.PostsSpawnFailures = 2;
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 5;
        rig.Field.TowerShots["wall"] = 5;

        rig.ClearWave();

        var lines = rig.Posts();
        var hornburg = lines.Single(l => l.Contains("tower=hornburg"));
        StringAssert.StartsWith(hornburg, "fail ");
        StringAssert.Contains(hornburg, "expected=4 manned=2");
        StringAssert.Contains(hornburg, "short=0 failed=2");
        StringAssert.StartsWith(lines.Single(l => l.Contains("tower=wall")), "pass ");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] fill") && l.Contains("failed 2")));
    }

    [TestMethod]
    public void TheFormationsLine_IsWrittenAtEachFill_AndAtEachWaveStartWithPosts()
    {
        var rig = InWaveOne();
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] after the fill: archer_positions=")));
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Posts] at the wave start: archer_positions=")));

        var before = rig.Log.Lines.Count(l => l.Contains("[Posts] after the fill"));
        rig.ClearWave();
        Assert.IsTrue(rig.Log.Lines.Count(l => l.Contains("[Posts] after the fill")) > before, "the clear refills and writes it again");
    }

    [TestMethod]
    public void TheSecondLevel_FillsTheRestOfTheEightPosts_OnEachTower()
    {
        var rig = InWaveOne();
        rig.ClearWave();

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.Tick();

        CollectionAssert.AreEquivalent(new[] { 8, 8 }, rig.Field.Archers.Values.ToArray());
        Assert.AreEqual(FortResult.NotOffered, rig.Session.TryBuyTowerLevel(), "level 3, the tower volley, is Slice 8");
    }

    [TestMethod]
    public void ThePostsLine_OfWaveTwoAfterTheSecondLevel_NamesLevelTwoAndEightPosts()
    {
        var rig = InWaveOne();
        rig.ClearWave();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
        rig.Tick();
        rig.StartNextWave();
        rig.Ticks(35);
        rig.Field.TowerShots["hornburg"] = 20;
        rig.Field.TowerShots["wall"] = 20;

        rig.ClearWave();

        var second = rig.Posts().Where(l => l.StartsWith("pass hornburg-w2 ", System.StringComparison.Ordinal)).ToList();
        Assert.AreEqual(1, second.Count, string.Join("\n", rig.Posts()));
        StringAssert.Contains(second[0], "level=2");
        StringAssert.Contains(second[0], "expected=8 manned=8");
    }
}
