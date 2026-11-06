using System;
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
/// The mounted sally at the director (phase2-plan.md Slice 10, spike S9). No mounted class can be chosen in a real run before Slice 11, so everything here is driven through fakes: the points are
/// checked at boot, the lord is put out at each wave start (teleported when mounted, remounted in one step when he is on foot, left alone when his body is a soldier's), brought home at each clear,
/// and the lines and checks the spike reads are written.
/// </summary>
[TestClass]
public class LongDefeatDirectorSallyTests
{
    private static readonly MusterFrame SallyFrame = new(100f, 200f, 30f, 0f, 1f);
    private static readonly MusterFrame FootFrame = new(10f, 20f, 30f, 0f, 1f);

    private static LdLoadout Rider() => new("cavalry", true, new[] { new LdKitSlot("Horse", "horse_a"), new LdKitSlot("HorseHarness", "harness_a") });

    private static LdLoadout Archer() => new("archer", false, new[] { new LdKitSlot("Horse", "") });

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public FakeLongDefeatEffects Effects { get; }
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(LdLoadout? loadout, MusterFrame? sally, MusterFrame? foot = null, KeepPoint? keep = null, int stopAfter = 2)
        {
            Effects = new FakeLongDefeatEffects(Field.Calls);
            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 5f },
                new RunOptions { Autoplay = true, StopAfterWaves = stopAfter, Seed = 1UL, ClassId = loadout?.ClassId })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Loadout = loadout,
                Sally = sally,
                Muster = foot == null ? null : new MusterPoints(foot, null),
                Keep = keep ?? new KeepPoint(10f, 20f, 30f, 14f),
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, Effects);
        }

        public List<string> Calls => Field.Calls;

        public IEnumerable<string> Sally => Log.Lines.Where(l => l.Contains("[LongDefeat][Sally]"));

        public void Step()
        {
            Director.Tick(1f);
            Field.AttackersStanding = 0;
        }

        /// <summary>Ticks until the wave's first batch is out (the wave start and the sally out have been applied).</summary>
        public void ToTheWaveStart()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 200 && !Field.Calls.Contains("Refresh"); i++)
                Director.Tick(1f);
            Assert.IsTrue(Field.Calls.Contains("Refresh"), "the wave never started");
        }

        public void RunToTheEnd()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 4000 && Director.Result == null; i++)
                Step();
            Assert.IsNotNull(Director.Result, "the run never ended");
        }

        public List<string> Check(string id) => Outcomes.Of(id);
    }

    private static Rig MountedRig(int stopAfter = 2) => new(Rider(), SallyFrame, FootFrame, stopAfter: stopAfter);

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- the boot ----

    [TestMethod]
    public void Boot_ChecksTheSallyAndTheHomePoint_AndWritesOneLineForEach()
    {
        var rig = MountedRig();

        rig.Director.Start();

        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("point (100.0, 200.0, 30.0) navmesh yes boundary yes")), string.Join("\n", rig.Sally));
        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("home point (10.0, 20.0, 30.0) navmesh yes boundary yes")), string.Join("\n", rig.Sally));
        Assert.AreEqual(2, rig.Field.PointsAsked.Count);
    }

    [TestMethod]
    public void Boot_AFailingSallyPoint_SaysWhichPartFailed_AndCavalryRidesFromBeginWithNoSally()
    {
        var rig = MountedRig();
        rig.Field.PointFacts = place => place.X == 100f ? new LdPointFacts(false, true) : new LdPointFacts(true, true);

        rig.ToTheWaveStart();

        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("point (100.0, 200.0, 30.0) navmesh no boundary yes")), string.Join("\n", rig.Sally));
        Assert.IsTrue(rig.Sally.Any(l => l.Contains("Cavalry rides from Begin with no sally")), string.Join("\n", rig.Sally));
        Assert.AreEqual(0, rig.Field.Count("Teleport"));
        Assert.AreEqual(0, rig.Field.Count("Remount"));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyId).Single(), "reason=point-unusable");
        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SallyId));
    }

    [TestMethod]
    public void Boot_APointOutsideTheBoundary_IsNamedToo()
    {
        var rig = MountedRig();
        rig.Field.PointFacts = place => place.X == 100f ? new LdPointFacts(true, false) : new LdPointFacts(true, true);

        rig.Director.Start();

        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("navmesh yes boundary no")), string.Join("\n", rig.Sally));
    }

    [TestMethod]
    public void Boot_NoSallyBlock_LogsOneLine_AndRidesFromBegin()
    {
        var rig = new Rig(Rider(), null, FootFrame);

        rig.ToTheWaveStart();

        Assert.AreEqual(1, rig.Sally.Count(l => l.Contains("no sally point recorded")), string.Join("\n", rig.Sally));
        Assert.IsTrue(rig.Sally.Any(l => l.Contains("Cavalry rides from Begin with no sally")));
        Assert.AreEqual(1, rig.Field.PointsAsked.Count, "only the home point is checked");
        Assert.AreEqual(0, rig.Field.Count("Teleport"));
        Assert.AreEqual(0, rig.Field.Count("Remount"));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyId).Single(), "reason=no-sally-point");
    }

    [TestMethod]
    public void Boot_AFailingHomePoint_RefusesTheRun_BecauseTheMountedSpawnNeedsAPlaceToStandOn()
    {
        var rig = MountedRig();
        rig.Field.PointFacts = place => place.X == 10f ? new LdPointFacts(true, false) : new LdPointFacts(true, true);

        var started = rig.Director.Start();

        Assert.IsFalse(started, "no cavalry plan exists at boot, so a lord with no home place would spawn at a frame nothing proved");
        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("home point (10.0, 20.0, 30.0) navmesh yes boundary no")), string.Join("\n", rig.Sally));
        Assert.AreEqual(0, rig.Field.Loadouts.Count, "no lord was spawned");
        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.BootId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Check(LongDefeatChecks.BootId).Single(), "home point");
    }

    [TestMethod]
    public void Boot_WithNoMusterFootPoint_TheHomeIsTheKeepPoint()
    {
        var rig = new Rig(Rider(), SallyFrame, foot: null, keep: new KeepPoint(7f, 8f, 9f, 14f));

        rig.Director.Start();

        Assert.IsTrue(rig.Sally.Any(l => l.EndsWith("home point (7.0, 8.0, 9.0) navmesh yes boundary yes")), string.Join("\n", rig.Sally));
        Assert.AreEqual(new LdPoint(7f, 8f, 9f), rig.Field.Places[0]!.Point);
        Assert.IsFalse(rig.Field.Places[0]!.HasFacing, "a keep carries no facing");
    }

    [TestMethod]
    public void AFootClass_AsksAboutNoPoint_WritesNoSallyLine_AndSpawnsWithNoPlace()
    {
        var rig = new Rig(Archer(), SallyFrame, FootFrame);

        rig.RunToTheEnd();

        Assert.AreEqual(0, rig.Field.PointsAsked.Count);
        Assert.AreEqual(0, rig.Sally.Count());
        Assert.IsNull(rig.Field.Places[0]);
        Assert.AreEqual(0, rig.Field.Count("Teleport") + rig.Field.Count("Remount"));
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains("sally")));
    }

    [TestMethod]
    public void ARunWithNoClass_IsLeftAlone()
    {
        var rig = new Rig(null, SallyFrame, FootFrame);

        rig.RunToTheEnd();

        Assert.AreEqual(0, rig.Field.PointsAsked.Count);
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains("sally")));
    }

    // ---- the mounted spawn ----

    [TestMethod]
    public void TheMountedLord_SpawnsAtTheHomePlace_AndIsRefreshedAfterTheSpawn()
    {
        var rig = MountedRig();

        rig.Director.Start();

        Assert.AreEqual(new LdPlace(10f, 20f, 30f, 0f, 1f), rig.Field.Places[0]);
        Assert.IsTrue(rig.Field.Index("RefreshStats:Player") > rig.Field.Index("SpawnPlayer:False:cavalry"), "the horse was built before its rider: derive the lord's stats again after the spawn");
        Assert.AreEqual(1, rig.Calls.Count(c => c == "RefreshStats:Player"), "the new body's adoption refreshes him once; the spawn adds no second refresh");
    }

    [TestMethod]
    public void AMountedLord_IsNotInTheFootFormationsBatch_SoNoSlotIsReservedForHim()
    {
        var rider = MountedRig();
        var archer = new Rig(Archer(), SallyFrame, FootFrame);

        rider.Director.Start();
        archer.Director.Start();

        Assert.AreEqual(1, archer.Field.Count("Boot:31/"), "a foot class reserves one slot for the lord");
        Assert.AreEqual(1, rider.Field.Count("Boot:30/"), "a rider does not");
        Assert.AreEqual(PlacedFoot(archer), PlacedFoot(rider) + 1, "the foot formation's first batch is one larger when the lord joins it");
    }

    private static int PlacedFoot(Rig rig) =>
        int.Parse(rig.Field.Calls.First(c => c.StartsWith("Place:def:Infantry:", StringComparison.Ordinal)).Substring("Place:def:Infantry:".Length));

    // ---- going out ----

    [TestMethod]
    public void TheWaveStart_TeleportsAMountedLordToTheSallyPoint_BeforeTheFirstAttackerSpawns()
    {
        var rig = MountedRig();

        rig.ToTheWaveStart();

        Assert.AreEqual(1, rig.Field.Count("Teleport:100"), string.Join(",", rig.Calls));
        Assert.AreEqual(0, rig.Field.Count("Remount"));
        Assert.IsTrue(rig.Field.Index("Teleport:100") < rig.Calls.FindIndex(c => c.StartsWith("Spawn:att", StringComparison.Ordinal)), "out comes before the wave's attackers");
        Assert.AreEqual(new LdPlace(100f, 200f, 30f, 0f, 1f), rig.Field.Teleports[0]);
    }

    [TestMethod]
    public void TheOutLine_NamesHowHeWentOut_TheBodyTheHorseTheDistanceTheFormationAndItsOrder()
    {
        var rig = MountedRig();

        rig.ToTheWaveStart();

        var line = rig.Sally.Single(l => l.Contains("w1 out:"));
        StringAssert.Contains(line, "out: teleport main=lord_4_1 mount=yes at=(100.0, 200.0, 30.0) dist=0.0 formation=Cavalry order_at=(10.0, 20.0, 30.0)");
    }

    [TestMethod]
    public void TheSallyCheck_PassesAtTheStartOfTheWave_KeyedByTheWave()
    {
        var rig = MountedRig();

        rig.ToTheWaveStart();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyId));
        StringAssert.StartsWith(rig.Check(LongDefeatChecks.SallyId).Single(), "w1 w=1 how=teleport mount=yes dist=0.0 ");
    }

    [TestMethod]
    public void ALordWhoseHorseDied_IsRemountedAtThePoint_InOneStep_AndRefreshedAfterIt()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };

        rig.ToTheWaveStart();
        rig.Director.Tick(1f);

        Assert.AreEqual(0, rig.Field.Count("Teleport"));
        Assert.AreEqual(1, rig.Field.Count("Remount:100"), string.Join(",", rig.Calls));
        Assert.AreEqual("cavalry", rig.Field.Remounts[0].Loadout.ClassId);
        Assert.AreEqual(new LdPlace(100f, 200f, 30f, 0f, 1f), rig.Field.Remounts[0].Place);
        var remounted = rig.Calls.IndexOf("Remount:100");
        Assert.AreEqual(1, rig.Calls.Skip(remounted).Count(c => c == "RefreshStats:Player"), "the new body is adopted and refreshed once, a tick later: " + string.Join(",", rig.Calls.Skip(remounted)));
        Assert.IsTrue(rig.Calls.LastIndexOf("RefreshStats:Player") > remounted, "the new body's horse was built before its rider");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyId), string.Join("\n", rig.Outcomes.Lines));
        var line = rig.Sally.Single(l => l.Contains("w1 out:"));
        StringAssert.Contains(line, "out: remount main=lord_4_1 mount=yes");
        StringAssert.Contains(line, "old_state=Routed health_share=0.50 main_after=yes deaths=0->0");
    }

    [TestMethod]
    public void ARemount_CountsNoPlayerDeath_AndNoRespawnIsAskedFor()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };

        rig.ToTheWaveStart();

        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"));
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.PlayerDeathId)));
    }

    [TestMethod]
    public void ARemountWhoseOldBodyIsReportedKilled_FailsTheSallyCheck_AndSaysSo()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };
        rig.Field.RemountResult = new LdRemount(true, "Killed", true, 0.5f, "");

        rig.ToTheWaveStart();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.SallyId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Sally.Any(l => l.Contains("old_state=Killed")));
    }

    [TestMethod]
    public void ARemountThatFails_FailsTheCheck_AndTheWaveGoesOn()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };
        rig.Field.RemountResult = new LdRemount(false, "", false, 0f, "no own body");

        rig.ToTheWaveStart();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.SallyId));
        Assert.IsTrue(rig.Sally.Any(l => l.Contains("no own body")), string.Join("\n", rig.Sally));
        Assert.IsTrue(rig.Calls.Contains("Refresh"), "the wave's attackers still came");
    }

    [TestMethod]
    public void ATeleportTheFieldRefused_FailsTheCheck()
    {
        var rig = MountedRig();
        rig.Field.TeleportOk = false;

        rig.ToTheWaveStart();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.SallyId));
    }

    [TestMethod]
    public void ASoldiersBodyTheLordTookOver_GetsNoSally_ASkipLineSaysWhy()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { Own = false, HasMount = false };

        rig.ToTheWaveStart();

        Assert.AreEqual(0, rig.Field.Count("Teleport") + rig.Field.Count("Remount"));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyId).Single(), "reason=taken-over");
        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SallyId));
    }

    [TestMethod]
    public void WithNoLordStanding_ThereIsNoSally()
    {
        var rig = MountedRig();
        rig.Field.Lord = LdLordFacts.None;

        rig.ToTheWaveStart();

        Assert.AreEqual(0, rig.Field.Count("Teleport") + rig.Field.Count("Remount"));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyId).Single(), "reason=no-lord");
    }

    // ---- coming home ----

    [TestMethod]
    public void AtTheClear_ALivingLordIsTeleportedHome_AndSallyReturnPasses()
    {
        var rig = MountedRig(stopAfter: 1);

        rig.RunToTheEnd();

        Assert.AreEqual(1, rig.Field.Teleports.Count(t => t.X == 10f), string.Join(",", rig.Calls));
        StringAssert.Contains(rig.Sally.Single(l => l.Contains("w1 home:")), "home: teleport main=lord_4_1 at=(10.0, 20.0, 30.0) dist=0.0");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyReturnId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(rig.Check(LongDefeatChecks.SallyReturnId).Single(), "w1 w=1 dist=0.0 ");
    }

    [TestMethod]
    public void EveryWaveGoesOutAndComesHome_UnderItsOwnKey()
    {
        var rig = MountedRig(stopAfter: 2);

        rig.RunToTheEnd();

        CollectionAssert.AreEqual(new[] { "w1", "w2" }, rig.Check(LongDefeatChecks.SallyId).Select(l => l.Split(' ')[0]).ToArray());
        CollectionAssert.AreEqual(new[] { "w1", "w2" }, rig.Check(LongDefeatChecks.SallyReturnId).Select(l => l.Split(' ')[0]).ToArray());
        Assert.AreEqual(2, rig.Field.Teleports.Count(t => t.X == 100f));
        Assert.AreEqual(2, rig.Field.Teleports.Count(t => t.X == 10f));
    }

    [TestMethod]
    public void NoHomeTeleport_WhenNoSallyWentOut_ThatWave()
    {
        var rig = new Rig(Rider(), null, FootFrame, stopAfter: 1);

        rig.RunToTheEnd();

        Assert.AreEqual(0, rig.Field.Teleports.Count);
        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.SallyReturnId)));
    }

    [TestMethod]
    public void AtTheClear_ALordWhoseBodyIsNotHisOwn_IsNotTeleported_AndTheReturnIsASkip()
    {
        var rig = MountedRig(stopAfter: 1);
        rig.ToTheWaveStart();
        rig.Field.Lord = rig.Field.Lord with { Own = false };

        for (var i = 0; i < 4000 && rig.Director.Result == null; i++)
            rig.Step();

        Assert.AreEqual(0, rig.Field.Teleports.Count(t => t.X == 10f));
        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SallyReturnId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void AtTheClear_ALordTooFarFromHome_FailsTheReturn()
    {
        var rig = MountedRig(stopAfter: 1);
        rig.ToTheWaveStart();
        rig.Field.TeleportOk = true;
        rig.Field.Lord = rig.Field.Lord with { At = new LdPoint(500f, 500f, 30f) };

        // The teleport that brings him home does not take, as a native refusal would not.
        rig.Field.TeleportOk = false;
        for (var i = 0; i < 4000 && rig.Director.Result == null; i++)
            rig.Step();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.SallyReturnId), string.Join("\n", rig.Outcomes.Lines));
    }

    // ---- hits ----

    [TestMethod]
    public void ASallyWave_WantsHits_UntilTheClear()
    {
        var rig = MountedRig(stopAfter: 1);

        Assert.IsFalse(rig.Director.WantsHits);
        rig.ToTheWaveStart();
        Assert.IsTrue(rig.Director.WantsHits);

        for (var i = 0; i < 4000 && rig.Director.Result == null; i++)
            rig.Step();

        Assert.IsFalse(rig.Director.WantsHits);
    }

    [TestMethod]
    public void AWaveWithNoSally_DoesNotWantHits()
    {
        var rig = new Rig(Rider(), null, FootFrame);

        rig.ToTheWaveStart();

        Assert.IsFalse(rig.Director.WantsHits);
    }

    [TestMethod]
    public void ASallyWaveThatIsNotABladesWave_DoesNotResolveHitKeys()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();

        rig.Director.OnHit("D:man", "A:orc");

        Assert.AreEqual(0, rig.Field.HitAsks, "the poison path is for a blades wave");
    }

    [TestMethod]
    public void AHitByTheLord_NearTheSallyPoint_IsContact_AndTheCheckPasses()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();

        rig.Director.OnContactHit("A:orc", "P", 40f);

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyContactId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyContactId).Single(), "hits_given=1 hits_taken=0 mount_hits=0 kills=0");
    }

    [TestMethod]
    public void AHitOnTheHorse_IsCountedInItsOwnField()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();

        rig.Director.OnContactHit("M", "A:orc", 40f);

        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyContactId).Single(), "hits_given=0 hits_taken=0 mount_hits=1");
    }

    [TestMethod]
    public void AHitBetweenStrangers_IsNotContact()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();

        rig.Director.OnContactHit("D:man", "A:orc", 40f);
        rig.Director.OnContactHit("A:orc", "D:man", 40f);

        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.SallyContactId)));
    }

    [TestMethod]
    public void AHit_WhenNoSallyIsOut_IsIgnored()
    {
        var rig = MountedRig();
        rig.Director.Start();

        rig.Director.OnContactHit("A:orc", "P", 40f);

        Assert.IsFalse(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.SallyContactId)));
    }

    [TestMethod]
    public void AHitFarFromThePoint_IsNotContact()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Field.Lord = rig.Field.Lord with { At = new LdPoint(300f, 300f, 30f) };
        rig.Director.Tick(1f);

        rig.Director.OnContactHit("A:orc", "P", 40f);

        Assert.IsFalse(rig.Outcomes.Has("pass", LongDefeatChecks.SallyContactId));
    }

    [TestMethod]
    public void AWaveWithNoContact_WritesASkip_AtTheClear_NeverAFail()
    {
        var rig = MountedRig(stopAfter: 1);

        rig.RunToTheEnd();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SallyContactId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.SallyContactId));
        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyContactId).Single(), "reason=none-before-clear");
    }

    [TestMethod]
    public void A180SecondWaveWithNoContact_WritesTheSkipAt180Seconds()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();

        rig.Director.Tick(181f);

        StringAssert.Contains(rig.Check(LongDefeatChecks.SallyContactId).Single(), "reason=none-in-180s");
    }

    // ---- the lines every five seconds ----

    [TestMethod]
    public void WhileMoreThan20mOutsideTheKeep_AFiveSecondLineCarriesTheCountsAndTheDistances()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Field.Lord = rig.Field.Lord with { At = new LdPoint(100f, 200f, 30f), NearestAttacker = 12f, ToFormation = 88f, Gate = "closed" };

        rig.Director.Tick(5f);

        var line = rig.Sally.Last(l => l.Contains("t="));
        StringAssert.Contains(line, "w1 t=");
        StringAssert.Contains(line, "hits_given=0 hits_taken=0 mount_hits=0 kills=0 nearest_attacker=12.0 to_formation=88.0 gate=closed outside_boundary=0.0");
    }

    [TestMethod]
    public void WithinTheTwentyMetresOfTheKeep_NoFiveSecondLineIsWritten()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Field.Lord = rig.Field.Lord with { At = new LdPoint(12f, 22f, 30f) };

        rig.Director.Tick(5f);
        rig.Director.Tick(5f);

        Assert.IsFalse(rig.Sally.Any(l => l.Contains(" t=")), string.Join("\n", rig.Sally));
    }

    [TestMethod]
    public void TheSecondsOutsideTheBoundary_AreOnTheLine()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Field.Lord = rig.Field.Lord with { Inside = false };

        rig.Director.Tick(5f);

        StringAssert.Contains(rig.Sally.Last(l => l.Contains(" t=")), "outside_boundary=5.0");
    }

    // ---- the old body ----

    [TestMethod]
    public void TheCallbacksThatReachTheLordsBodies_AreLoggedWithTheirState()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Field.PendingBodyEvents.Add(new LdBodyEvent("removed", "Routed", true, false));
        rig.Field.PendingBodyEvents.Add(new LdBodyEvent("deleted", "Deleted", true, false));

        rig.Director.Tick(1f);

        Assert.IsTrue(rig.Sally.Any(l => l.Contains("old body removed state=Routed")), string.Join("\n", rig.Sally));
        Assert.IsTrue(rig.Sally.Any(l => l.Contains("old body deleted state=Deleted")), string.Join("\n", rig.Sally));
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.SallyId));
    }

    [TestMethod]
    public void ALateKilledReportOnThePlannedBody_FailsTheSallyCheck_OfTheWaveItWasPlannedIn()
    {
        var rig = MountedRig();
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };
        rig.ToTheWaveStart();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyId));

        rig.Field.PendingBodyEvents.Add(new LdBodyEvent("removed", "Killed", true, true));
        rig.Director.Tick(1f);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.SallyId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.SallyId + " w1 ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ALordsOwnRealDeath_IsStillACountedDeath()
    {
        var rig = MountedRig(stopAfter: 3);
        rig.ToTheWaveStart();

        rig.Director.OnAgentRemoved("P", true);

        rig.Field.MainBody = 0;
        rig.Director.Tick(1f);
        rig.Field.MainBody = 2;
        rig.Director.Tick(1f);
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.PlayerDeathId)), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void TheBodyEventsAreNotReadAtAll_ForAFootClass()
    {
        var rig = new Rig(Archer(), SallyFrame, FootFrame);
        rig.Director.Start();
        rig.Field.PendingBodyEvents.Add(new LdBodyEvent("removed", "Routed", false, false));

        rig.Director.Tick(1f);

        Assert.AreEqual(1, rig.Field.PendingBodyEvents.Count, "nothing is taken for a class that never plans a removal");
    }

    [TestMethod]
    public void ARunThatEndsInASallyWave_JudgesTheContactAsASkip_AndWantsNoMoreHits()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        Assert.IsTrue(rig.Director.WantsHits);

        rig.Director.Shutdown();
        rig.Director.OnContactHit("A:orc", "P", 40f);

        Assert.IsFalse(rig.Director.WantsHits);
        var contact = rig.Check(LongDefeatChecks.SallyContactId).Single();
        StringAssert.Contains(contact, "reason=run-ended");
        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.SallyContactId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsFalse(rig.Outcomes.Has("pass", LongDefeatChecks.SallyContactId), "a contact after the report is written is no verdict");
    }

    [TestMethod]
    public void ARunThatEndsAfterTheContactWasJudged_WritesNoSecondContactLine()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        rig.Director.OnContactHit("A:orc", "P", 40f);

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Check(LongDefeatChecks.SallyContactId).Count);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.SallyContactId));
    }

    [TestMethod]
    public void TheShutdown_EndsTheWantForHits()
    {
        var rig = MountedRig();
        rig.ToTheWaveStart();
        Assert.IsTrue(rig.Director.WantsHits);

        rig.Director.Shutdown();

        Assert.IsFalse(rig.Director.WantsHits);
    }
}
