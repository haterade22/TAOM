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
/// The engines a player destroys come back for the next wave (#1160): each wave start restores them before the Hammer writes their hit points, an <c>engine-restore</c> line reads them back, the clear of
/// that wave writes <c>engine-use</c>, and the test option <c>break_engines</c> destroys them at one clear so an unattended run can prove it.
/// </summary>
[TestClass]
public class LongDefeatDirectorEngineTests
{
    private static LdEngineState Engine(string kind, int index, float traveled = 0f, bool arrived = false) =>
        new(kind, index, false, false, 5000f, 5000f, traveled, arrived, true, false, true, true);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public void PumpUntil(System.Func<bool> done, int limit = 400)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(1f);
            Assert.IsTrue(done(), "the condition never came true");
        }

        public List<string> Of(string id) => Outcomes.Of(id);

        public RunPhase Phase => Session.GetView()!.Phase;

        public void StartFirstWave()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
        }

        public void ClearWave()
        {
            for (var round = 0; round < 20 && Phase == RunPhase.Wave; round++)
            {
                while (Field.AttackersStanding > 0)
                {
                    Field.AttackersStanding--;
                    Director.OnAgentRemoved("A:orc", true);
                }

                Director.Tick(1f);
            }

            Assert.AreNotEqual(RunPhase.Wave, Phase);
        }

        public void StartNextWave()
        {
            var refreshes = Field.Count("Refresh");
            Assert.AreEqual(StartResult.Ok, Session.TryStartWave());
            PumpUntil(() => Field.Count("Refresh") > refreshes);
        }
    }

    private static Rig NewRig(int? breakAt = null, int? waves = null)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f },
            new RunOptions { Autoplay = false, BreakEnginesAtClear = breakAt, StopAfterWaves = waves })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
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
    public void EachWaveStart_RestoresTheEngines_BeforeTheHammerWritesTheirHitPoints()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.StartNextWave();

        Assert.AreEqual(2, rig.Field.Count("RestoreEngines"), "one restore per wave start");
        var calls = rig.Field.Calls;
        var hammers = calls.Select((c, i) => (c, i)).Where(p => p.c.StartsWith("Engines:")).Select(p => p.i).ToList();
        var restores = calls.Select((c, i) => (c, i)).Where(p => p.c == "RestoreEngines").Select(p => p.i).ToList();
        Assert.AreEqual(2, hammers.Count);
        Assert.IsTrue(restores[0] < hammers[0] && restores[1] < hammers[1] && hammers[0] < restores[1], string.Join(", ", calls));
    }

    [TestMethod]
    public void AWaveStartThatRestoredNothing_WritesNoLine()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();

        Assert.AreEqual(0, rig.Of(LongDefeatEngineChecks.RestoreId).Count);
        Assert.AreEqual(0, rig.Of(LongDefeatEngineChecks.UseId).Count);
        Assert.AreEqual(0, rig.Field.Count("ReadEngines"), "no engine came back, so the clear has nothing to judge");
    }

    [TestMethod]
    public void RestoredEngines_ArePassedAtTheWaveStart_AndJudgedAtItsClear()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.Field.RestoreAnswer = new LdEngineRestore(new[] { Engine("tower", 0), Engine("ram", 0) });
        rig.StartNextWave();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatEngineChecks.RestoreId));
        Assert.AreEqual("w2 w=2 restored=2 tower0=ok ram0=ok", rig.Of(LongDefeatEngineChecks.RestoreId).Single());
        StringAssert.Contains(rig.Log.Lines.Single(l => l.Contains("[Engines] w2 restored")), "tower0=ok ram0=ok");

        rig.Field.EngineStates = new[] { Engine("tower", 0, 38f, arrived: true), Engine("tower", 1, 40f), Engine("ram", 0, 22f) };
        rig.ClearWave();

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatEngineChecks.UseId));
        Assert.AreEqual("w2 w=2 restored=2 tower0=arrived,38.0 ram0=used,22.0", rig.Of(LongDefeatEngineChecks.UseId).Single());
    }

    [TestMethod]
    public void ARestoreWithAProblem_Fails_AtTheWaveStart()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.Field.RestoreAnswer = new LdEngineRestore(System.Array.Empty<LdEngineState>(), "reset missing");
        rig.StartNextWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatEngineChecks.RestoreId));
        Assert.AreEqual("w2 w=2 restored=0 problem=reset_missing", rig.Of(LongDefeatEngineChecks.RestoreId).Single());
    }

    [TestMethod]
    public void TheRunEndingInsideTheWave_JudgesTheRestoredEngines_AsRunEnded()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.Field.RestoreAnswer = new LdEngineRestore(new[] { Engine("tower", 0) });
        rig.StartNextWave();
        rig.Field.EngineStates = new[] { Engine("tower", 0) };

        rig.Director.Shutdown();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatEngineChecks.UseId));
        Assert.AreEqual("w2 w=2 restored=1 tower0=idle,0.0 reason=run-ended", rig.Of(LongDefeatEngineChecks.UseId).Single());
    }

    [TestMethod]
    public void AWaveWithNoRestore_AfterOneWithARestore_JudgesNothingAtItsClear()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.Field.RestoreAnswer = new LdEngineRestore(new[] { Engine("tower", 0) });
        rig.StartNextWave();
        rig.Field.EngineStates = new[] { Engine("tower", 0, 20f) };
        rig.ClearWave();
        var reads = rig.Field.Count("ReadEngines");
        rig.StartNextWave();
        rig.ClearWave();

        Assert.AreEqual("w2 w=2 restored=1 tower0=used,20.0", rig.Of(LongDefeatEngineChecks.UseId).Single(), "wave 3 restored nothing, so only wave 2 is judged");
        Assert.AreEqual(1, rig.Of(LongDefeatEngineChecks.RestoreId).Count);
        Assert.AreEqual(reads, rig.Field.Count("ReadEngines"), "the clear of a wave that restored nothing reads no engine");
    }

    [TestMethod]
    public void BreakEngines_DestroysTheEngines_AtThatClearOnly()
    {
        var rig = NewRig(breakAt: 1);
        rig.StartFirstWave();
        rig.ClearWave();

        Assert.AreEqual(1, rig.Field.Count("BreakEngines"));
        StringAssert.Contains(rig.Log.Lines.Single(l => l.Contains("[Engines] break_engines")), "3 engine(s) destroyed at clear 1");

        rig.StartNextWave();
        rig.ClearWave();

        Assert.AreEqual(1, rig.Field.Count("BreakEngines"), "only clear 1 breaks them");
    }

    [TestMethod]
    public void BreakEngines_AtTheClearThatEndsTheRun_BreaksNothing()
    {
        var rig = NewRig(breakAt: 1, waves: 1);
        rig.StartFirstWave();
        rig.ClearWave();

        Assert.AreEqual(0, rig.Field.Count("BreakEngines"), "no wave follows, so nothing could prove the restore");
    }

    [TestMethod]
    public void NoBreakOption_NeverBreaksAnEngine()
    {
        var rig = NewRig();
        rig.StartFirstWave();
        rig.ClearWave();
        rig.StartNextWave();
        rig.ClearWave();

        Assert.AreEqual(0, rig.Field.Count("BreakEngines"));
    }
}
