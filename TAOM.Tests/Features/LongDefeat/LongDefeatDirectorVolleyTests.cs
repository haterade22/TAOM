using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Phase 3 Slice 6, the Elven Volley at the director (phase3-plan.md D22 to D25, D30): an order is answered inside <c>Apply</c> with exactly one of fired or withheld, the aim reads the field and the shots go
/// through the adapter last in the tick, the hit window opens when the volley fires and its <c>volley</c> check is written when it closes, the bot arms the volley in a wave and aims a crosshair upgrade from
/// the lord's eye toward the densest group, and a recorded origin the boot check finds blocked is dropped.
/// </summary>
[TestClass]
public class LongDefeatDirectorVolleyTests
{
    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f,
        new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto), new VolleyLevelPlan(300, 16, 40f, 8f, 90f, VolleyAimMode.Crosshair) });

    private static readonly LdPoint[] Origins = { new(0f, 0f, 0f), new(10f, 0f, 0f) };

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

        public List<string> Of(string id) => Outcomes.Of(id);

        public void ClearWave()
        {
            for (var round = 0; round < 20 && Session.GetView()!.Phase == RunPhase.Wave; round++)
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

        /// <summary>Arms the volley as a person would and ticks once; the order is answered inside that tick.</summary>
        public void Fire()
        {
            Assert.AreEqual(VolleyArm.Armed, Session.TryVolley());
            Tick();
        }

        public static LdVolleyHit Foe(bool kill = false) => new("ld_elven_arrow", LdVolleyHitKind.Foe, kill, false, false, 45f, 30f);
    }

    private static Rig NewRig(bool autoplay = false, int? startGold = null, IReadOnlyList<LdPoint>? origins = null, FortOffer? offered = null, bool withOrigins = true, bool fort = true, int waveSize = 110)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(waveSize, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(1200, new[]
            {
                new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
                new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
            }, new FortPlan(null, null, null, Volley)),
            RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 0.5f },
            new RunOptions { Autoplay = autoplay, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? FortOffer.Offered,
            Volley = withOrigins ? new LdVolleyOrigins(origins ?? Origins) : null,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, null, fort ? rig.Field : null);
        return rig;
    }

    /// <summary>A run with the volley's first level, in wave 1, thirty attackers in reach.</summary>
    private static Rig InWaveOne(bool autoplay = false, int? startGold = null, int attackers = 30, int waveSize = 110)
    {
        var rig = NewRig(autoplay, startGold, waveSize: waveSize);
        rig.Field.PlaceAttackers(attackers);
        Assert.IsTrue(rig.Director.Start());
        if (!autoplay)
            Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();


    // ---- firing ----

    [TestMethod]
    public void AnArmedVolley_IsFiredOnTheNextTick_NotOnTheKey_OneArrowAtEachOfTwelveAttackers()
    {
        var rig = InWaveOne();

        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
        Assert.AreEqual(0, rig.Field.Volleys.Count, "arming never touches the engine by itself");
        rig.Tick();

        Assert.AreEqual(1, rig.Field.Volleys.Count);
        var (item, shots, tower) = rig.Field.Volleys[0];
        Assert.AreEqual("ld_elven_arrow", item);
        Assert.AreEqual(12, shots.Count);
        Assert.AreEqual(string.Empty, tower, "an Elven Volley comes from the wall, owned by no tower");
        Assert.AreEqual(12, shots.Select(s => s.Target).Distinct().Count(), "one distinct attacker per arrow while they last");
        Assert.IsTrue(shots.All(s => s.Origin.Z == 3f && s.Speed == 45f), "raised by the origin height, at the item's speed");
        Assert.AreEqual((90f, 3f), (rig.Field.TargetReads[0].Range, rig.Field.TargetReads[0].Height));
    }

    [TestMethod]
    public void AfterTheFire_TheCooldownRuns_AndTheNextKeyIsRefusedUntilItEnds()
    {
        var rig = InWaveOne();
        rig.Fire();

        Assert.IsFalse(rig.Session.GetView()!.Volleys.Ready);
        Assert.AreEqual(VolleyArm.Cooldown, rig.Session.TryVolley());
        Assert.IsTrue(rig.Session.GetView()!.Volleys.SecondsLeft > 40f);

        rig.Ticks(46);

        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
    }

    [TestMethod]
    public void TheFire_IsLastInTheTick_AfterTheBatchesTheTickSpawned()
    {
        // 400 attackers against a live cap of 150: the wave keeps spawning as they fall.
        var rig = InWaveOne(waveSize: 400);
        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
        for (var i = 0; i < 20; i++)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        var before = rig.Field.Calls.Count;
        rig.Tick();

        var added = rig.Field.Calls.Skip(before).ToList();
        var fire = added.FindIndex(c => c.StartsWith("FireVolley:"));
        Assert.IsTrue(added.Any(c => c.StartsWith("Spawn:att")), "the tick spawned attackers, so the order of the calls is a test: " + string.Join(",", added));
        Assert.IsTrue(fire > added.FindLastIndex(c => c.StartsWith("Spawn:") || c.StartsWith("End:")), "the volley reads the attackers this tick's batches spawned: " + string.Join(",", added));
    }

    [TestMethod]
    public void NoAttackerInReach_WithholdsTheShot_KeepsItReady_AndAsksAgainAfterASecond()
    {
        var rig = InWaveOne(attackers: 0);

        rig.Fire();

        Assert.AreEqual(0, rig.Field.Volleys.Count);
        Assert.AreEqual(0, rig.Field.Count("FireVolley"));
        var view = rig.Session.GetView()!.Volleys;
        Assert.AreEqual(VolleyWithheldReason.NoTargets, view.Withheld);
        Assert.IsFalse(view.Ready, "a withheld shot holds the next ask for a second");
        rig.Tick();
        rig.Tick();
        Assert.IsTrue(rig.Session.GetView()!.Volleys.Ready, "the cooldown was never spent");
        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
    }

    [TestMethod]
    public void NoOwner_AndNoItem_AreWithheldToo_EachWithItsReason()
    {
        var rig = InWaveOne();
        rig.Field.FireRefusal = VolleyWithheldReason.NoOwner;
        rig.Fire();
        Assert.AreEqual(VolleyWithheldReason.NoOwner, rig.Session.GetView()!.Volleys.Withheld);

        var missing = InWaveOne();
        missing.Field.VolleyItemResult = null;
        missing.Fire();
        Assert.AreEqual(VolleyWithheldReason.NoItem, missing.Session.GetView()!.Volleys.Withheld);
        Assert.AreEqual(0, missing.Field.Count("FireVolley"), "an item the game did not load is never fired");
    }

    [TestMethod]
    public void EveryOrder_GetsOneAnswer_SoAShotThatFiredNeverOrdersAgainOnTheNextTick()
    {
        var rig = InWaveOne();
        rig.Fire();
        rig.Ticks(3);

        Assert.AreEqual(1, rig.Field.Volleys.Count, "one press, one volley");
        Assert.AreEqual(1, rig.Field.Count("VolleyTargets"));
    }

    [TestMethod]
    public void TheFireIsBooked_UsesAndArrows_AndTheNumberOfTheNextUseGoesUp()
    {
        var rig = InWaveOne();
        rig.Fire();
        rig.Ticks(60);
        rig.Fire();

        Assert.AreEqual(2, rig.Field.Volleys.Count);
        var lines = rig.Log.Lines.Where(l => l.Contains("[LongDefeat][Volley]")).ToList();
        Assert.IsTrue(lines.Any(l => l.Contains("w1 use1 elven")), string.Join("\n", lines));
        Assert.IsTrue(lines.Any(l => l.Contains("w1 use2 elven")), string.Join("\n", lines));
    }

    [TestMethod]
    public void WithNoFortAdapter_AnOrderIsStillAnswered_AsWithheld_NotLeftHanging()
    {
        var rig = NewRig(fort: false);
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));

        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
        rig.Tick();

        Assert.IsNotNull(rig.Session.GetView()!.Volleys.Withheld);
        Assert.IsFalse(rig.Session.GetView()!.Volleys.Ready);
    }

    // ---- the aim ----

    [TestMethod]
    public void ACrosshairVolley_AimedByAPerson_CastsTheEyeRay_AndLabelsItsLineAimPerson()
    {
        var rig = InWaveOne();
        rig.Field.RayHitAt = (origin, direction, range) => new Vec3f(origin.X + direction.X * 40f, origin.Y + direction.Y * 40f, 0f);
        rig.ClearWave();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.StartNextWave();

        rig.Fire();

        Assert.AreEqual(1, rig.Field.Volleys.Count);
        Assert.AreEqual(16, rig.Field.Volleys[0].Shots.Count);
        var ray = rig.Field.Rays.Single(r => r.Range > 2f);
        Assert.AreEqual(rig.Field.Eye!.Value.Origin, ray.Origin, "from the eye");
        Assert.AreEqual(rig.Field.Eye!.Value.Direction, ray.Direction, "along the look direction");
        Assert.AreEqual(97f, ray.Range, 1e-3f, "90 m of range plus 7 m from the eye (0, 0, 10) to the nearest raised origin (0, 0, 3)");
        rig.Ticks(7);
        StringAssert.Contains(rig.Of(LongDefeatChecks.VolleyId).Single(), "mode=crosshair aim=person");
    }

    [TestMethod]
    public void TheCrosshairRay_IsLongEnoughToReachAnythingInReachOfTheWalls_WhereverTheLordStands()
    {
        // The targets are in range of the raised origins, so a ray measured from the eye must run the range plus the eye's distance to the nearest origin, or the volley is withheld for a lord who stands far back.
        var rig = InWaveOne();
        rig.Field.RayHitAt = (origin, direction, range) => new Vec3f(origin.X + direction.X * 40f, origin.Y + direction.Y * 40f, 0f);
        rig.ClearWave();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.StartNextWave();
        rig.Field.Eye = new LdVolleyRay(new Vec3f(-100f, 0f, 10f), new Vec3f(1f, 0f, 0f));

        rig.Fire();

        var ray = rig.Field.Rays.Single(r => r.Range > 2f);
        var toNearestOrigin = (float)System.Math.Sqrt(100f * 100f + 7f * 7f);
        Assert.AreEqual(90f + toNearestOrigin, ray.Range, 1e-2f);
    }

    [TestMethod]
    public void ACrosshairVolley_WithNoLord_OrAnEmptySky_IsWithheldForNoTargets()
    {
        var rig = InWaveOne();
        rig.ClearWave();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.StartNextWave();
        rig.Field.Eye = null;

        rig.Fire();
        Assert.AreEqual(VolleyWithheldReason.NoTargets, rig.Session.GetView()!.Volleys.Withheld);

        rig.Field.Eye = new LdVolleyRay(new Vec3f(0f, 0f, 10f), new Vec3f(1f, 0f, 0f));
        rig.Ticks(2);
        rig.Fire();
        Assert.AreEqual(0, rig.Field.Volleys.Count, "the ray found nothing within range");
        Assert.AreEqual(VolleyWithheldReason.NoTargets, rig.Session.GetView()!.Volleys.Withheld);
    }

    // ---- the bot ----

    [TestMethod]
    public void TheBot_ArmsTheVolleyInAWave_OncePerReadiness_AndLevelOneAimsItself()
    {
        var rig = InWaveOne(autoplay: true, startGold: 5000);

        rig.Ticks(3);

        Assert.AreEqual(1, rig.Field.Volleys.Count, "armed once, fired once, then the cooldown");
        Assert.AreEqual(0, rig.Field.Rays.Count(r => r.Range > 2f), "level one needs no aim ray (the boot cast its clearance rays, two metres long)");
        rig.Ticks(7);
        StringAssert.Contains(rig.Of(LongDefeatChecks.VolleyId).Single(), "mode=auto");
    }

    [TestMethod]
    public void TheBot_AtLevelTwo_AimsTheCrosshairFromTheLordsEyeTowardTheDensestGroup_AndLabelsItAimBot()
    {
        var rig = InWaveOne(autoplay: true, startGold: 5000);
        rig.Field.RayHitAt = (origin, direction, range) => new Vec3f(origin.X + direction.X * 50f, origin.Y + direction.Y * 50f, origin.Z + direction.Z * 50f);
        rig.ClearWave();
        rig.PumpUntil(() => rig.Session.GetView()!.Fort.Volley.Level == 2, 100);
        rig.StartNextWave();

        rig.Ticks(3);

        Assert.IsTrue(rig.Field.Volleys.Count >= 2, "wave one's volley and wave two's");
        Assert.AreEqual(16, rig.Field.Volleys.Last().Shots.Count);
        var ray = rig.Field.Rays.Single(r => r.Range > 2f);
        Assert.AreEqual(rig.Field.Eye!.Value.Origin, ray.Origin, "the bot's ray leaves the lord's eye");
        Assert.IsTrue(ray.Direction.X > 0.9f && System.Math.Abs(ray.Direction.Y) < 0.1f, "toward the attackers, who stand along +x of the wall: " + ray.Direction);
        rig.Ticks(7);
        StringAssert.Contains(rig.Of(LongDefeatChecks.VolleyId).Last(), "mode=crosshair aim=bot");
    }

    // ---- the hits and the check ----

    [TestMethod]
    public void TheHitWindow_IsOpenOnlyWhileAVolleysArrowsFly_AndAHitOutsideItIsDropped()
    {
        var rig = InWaveOne();
        Assert.IsFalse(rig.Director.WantsVolleyHits);
        rig.Director.OnVolleyHit(Rig.Foe());
        rig.Fire();
        Assert.IsTrue(rig.Director.WantsVolleyHits);

        rig.Ticks(7);

        Assert.IsFalse(rig.Director.WantsVolleyHits, "closed after six seconds");
        rig.Director.OnVolleyHit(Rig.Foe());
        rig.Tick();
        Assert.AreEqual(1, rig.Of(LongDefeatChecks.VolleyId).Count);
        StringAssert.Contains(rig.Of(LongDefeatChecks.VolleyId)[0], " hits=0 ");
    }

    [TestMethod]
    public void TheVolleyItemsAreKnownByIdToTheLogic_AndNothingElseIs()
    {
        var rig = NewRig();

        Assert.IsTrue(rig.Director.IsVolleyItem("ld_elven_arrow"));
        Assert.IsFalse(rig.Director.IsVolleyItem("wm_elven_arrow_q1v3"));
        Assert.IsFalse(rig.Director.IsVolleyItem(""));
    }

    [TestMethod]
    public void EnoughHits_PassTheCheck_ItsKeyNamesTheSourceWaveAndUse_AndTheRunBooksHitsAndKills()
    {
        var rig = InWaveOne();
        rig.Fire();
        for (var i = 0; i < 8; i++)
            rig.Director.OnVolleyHit(Rig.Foe(kill: i < 3));
        rig.Ticks(7);

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.VolleyId), string.Join("\n", rig.Outcomes.Lines));
        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("pass " + LongDefeatChecks.VolleyId));
        StringAssert.StartsWith(line, "pass " + LongDefeatChecks.VolleyId + " elven-w1u1 w=1 use=1 source=elven mode=auto arrows=12 shots=12 candidates=27 distinct=12 spawned=0 hits=8 ");
        StringAssert.Contains(line, "kills=3");
        StringAssert.Contains(line, "owner=fow_rohan_eastfold_bowman mult=1.00 base_speed=45.0 impact_speed=30.0 window=6.0");
        rig.Field.AttackersStanding = 0;
        rig.ClearWave();
        var row = rig.Session.GetView();
        Assert.IsNotNull(row);
    }

    [TestMethod]
    public void TooFewHits_FailTheCheck()
    {
        var rig = InWaveOne();
        rig.Fire();
        rig.Director.OnVolleyHit(Rig.Foe());
        rig.Ticks(7);

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.VolleyId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void AFriendlyHit_ThatWasNotCancelled_FailsTheCheck_AndACancelledOneIsCounted()
    {
        var rig = InWaveOne();
        rig.Fire();
        for (var i = 0; i < 8; i++)
            rig.Director.OnVolleyHit(Rig.Foe());
        rig.Director.OnVolleyHit(new LdVolleyHit("ld_elven_arrow", LdVolleyHitKind.Cancelled, false, false, false, 0f, 0f));
        rig.Director.OnVolleyHit(new LdVolleyHit("ld_elven_arrow", LdVolleyHitKind.Friendly, false, false, false, 45f, 30f));
        rig.Ticks(7);

        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("fail " + LongDefeatChecks.VolleyId));
        StringAssert.Contains(line, "friendly=1 cancelled=1");
    }

    [TestMethod]
    public void AFireThatSpawnedAnAgent_FailsTheCheck()
    {
        var rig = InWaveOne();
        rig.Field.AgentsSpawnedByFire = 1;
        rig.Fire();
        for (var i = 0; i < 12; i++)
            rig.Director.OnVolleyHit(Rig.Foe());
        rig.Ticks(7);

        var line = rig.Outcomes.Lines.Single(l => l.StartsWith("fail " + LongDefeatChecks.VolleyId));
        StringAssert.Contains(line, "spawned=1");
    }

    [TestMethod]
    public void AHitReportedOffTheMainThread_IsParkedAndBooked_NotLost()
    {
        var rig = InWaveOne();
        rig.Fire();

        Task.Run(() =>
        {
            for (var i = 0; i < 8; i++)
                rig.Director.OnVolleyHit(Rig.Foe());
        }).Wait();
        rig.Ticks(7);

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.VolleyId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void TheClear_CutsAnOpenWindow_AndWritesItsLine_EndedWave()
    {
        var rig = InWaveOne();
        rig.Fire();
        for (var i = 0; i < 8; i++)
            rig.Director.OnVolleyHit(Rig.Foe());

        rig.ClearWave();

        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatChecks.VolleyId));
        StringAssert.Contains(line, "ended=wave");
        Assert.IsFalse(rig.Director.WantsVolleyHits);
    }

    [TestMethod]
    public void ARunThatEndsInsideTheWindow_FlushesItsLine_EndedRun_AndLogsTheWavesCounts()
    {
        var rig = InWaveOne();
        rig.Fire();
        for (var i = 0; i < 7; i++)
            rig.Director.OnVolleyHit(Rig.Foe(kill: true));
        rig.Tick();

        rig.Director.Shutdown();

        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatChecks.VolleyId));
        StringAssert.Contains(line, "ended=run");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Volley]") && l.Contains("run ended in wave 1") && l.Contains("hits=7") && l.Contains("kills=7")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Volley"))));
    }

    // ---- the boot: origins ----

    [TestMethod]
    public void AnOriginWhoseRayHitsTheParapetWithinTwoMetres_IsDropped_NamedAndFailsTheFortPointsCheck()
    {
        var rig = NewRig();
        rig.Field.RayHitAt = (origin, direction, range) => origin.X > 5f ? new Vec3f(origin.X, origin.Y, origin.Z) : null;

        Assert.IsTrue(rig.Director.Start(), "a bad origin never refuses the boot");

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.FortPointsId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Of(LongDefeatChecks.FortPointsId)[0], "bad=volley[1](navmesh=yes boundary=yes clear=no)");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("WARN ") && l.Contains("volley[1]") && l.Contains("clear") && l.Contains("is dropped")), string.Join("\n", rig.Log.Lines));
        Assert.IsTrue(rig.Field.Rays.All(r => r.Range == 2f), "the clearance ray is two metres long");
        Assert.AreEqual(3f, rig.Field.Rays[0].Origin.Z, "from the raised origin");

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.Field.PlaceAttackers(30);
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Fire();
        Assert.IsTrue(rig.Field.TargetReads[0].Origins.SequenceEqual(new[] { Origins[0] }), "the fire uses the origins that passed");
    }

    [TestMethod]
    public void WhenEveryOriginIsBlocked_TheVolleyRowIsHidden_AndNothingIsSoldForIt()
    {
        var rig = NewRig();
        rig.Field.RayHitAt = (origin, direction, range) => new Vec3f(origin.X, origin.Y, origin.Z);

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(FortResult.Hidden, rig.Session.TryBuyVolleyLevel());
    }

    [TestMethod]
    public void AnOriginThatFailsTheNavmeshTest_IsDropped_Too()
    {
        var rig = NewRig();
        rig.Field.FortPointFacts = place => new LdPointFacts(place.X != 10f, true);

        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        rig.Field.PlaceAttackers(30);
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Fire();

        Assert.IsTrue(rig.Field.TargetReads[0].Origins.SequenceEqual(new[] { Origins[0] }));
    }

    [TestMethod]
    public void AnArrowTheGameDidNotLoad_HidesTheVolleyRow_AtTheBoot_WithOneWarning()
    {
        var rig = NewRig();
        rig.Field.VolleyItemResult = null;

        Assert.IsTrue(rig.Director.Start(), "a missing arrow never refuses the boot");

        Assert.AreEqual(FortResult.Hidden, rig.Session.TryBuyVolleyLevel(), "nobody pays for a volley that cannot fire");
        Assert.AreEqual(1, rig.Log.Lines.Count(l => l.StartsWith("WARN ") && l.Contains("ld_elven_arrow")), string.Join(" | ", rig.Log.Lines));
        CollectionAssert.AreEqual(new[] { 90f }, rig.Field.ItemRangesAsked, "asked once, with the widest range of its levels");
    }

    [TestMethod]
    public void AnArrowThatLoaded_IsLookedUpAtTheBoot_SoItsReachLineIsInTheLogBeforeTheFirstVolley()
    {
        var rig = NewRig();

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(1, rig.Field.Count("VolleyItem:ld_elven_arrow"));
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
    }

    [TestMethod]
    public void AFortressWithNoRecordedOrigin_SellsNoVolley_AndFiresNothing_ThroughAWholeWave()
    {
        var rig = NewRig(withOrigins: false);
        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(FortResult.Hidden, rig.Session.TryBuyVolleyLevel());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        Assert.AreEqual(VolleyArm.NotSold, rig.Session.TryVolley(), "Helm's Deep records no origin: the shop shows no row, so the key has nothing to say");
        rig.Ticks(5);
        Assert.AreEqual(0, rig.Field.Count("FireVolley") + rig.Field.Count("VolleyItem") + rig.Field.Count("VolleyTargets"));
        Assert.AreEqual(0, rig.Of(LongDefeatChecks.VolleyId).Count);
    }

    [TestMethod]
    public void AnOriginIsKeptWhenNoKeepIsKnown_NoDirectionToCastTheClearanceRay()
    {
        var rig = NewRig();
        rig.Field.Keep = null;

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(0, rig.Field.Rays.Count);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FortPointsId), string.Join("\n", rig.Outcomes.Lines));
    }
}
