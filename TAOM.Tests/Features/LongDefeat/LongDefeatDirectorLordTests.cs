using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The death rule's engine half at the director (phase4-plan.md Slice 4, spike S12): the rise at a clear through the lord adapter, the general's seat written on the tick after a fall, the bot's take-over, the
/// forced fall and the facts the removal carries, and the checks they write. A director with no lord adapter keeps the old respawn, which <c>LongDefeatDirectorTests</c> covers.
/// </summary>
[TestClass]
public class LongDefeatDirectorLordTests
{
    private static readonly KeepPoint RecordedKeep = new(138f, 159f, 32f, 20f);

    private static LdLoadout Cavalry() => new("cavalry", true, new[] { new LdKitSlot("Horse", "horse_a") });

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(bool autoplay = false, KeepPoint? keep = null, LdLoadout? loadout = null, int? lordFalls = null, bool lordAdapter = true, bool fort = false, int stopAfter = 4, bool field = false)
        {
            if (field)
                Field.Starts = new FieldPlaces(new MusterFrame(11f, 1f, 2f, 0f, 1f), new MusterFrame(77f, 3f, 4f, 0f, -1f));

            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 1f },
                new RunOptions { Autoplay = autoplay, StopAfterWaves = stopAfter, Seed = 1UL, LordFalls = lordFalls, ClassId = loadout?.ClassId })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Keep = keep,
                Loadout = loadout,
                Field = field ? new FieldPlaces(null, null) : null,
            };
            Director = new LongDefeatDirector(plan, Field, new LongDefeatSession(), Log, Outcomes, "m1", null, null, fort ? Field : null, lordAdapter ? Field : null);
        }

        public void Tick(float dt = 1f) => Director.Tick(dt);

        /// <summary>Starts the run and ticks until the wave's first batch is out.</summary>
        public void StartAndFight()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 200 && Field.Count("Refresh") == 0; i++)
                Tick();
            Assert.AreEqual(1, Field.Count("Refresh"), "the first wave never started");
        }

        /// <summary>Every attacker the field holds is gone: the tick reads the clear.</summary>
        public void Clear()
        {
            Field.AttackersStanding = 0;
            Tick();
        }

        /// <summary>Ticks until the next wave's first batch is out.</summary>
        public void NextWave()
        {
            var waves = Field.Count("Refresh");
            for (var i = 0; i < 200 && Field.Count("Refresh") == waves; i++)
                Tick();
            Assert.AreEqual(waves + 1, Field.Count("Refresh"), "the next wave never started");
        }

        /// <summary>The lord's body falls: the engine's removal, then the tick that reads no main agent.</summary>
        public void LordFalls(string removal = "P")
        {
            Director.OnAgentRemoved(removal, true);
            Field.MainBody = 0;
            Tick();
        }

        public JObject Report()
        {
            Director.Shutdown();
            return JObject.Parse(Director.ReportText);
        }

        public List<string> Warnings => Log.Lines.Where(l => l.StartsWith("WARN ")).ToList();

        /// <summary>The index of the first call from <paramref name="from"/> on that starts with the prefix, or -1.</summary>
        public int At(string prefix, int from = 0)
        {
            for (var i = from; i < Field.Calls.Count; i++)
            {
                if (Field.Calls[i].StartsWith(prefix, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public List<string> Lines(string id) => Outcomes.Lines.Where(l => l.Split(' ')[1] == id).ToList();
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- the rise (D40) ----

    [TestMethod]
    public void ALordWhoFell_RisesAtTheClear_AtTheRecordedKeep_AndTheRunBooksTheRise()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.StartAndFight();
        rig.LordFalls();
        Assert.AreEqual(0, rig.Field.Count("Rise:"), "no rise during the wave");

        rig.Clear();

        Assert.AreEqual(1, rig.Field.Count("Rise:"));
        var place = rig.Field.Rises.Single().Place!;
        Assert.AreEqual((138f, 159f, 32f), (place.X, place.Y, place.Z), "the keep as recorded");
        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"), "the director spawns nothing itself: the adapter spawns, then hands back");
        var rise = rig.Report()["lord"]!["rises"]!.Single();
        Assert.AreEqual((2, "recorded", false), ((int)rise["beforeWave"]!, (string)rise["at"]!, (bool)rise["handedBack"]!));
    }

    [TestMethod]
    public void WithNoKeepRecorded_TheRiseIsAtTheScenesFrame_AndSaysScene()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();

        var place = rig.Field.Rises.Single().Place!;
        Assert.AreEqual(rig.Field.Keep!.Value.X, place.X, "the scene's keep frame");
        Assert.AreEqual("scene", (string)rig.Report()["lord"]!["rises"]!.Single()["at"]!);
    }

    [TestMethod]
    public void ALordWhoAttacks_RisesAtTheEnginesAttackerFrame_NotInsideTheDefendersKeep()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.Field.PlayerAttacks = true;
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();
        rig.Tick();

        Assert.IsNull(rig.Field.Rises.Single().Place, "a story attack: the recorded keep is the enemy's");
        Assert.AreEqual("respawn", (string)rig.Report()["lord"]!["rises"]!.Single()["at"]!);
        StringAssert.Contains(rig.Lines(LongDefeatChecks.RiseId).Single(), "at=respawn main_is_lord=1 dist=none");
    }

    [TestMethod]
    public void AnAttackersBoot_TestsNoRisePoint_ThoughTheFortressRecordsAKeep()
    {
        var rig = new Rig(keep: RecordedKeep, fort: true);
        rig.Field.PlayerAttacks = true;
        rig.Field.FortPointFacts = p => new LdPointFacts(false, true);

        rig.StartAndFight();

        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        Assert.IsFalse(line.Contains("rise="), "the boot knows the side before it tests the points, and the enemy's keep is no rise point of ours: " + line);
        Assert.IsFalse(line.StartsWith("fail "), line);
    }

    // A story mission on a field scene (#1105 slice 2) has no keep to rise at: the adapter's field branch spawns the lord at his side's own start place
    // (LongDefeatBattlefieldAdapter.Player.cs, SpawnPlayer), so a keep the mission records is no rise place and nothing is measured against it.
    [TestMethod]
    public void OnAField_ALordWhoFell_RisesAtHisSidesStart_ThoughTheMissionRecordsAKeep()
    {
        var rig = new Rig(keep: RecordedKeep, field: true);
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();
        rig.Tick();

        Assert.IsNull(rig.Field.Rises.Single().Place, "the side's start place serves the rise on a field");
        StringAssert.Contains(rig.Lines(LongDefeatChecks.RiseId).Single(), "at=respawn main_is_lord=1 dist=none");
    }

    [TestMethod]
    public void AFieldBoot_TestsNoRisePoint_ThoughTheMissionRecordsAKeep()
    {
        var rig = new Rig(keep: RecordedKeep, fort: true, field: true);
        rig.Field.FortPointFacts = p => new LdPointFacts(false, true);

        rig.StartAndFight();

        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        Assert.IsFalse(line.Contains("rise="), line);
        Assert.IsFalse(line.StartsWith("fail "), line);
        Assert.IsFalse(rig.Log.Lines.Any(l => l.Contains("the rise point (the recorded keep) is unusable")), "no fall back to a scene frame a field does not have");
    }

    [TestMethod]
    public void AMountedLord_RisesAtTheHomePoint_AndSaysHome()
    {
        var rig = new Rig(keep: RecordedKeep, loadout: Cavalry());
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();

        var place = rig.Field.Rises.Single().Place!;
        Assert.AreEqual(138f, place.X, "no foot muster is recorded, so home is the keep");
        Assert.AreEqual("home", (string)rig.Report()["lord"]!["rises"]!.Single()["at"]!);
        Assert.AreEqual("cavalry", rig.Field.Rises.Single().Loadout!.ClassId, "the rise spawns the class kit");
    }

    [TestMethod]
    public void AClear_WithTheLordsOwnBodyStanding_RaisesNobody()
    {
        var rig = new Rig();
        rig.StartAndFight();

        rig.Clear();

        Assert.AreEqual(0, rig.Field.Count("Rise:"));
        Assert.AreEqual(0, rig.Field.Count("SpawnPlayer:True"));
        Assert.AreEqual(0, rig.Lines(LongDefeatChecks.RiseId).Count);
    }

    [TestMethod]
    public void TheLord_RisesOncePerWaveWithADeath_AndNotInAWaveWithNone()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.StartAndFight();
        rig.LordFalls();
        rig.Clear();
        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("Rise:"));

        rig.NextWave();
        rig.Clear();
        Assert.AreEqual(1, rig.Field.Count("Rise:"), "a wave in which he never fell owes no rise");

        rig.NextWave();
        rig.LordFalls();
        rig.Clear();
        Assert.AreEqual(2, rig.Field.Count("Rise:"), "a fall in a later wave repeats the cycle");
    }

    [TestMethod]
    public void ARiseWhoseSoldierWasTaken_IsAnsweredWithTheHandBack_AndTheCheckNamesIt()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.StartAndFight();
        rig.LordFalls();
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };
        rig.Tick();
        rig.Field.RiseAnswer = new LdRise(true, LdHandBack.Done, true);

        rig.Clear();
        rig.Tick();

        var report = rig.Report();
        Assert.IsTrue((bool)report["lord"]!["rises"]!.Single()["handedBack"]!);
        var line = rig.Lines(LongDefeatChecks.RiseId).Single();
        StringAssert.StartsWith(line, "pass " + LongDefeatChecks.RiseId + " w2 at=recorded main_is_lord=1");
        StringAssert.Contains(line, "handed_back=1 was_using=1");
    }

    [TestMethod]
    public void ASoldierGoneBeforeTheClear_IsNamedDead_NotNone()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };
        rig.Tick();

        rig.Clear();
        rig.Tick();

        StringAssert.Contains(rig.Lines(LongDefeatChecks.RiseId).Single(), "handed_back=dead", "the run expected a soldier to hand back and the field found none");
    }

    [TestMethod]
    public void TheRiseCheck_IsWrittenOnTheTickAfter_OnceAdoptionRan_AndPassesForAPerson()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();
        Assert.AreEqual(0, rig.Lines(LongDefeatChecks.RiseId).Count, "the rise tick writes no line: the body is read back after adoption");
        var adopts = rig.Field.Count("Adopt:");
        rig.Tick();

        Assert.AreEqual(adopts + 1, rig.Field.Count("Adopt:"), "the new body was adopted first");
        Assert.AreEqual("pass " + LongDefeatChecks.RiseId + " w2 at=recorded main_is_lord=1 dist=0.0 dz=0.0 controller=Player want=Player others=0 handed_back=none was_using=0 general_is_lord=1 owner_is_lord=1",
            rig.Lines(LongDefeatChecks.RiseId).Single());
        Assert.IsTrue(rig.At("Adopt:False", rig.At("Rise:")) < rig.At("ReadControl", rig.At("Rise:")), "adoption, then the read");
    }

    [TestMethod]
    public void UnderAutoplay_TheRiseCheckExpectsTheAiToDriveTheNewBody()
    {
        var rig = new Rig(autoplay: true);
        rig.StartAndFight();
        rig.LordFalls();
        rig.Clear();

        rig.Tick();

        StringAssert.Contains(rig.Lines(LongDefeatChecks.RiseId).Single(), "controller=AI want=AI");
        Assert.AreEqual("pass", rig.Lines(LongDefeatChecks.RiseId).Single().Split(' ')[0]);
    }

    [TestMethod]
    public void ASecondPlayerBodyOnTheTickAfter_FailsTheRise()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();
        rig.Clear();
        rig.Field.OtherPlayerBodies = 1;

        rig.Tick();

        StringAssert.StartsWith(rig.Lines(LongDefeatChecks.RiseId).Single(), "fail " + LongDefeatChecks.RiseId + " w2 ");
    }

    [TestMethod]
    public void AFailedSpawn_HandsNothingBack_FailsTheRise_AndTheNextClearOrdersItAgain()
    {
        var rig = new Rig(keep: RecordedKeep);
        rig.StartAndFight();
        rig.LordFalls();
        rig.Field.RiseSpawnOk = false;

        rig.Clear();
        rig.Tick();

        Assert.AreEqual("fail " + LongDefeatChecks.RiseId + " w2 at=recorded reason=spawn-refused handed_back=none", rig.Lines(LongDefeatChecks.RiseId).Single());
        Assert.AreEqual(1, rig.Field.Count("Rise:"));
        rig.Field.RiseSpawnOk = true;
        rig.NextWave();
        rig.Clear();
        Assert.AreEqual(2, rig.Field.Count("Rise:"), "the debt stands: the lord is still down");
        var rises = rig.Report()["lord"]!["rises"]!;
        Assert.AreEqual(1, rises.Count(), "only the rise that spawned is booked");
        Assert.AreEqual(3, (int)rises.Single()["beforeWave"]!);
    }

    [TestMethod]
    public void TheRise_ClassCheckIsWrittenFromTheReadBackOfTheNewBody()
    {
        var rig = new Rig(keep: RecordedKeep, loadout: Cavalry());
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();

        Assert.IsTrue(rig.At("ReadKit", rig.At("Rise:")) > rig.At("Rise:"), "the new body's kit is read back after the rise");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Class] respawn")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void AClearThatEndsTheRun_RisesNobody()
    {
        var rig = new Rig(stopAfter: 1);
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();

        Assert.AreEqual(0, rig.Field.Count("Rise:"));
    }

    // ---- the player-death row ----

    [TestMethod]
    public void ADeathFollowedByARise_IsBookedViaRise()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();
        rig.Clear();

        rig.Tick();

        Assert.AreEqual("pass " + LongDefeatChecks.PlayerDeathId + " d1 via=rise", rig.Lines(LongDefeatChecks.PlayerDeathId).Single());
    }

    [TestMethod]
    public void ADeathFollowedByATakeover_IsBookedViaTakeover_ByPerson_AndALaterRiseDoesNotMislabelTheNextDeath()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };
        rig.Tick();
        Assert.AreEqual("pass " + LongDefeatChecks.PlayerDeathId + " d1 via=takeover by=person", rig.Lines(LongDefeatChecks.PlayerDeathId).Single());
        rig.Clear();
        rig.Tick();

        rig.NextWave();
        rig.LordFalls();
        rig.Field.MainBody = 6;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_bowman" };
        rig.Tick();

        Assert.AreEqual("pass " + LongDefeatChecks.PlayerDeathId + " d2 via=takeover by=person", rig.Lines(LongDefeatChecks.PlayerDeathId).Last(),
            "the rise's flag was spent at its adoption, so the next death is a take-over");
    }

    // ---- the general's seat (D41) ----

    [TestMethod]
    public void TheSeat_IsWrittenOnTheTickAfterTheFall_AndReadBackNeverAsTheDeadBody()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.Director.OnAgentRemoved("P", true);
        Assert.AreEqual(0, rig.Field.Count("General:"), "a removal writes nothing: the seat is the tick's");
        Assert.AreEqual(0, rig.Lines(LongDefeatChecks.GeneralDownId).Count);
        rig.Field.MainBody = 0;

        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("General:None"), string.Join(",", rig.Field.Calls));
        Assert.IsTrue(rig.At("ReadSeat") > rig.At("General:None"), "the check reads what was written");
        Assert.AreEqual("pass " + LongDefeatChecks.GeneralDownId + " d1 seat=none want=none", rig.Lines(LongDefeatChecks.GeneralDownId).Single());
    }

    [TestMethod]
    public void ASeatNobodyWrote_IsTheDeadBody_AndFailsTheCheck()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.Field.SeatOverride = new LdSeatRead(LdSeatKind.Stale);

        rig.LordFalls();

        Assert.AreEqual("fail " + LongDefeatChecks.GeneralDownId + " d1 seat=stale want=none", rig.Lines(LongDefeatChecks.GeneralDownId).Single());
    }

    [TestMethod]
    public void ATakeoverBeforeTheRead_MakesTheSeatCheckASkip_AndTheLordTakesTheSeatBack()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };

        rig.Tick();

        Assert.IsTrue(rig.Lines(LongDefeatChecks.GeneralDownId).Single().StartsWith("skip " + LongDefeatChecks.GeneralDownId + " d1 seat=main want=lord reason=has-main"), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void TheSeat_GoesBackToTheLord_WhenHeHasABodyAgain()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };

        rig.Tick();
        rig.Tick();

        CollectionAssert.AreEqual(new[] { GeneralSeatKind.None, GeneralSeatKind.Lord }, rig.Field.Seats.Select(s => s.Kind).ToArray(), "written when it changes, never every tick");
    }

    // ---- the bot's take-over (D44a) ----

    [TestMethod]
    public void UnderAutoplay_TheBotTakesASoldier_FiveSecondsAfterTheFall_AndTheAdoptionLogsHim()
    {
        var rig = new Rig(autoplay: true);
        rig.StartAndFight();
        rig.LordFalls();
        for (var i = 0; i < 3; i++)
            rig.Tick();
        Assert.AreEqual(0, rig.Field.Count("TakeOver"), "four seconds in");

        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("TakeOver"), "five seconds in");
        rig.Tick();

        Assert.AreEqual("pass " + LongDefeatChecks.PlayerDeathId + " d1 via=takeover by=bot", rig.Lines(LongDefeatChecks.PlayerDeathId).Single());
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat][Lord] takeover after troop=fow_militia")), string.Join("\n", rig.Log.Lines));
        Assert.AreEqual(1, rig.Field.Count("TakeOver"), "a taken body ends the asking");
    }

    [TestMethod]
    public void ANoSoldierToTake_IsAskedAgainWhileNoBodyStands_AndDoesNothingElse()
    {
        var rig = new Rig(autoplay: true);
        rig.Field.TakeAnswer = new LdTake(false, "");
        rig.StartAndFight();
        rig.LordFalls();

        for (var i = 0; i < 9; i++)
            rig.Tick();

        Assert.AreEqual(2, rig.Field.Count("TakeOver"), "at 5 s and at 10 s");
        Assert.AreEqual(0, rig.Lines(LongDefeatChecks.PlayerDeathId).Count);
    }

    [TestMethod]
    public void ForAPerson_NobodyIsTaken()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.LordFalls();

        for (var i = 0; i < 12; i++)
            rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("TakeOver"), "a person presses E himself");
    }

    // ---- the forced fall (D47) ----

    [TestMethod]
    public void WithLordFalls_TheLordIsFelledOnceInThatWave_ThroughTheAdapter()
    {
        var rig = new Rig(lordFalls: 1);
        rig.StartAndFight();

        for (var i = 0; i < 40; i++)
            rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("FellLord"));
    }

    [TestMethod]
    public void TheForcedRemoval_IsBookedForced_FromTheFieldsFact_AndANaturalOneIsNot()
    {
        var rig = new Rig(lordFalls: 1);
        rig.StartAndFight();
        for (var i = 0; i < 25; i++)
            rig.Tick();
        rig.Director.OnAgentRemoved("PF", true);
        rig.Field.MainBody = 0;
        rig.Tick();
        rig.Field.MainBody = 3;
        rig.Field.Lord = rig.Field.Lord with { Own = false, Id = "fow_militia" };
        rig.Tick();
        rig.Director.OnAgentRemoved("P", true);

        var deaths = rig.Report()["lord"]!["deaths"]!.ToList();

        CollectionAssert.AreEqual(new[] { true, false }, deaths.Select(d => (bool)d["forced"]!).ToArray(), "only the removal the blow produced is forced");
    }

    [TestMethod]
    public void WithLordFalls_ButNoLordAdapter_TheDirectorSaysItServesNothing()
    {
        var rig = new Rig(lordFalls: 1, lordAdapter: false);
        rig.StartAndFight();

        for (var i = 0; i < 40; i++)
            rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("FellLord"));
        Assert.IsTrue(rig.Warnings.Any(w => w.Contains("[LongDefeat][Lord]") && w.Contains("lord_falls")), string.Join("\n", rig.Warnings));
    }

    // ---- fort-points (D49) ----

    [TestMethod]
    public void AnUsableRecordedKeep_PassesFortPointsWithTheRisePoint_AndIsTheRisePlace()
    {
        var rig = new Rig(keep: RecordedKeep, fort: true);

        rig.Director.Start();

        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        StringAssert.StartsWith(line, "pass " + LongDefeatChecks.FortPointsId + " m1 points=0 failed=0 rise=recorded");
        Assert.AreEqual(138f, rig.Field.FortPointsAsked.Single().X, "the keep went through the fort adapter's own test");
    }

    [TestMethod]
    public void AnUnusableRecordedKeep_FallsBackToTheSceneFrameForTheRise_AndTheLineNamesIt()
    {
        var rig = new Rig(keep: RecordedKeep, fort: true);
        rig.Field.FortPointFacts = p => p.X == 138f ? new LdPointFacts(false, true) : new LdPointFacts(true, true);
        rig.StartAndFight();
        rig.LordFalls();

        rig.Clear();

        Assert.AreEqual(rig.Field.Keep!.Value.X, rig.Field.Rises.Single().Place!.X, "the scene's frame, not the recorded keep with no navmesh");
        Assert.AreEqual("scene", (string)rig.Report()["lord"]!["rises"]!.Single()["at"]!);
        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        StringAssert.StartsWith(line, "fail " + LongDefeatChecks.FortPointsId + " m1 points=0 failed=0 rise=scene-fallback rise_navmesh=no rise_boundary=yes");
        Assert.IsTrue(rig.Warnings.Any(w => w.Contains("rise point") && w.Contains("scene")), string.Join("\n", rig.Warnings));
    }

    [TestMethod]
    public void WithNoKeepRecorded_NoRisePointIsAsked_AndFortPointsStaysASkip()
    {
        var rig = new Rig(fort: true);

        rig.Director.Start();

        Assert.AreEqual(0, rig.Field.FortPointsAsked.Count);
        StringAssert.StartsWith(rig.Lines(LongDefeatChecks.FortPointsId).Single(), "skip " + LongDefeatChecks.FortPointsId);
    }

    // ---- the end ----

    [TestMethod]
    public void TheRunsEnd_ReleasesWhatTheFieldKeptForTheDeathRule()
    {
        var rig = new Rig();
        rig.StartAndFight();

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Field.Count("ReleaseLord"), "Finish releases it, and a second call at Shutdown adds none");
    }

    [TestMethod]
    public void AFallOnTheRunsLastTick_WritesTheSeatCheckAsASkip_SoItsRowExists()
    {
        var rig = new Rig();
        rig.StartAndFight();
        rig.Director.OnAgentRemoved("P", true);

        rig.Director.Shutdown();

        var line = rig.Lines(LongDefeatChecks.GeneralDownId).Single();
        Assert.IsTrue(line.StartsWith("skip " + LongDefeatChecks.GeneralDownId + " d1 seat=unread reason=run-ended"), line);
        rig.Director.Shutdown();
        Assert.AreEqual(1, rig.Lines(LongDefeatChecks.GeneralDownId).Count, "written once");
    }
}
