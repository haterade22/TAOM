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
/// Phase 3 Slice 5, the gates end to end (phase3-plan.md D17 to D19): a level and a repair are bought between waves and land on the tick after the buy, level first and repair second; a level keeps the gate's
/// share of hit points, a repair restores the levelled maximum of a broken or a damaged gate by one path; the checks <c>gate-level</c>, <c>gate-repair</c> and <c>gate-attacked</c> read each back; and the boot
/// writes the fort-points check and the A23 line.
/// </summary>
[TestClass]
public class LongDefeatDirectorGateTests
{
    private static readonly FortPlan Everything = new(
        new GatePlan(200, new[] { new GateLevelPlan(50f, 200), new GateLevelPlan(100f, 300), new GateLevelPlan(150f, 450) }), null, new CarePlan(0.75f, 150, 1), null);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public void Tick(float dt = 1f) => Director.Tick(dt);

        public void PumpUntil(System.Func<bool> done, int limit = 400)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(1f);
            Assert.IsTrue(done(), "the condition never came true");
        }

        public List<string> Of(string id) => Outcomes.Of(id);

        /// <summary>Kills every attacker standing, one tick at a time, until the wave is cleared and the shop is open.</summary>
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

        /// <summary>Starts the run and plays wave 1 to its clear: the shop is open for the intermission after wave 1.</summary>
        public void ClearWaveOne()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
            ClearWave();
        }

        /// <summary>Sounds the horn and plays until the next wave's first attackers are out.</summary>
        public void StartNextWave()
        {
            var refreshes = Field.Count("Refresh");
            Assert.AreEqual(StartResult.Ok, Session.TryStartWave());
            PumpUntil(() => Field.Count("Refresh") > refreshes);
        }
    }

    private static Rig NewRig(bool fort = true, FortOffer? offered = null, bool autoplay = false, int? startGold = null, IReadOnlyList<LdTower>? towers = null, LdVolleyOrigins? volley = null,
        FakeLongDefeatEffects? effects = null, bool field = true)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(1200, new[]
            {
                new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
                new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
            }, Everything),
            RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 0.5f },
            new RunOptions { Autoplay = autoplay, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? FortOffer.Offered,
            Towers = towers ?? System.Array.Empty<LdTower>(),
            Volley = volley,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, effects, field ? rig.Field : null);
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- the level ----

    [TestMethod]
    public void ALevelBoughtInThePreparation_IsWrittenOnTheNextTick_NotOnTheBuy_AndItsCheckPasses()
    {
        var rig = NewRig();
        rig.Director.Start();

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyGateLevel());
        Assert.AreEqual(0, rig.Field.Count("GateLevel"), "a buy never touches the engine by itself");
        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("GateLevel:1.50"));
        Assert.AreEqual(22500f, rig.Field.Gates[0].MaxHitPoints);
        Assert.AreEqual(1, rig.Of(LongDefeatChecks.GateLevelId).Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(rig.Of(LongDefeatChecks.GateLevelId)[0], "l1 l=1 factor=1.50 gates=1 max=22500.0/22500.0");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateLevelId), string.Join("\n", rig.Outcomes.Lines));
        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("GateLevel"), "told once");
    }

    [TestMethod]
    public void ALevelBoughtOnADentedGate_KeepsItsShare_AndABrokenGateStaysBroken()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(4000f);
        rig.Tick();

        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyGateLevel());
        rig.Tick();

        var gate = rig.Field.Gates[0];
        Assert.AreEqual(22500f, gate.MaxHitPoints);
        Assert.AreEqual(11000f * 1.5f - 1f, gate.HitPoints, 0.01f, "its share of the maximum, less one hit point; the missing share is not healed");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateLevelId));

        rig.Field.HitGates(100000f);
        rig.Tick();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyGateLevel());
        rig.Tick();
        Assert.AreEqual((0f, 30000f, true), (rig.Field.Gates[0].HitPoints, rig.Field.Gates[0].MaxHitPoints, rig.Field.Gates[0].Destroyed), "a level on a broken gate raises only its maximum");
        Assert.IsFalse(rig.Outcomes.Has("fail", LongDefeatChecks.GateLevelId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ALevelThatTheEngineWroteWrong_FailsItsCheck()
    {
        var rig = NewRig();
        rig.Director.Start();
        rig.Field.LevelWritesLow = 0.9f;

        rig.Session.TryBuyGateLevel();
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.GateLevelId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsFalse(rig.Outcomes.Has("pass", LongDefeatChecks.GateLevelId));
    }

    // ---- the repair ----

    [TestMethod]
    public void ARepairOfABrokenGate_RestoresTheVanillaMaximumOnTheNextTick_AndItsCheckPassesWithPathBroken()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        var gold = rig.Session.GetView()!.Gold;

        Assert.AreEqual(FortResult.Ok, rig.Session.TryRepairGates());
        Assert.AreEqual(gold - 200, rig.Session.GetView()!.Gold);
        Assert.AreEqual(0, rig.Field.Count("RepairGates"));
        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("RepairGates:1.00"));
        Assert.IsFalse(rig.Field.Gates[0].Destroyed);
        var lines = rig.Of(LongDefeatChecks.GateRepairId);
        Assert.AreEqual(1, lines.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(lines[0], "w2 w=2 path=broken gates=1 hp=15000.0/15000.0");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateRepairId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ARepairOfADamagedGate_PaysItsShare_AndIsPathDamaged()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(7500f);
        rig.Tick();
        var gold = rig.Session.GetView()!.Gold;

        Assert.AreEqual(FortResult.Ok, rig.Session.TryRepairGates());
        rig.Tick();

        Assert.AreEqual(gold - 100, rig.Session.GetView()!.Gold, "half of the gate is missing: half of 200");
        Assert.AreEqual(15000f, rig.Field.Gates[0].HitPoints);
        StringAssert.Contains(rig.Of(LongDefeatChecks.GateRepairId)[0], "path=damaged");
    }

    [TestMethod]
    public void ARepairOfALevelledGate_RestoresTheLevelledMaximum_AndALevelBoughtBesideItIsWrittenFirst()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();

        Assert.AreEqual(FortResult.Ok, rig.Session.TryRepairGates());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyGateLevel());
        rig.Tick();

        Assert.IsTrue(rig.Field.Index("GateLevel:1.50") < rig.Field.Index("RepairGates:1.50"), string.Join(",", rig.Field.Calls));
        Assert.AreEqual((22500f, 22500f), (rig.Field.Gates[0].HitPoints, rig.Field.Gates[0].MaxHitPoints));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateRepairId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateLevelId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ARepairThatLeftTheGateDisabled_OrTheResetMissing_OrTheHiddenGateStanding_Fails()
    {
        var disabled = NewRig();
        disabled.ClearWaveOne();
        disabled.Field.HitGates(100000f);
        disabled.Tick();
        disabled.Field.RepairWorks = false;
        disabled.Session.TryRepairGates();
        disabled.Tick();
        Assert.IsTrue(disabled.Outcomes.Has("fail", LongDefeatChecks.GateRepairId), string.Join("\n", disabled.Outcomes.Lines));

        var missing = NewRig();
        missing.ClearWaveOne();
        missing.Field.HitGates(100000f);
        missing.Tick();
        missing.Field.ResetFound = false;
        missing.Session.TryRepairGates();
        missing.Tick();
        StringAssert.Contains(missing.Of(LongDefeatChecks.GateRepairId)[0], "reset=missing");
        Assert.IsTrue(missing.Outcomes.Has("fail", LongDefeatChecks.GateRepairId));

        var hidden = NewRig();
        hidden.ClearWaveOne();
        hidden.Field.HitGates(100000f);
        hidden.Tick();
        hidden.Field.HiddenStanding = 1;
        hidden.Session.TryRepairGates();
        hidden.Tick();
        Assert.IsTrue(hidden.Outcomes.Has("fail", LongDefeatChecks.GateRepairId));
    }

    [TestMethod]
    public void ARepairBoughtAfterTheWaveStarts_IsRefused_AndNothingIsWritten()
    {
        var rig = NewRig();
        rig.Director.Start();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.Field.HitGates(100000f);
        rig.Tick();

        Assert.AreEqual(FortResult.NotOpen, rig.Session.TryRepairGates());
        Assert.AreEqual(FortResult.NotOpen, rig.Session.TryBuyGateLevel());
        rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("RepairGates"));
        Assert.AreEqual(0, rig.Field.Count("GateLevel"));
    }

    [TestMethod]
    public void TheRepair_WaitsOneTickWhenItIsBoughtOnTheLastTickOfAStretch_SoTheWaveStartsAfterIt()
    {
        var rig = NewRig(autoplay: false);
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        Assert.AreEqual(FortResult.Ok, rig.Session.TryRepairGates());

        var spawned = rig.Field.Count("Spawn:att");
        rig.Session.TryStartWave();
        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("RepairGates"), "handed out before the wave begins");
        Assert.AreEqual(spawned, rig.Field.Count("Spawn:att"), "the wave's soldiers wait one tick (RCA row 10)");
        rig.Tick();
        Assert.IsTrue(rig.Field.Count("Spawn:att") > spawned);
    }

    // ---- gate-attacked ----

    [TestMethod]
    public void ARepairedGateThatTakesBlows_PassesGateAttackedAtTheNextClear_WithNobodyThrough()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.StartNextWave();

        rig.Field.HitGates(3000f);
        rig.PumpUntil(() => rig.Field.Count("Behind") > 0);
        rig.ClearWave();

        var lines = rig.Of(LongDefeatChecks.GateAttackedId);
        Assert.AreEqual(1, lines.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(lines[0], "w2 w=2 path=broken start=15000.0 now=12000.0 lost=3000.0 fell=false through=0");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateAttackedId));
    }

    [TestMethod]
    public void ARepairedGateThatFellAgain_PassesGateAttacked()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.StartNextWave();

        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.ClearWave();

        StringAssert.Contains(rig.Of(LongDefeatChecks.GateAttackedId)[0], "fell=true");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateAttackedId));
    }

    [TestMethod]
    public void ARepairedGateNobodyStruck_IsASkipWithNoBlow()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.StartNextWave();

        rig.ClearWave();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.GateAttackedId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.EndsWith(rig.Of(LongDefeatChecks.GateAttackedId)[0], " reason=no-blow");
    }

    [TestMethod]
    public void AnAttackerSampledBehindTheStandingRepairedGate_FailsGateAttacked_AndTheSampleUsesTheKeep()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.Field.BehindGate = 2;
        rig.StartNextWave();

        rig.Field.HitGates(3000f);
        rig.PumpUntil(() => rig.Field.Count("Behind") > 0, 40);
        rig.ClearWave();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.GateAttackedId), string.Join("\n", rig.Outcomes.Lines));
        Assert.AreEqual(new LdPoint(138.4f, 159.5f, 32.5f), rig.Field.SamplesAsked[0], "the inside is the keep");
    }

    [TestMethod]
    public void TheSampling_StopsAtTheClear_AndNeverRunsWithoutARepair()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.StartNextWave();
        rig.PumpUntil(() => rig.Field.Count("ReadKeep") > 12);
        Assert.AreEqual(0, rig.Field.Count("Behind"), "no repair, no watch");
    }

    [TestMethod]
    public void ARunThatEndsInTheWaveAfterARepair_WritesGateAttackedAtTheEnd()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.StartNextWave();
        rig.Field.HitGates(2000f);
        rig.Tick();

        rig.Director.Shutdown();

        var lines = rig.Of(LongDefeatChecks.GateAttackedId);
        Assert.AreEqual(1, lines.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(lines[0], "ended=run");
    }

    [TestMethod]
    public void ALevelBoughtAfterTheRepair_MovesTheWatchBaseline_SoItIsNotCountedAsALoss()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.Session.TryBuyGateLevel();
        rig.Tick();
        rig.StartNextWave();

        rig.ClearWave();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.GateAttackedId), "a level keeps the share less one point: that is no blow, " + string.Join("\n", rig.Outcomes.Lines));
    }

    // ---- the report ----

    [TestMethod]
    public void TheReport_ListsTheRepairWithItsPerGateShares_AndTheWavesGateRow()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Field.HitGates(7500f);
        rig.Tick();
        rig.Session.TryRepairGates();
        rig.Tick();
        rig.Director.Shutdown();

        var report = Newtonsoft.Json.Linq.JObject.Parse(rig.Director.ReportText);

        var repair = (Newtonsoft.Json.Linq.JObject)report["purchases"]!.Single(p => (string?)p["kind"] == "gateRepair");
        Assert.AreEqual(0.5, (double)repair["shares"]![0]!, 1e-9);
        Assert.AreEqual(100, (int)repair["price"]!);
        Assert.AreEqual(15000, (double)report["waves"]![0]!["gate"]!["maxHitPoints"]!, 1e-6);
    }

    // ---- the wiring ----

    [TestMethod]
    public void ADirectorWithNoFortAdapter_BuysNothingItCannotDo_AndTouchesNoGate()
    {
        var rig = NewRig(field: false);
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();

        rig.Session.TryRepairGates();
        rig.Session.TryBuyGateLevel();
        rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("RepairGates") + rig.Field.Count("GateLevel") + rig.Field.Count("FortPoints"));
        Assert.AreEqual(0, rig.Of(LongDefeatChecks.GateRepairId).Count + rig.Of(LongDefeatChecks.FortPointsId).Count);
    }

    [TestMethod]
    public void AnOfferedNone_BuysNoGate_AndReadsNone()
    {
        var rig = NewRig(offered: FortOffer.None);
        rig.ClearWaveOne();
        rig.Field.HitGates(100000f);
        rig.Tick();

        Assert.AreEqual(FortResult.NotOffered, rig.Session.TryRepairGates());
        Assert.AreEqual(FortResult.NotOffered, rig.Session.TryBuyGateLevel());
        rig.Tick();
        Assert.AreEqual(0, rig.Field.Count("RepairGates"));
    }

    [TestMethod]
    public void TheGatesTheFieldReadsEachTick_ReachTheRunsView()
    {
        var rig = NewRig();
        rig.Director.Start();
        rig.Tick();
        rig.Field.HitGates(15000f);
        rig.Tick();

        var gate = rig.Session.GetView()!.Fort.Gates.Single();

        Assert.IsTrue(gate.Destroyed);
    }

    // ---- the bot ----

    [TestMethod]
    public void TheBot_RepairsABrokenGateFirst_ThenBuysGateLevelOneAtClearOne()
    {
        var rig = NewRig(autoplay: true, startGold: 5000);
        rig.Director.Start();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        Assert.AreEqual(0, rig.Field.Count("GateLevel"), "wave 1 meets the vanilla gate: the bot buys no level before clear 1");
        rig.Field.HitGates(100000f);
        rig.Tick();

        rig.ClearWave();
        rig.Tick();
        rig.Tick();

        Assert.IsTrue(rig.Field.Index("RepairGates:1.00") >= 0 && rig.Field.Index("GateLevel:1.50") > rig.Field.Index("RepairGates:1.00"), string.Join(",", rig.Field.Calls));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateRepairId), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.GateLevelId), string.Join("\n", rig.Outcomes.Lines));
        Assert.AreEqual("w2", rig.Outcomes.Lines.First(l => l.StartsWith("pass " + LongDefeatChecks.GateRepairId)).Split(' ')[2]);
    }

    // ---- the boot ----

    [TestMethod]
    public void TheBoot_WithNoRecordedPoint_WritesAFortPointsSkip_AndTheA23Line()
    {
        var rig = NewRig();

        Assert.IsTrue(rig.Director.Start());

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.FortPointsId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Of(LongDefeatChecks.FortPointsId)[0], "reason=none-recorded");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("INFO ") && l.Contains("[LongDefeat][Boot] archer_positions=3 formations=0:30")), string.Join("\n", rig.Log.Lines));
        Assert.AreEqual(0, rig.Field.Count("FortPoints"), "no points to ask about");
    }

    [TestMethod]
    public void TheA23Line_IsWrittenAgainOnTheFirstWaveStateLine_OnceTheTacticHasSplitTheFormations()
    {
        // The boot line is written in the same call that spawns the opening batch, before the siege tactic's first tick has split any formation; index 4 can read empty there and be claimed seconds later.
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(0, rig.Log.Lines.Count(l => l.Contains("[LongDefeat][Wave] archer_positions=")), "not at the boot");

        rig.PumpUntil(() => rig.Log.Lines.Any(l => l.Contains("[State] wave 1 Wave")), 600);
        rig.Tick();
        rig.Tick();

        Assert.AreEqual(1, rig.Log.Lines.Count(l => l.StartsWith("INFO ") && l.Contains("[LongDefeat][Wave] archer_positions=3 formations=")), "once, on the first wave state line");
        Assert.AreEqual(1, rig.Log.Lines.Count(l => l.Contains("[LongDefeat][Boot] archer_positions=")), "the boot line stays");
    }

    [TestMethod]
    public void TheBoot_ChecksEveryRecordedPostAndOrigin_WithTheSallyPointTest()
    {
        var towers = new[]
        {
            new LdTower("hornburg", new[] { new MusterFrame(1f, 2f, 3f, 0f, 1f), new MusterFrame(4f, 5f, 6f, 0f, 1f) }, new[] { new LdPoint(7f, 8f, 9f) }),
            new LdTower("wall", new[] { new MusterFrame(10f, 11f, 12f, 1f, 0f) }, System.Array.Empty<LdPoint>()),
        };
        var rig = NewRig(towers: towers, volley: new LdVolleyOrigins(new[] { new LdPoint(20f, 21f, 22f), new LdPoint(23f, 24f, 25f) }));

        Assert.IsTrue(rig.Director.Start());

        Assert.AreEqual(6, rig.Field.FortPointsAsked.Count, "3 posts, 1 tower volley spot and 2 volley origins");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FortPointsId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Of(LongDefeatChecks.FortPointsId)[0], "points=6 failed=0");
    }

    [TestMethod]
    public void TheBoot_APointOffTheNavmesh_FailsTheCheck_NamesIt_AndStillStartsTheRun()
    {
        var towers = new[] { new LdTower("hornburg", new[] { new MusterFrame(1f, 2f, 3f, 0f, 1f), new MusterFrame(4f, 5f, 6f, 0f, 1f) }, System.Array.Empty<LdPoint>()) };
        var rig = NewRig(towers: towers);
        rig.Field.FortPointFacts = place => new LdPointFacts(place.X != 4f, true);

        Assert.IsTrue(rig.Director.Start(), "a bad point is read by its own slice; it does not refuse the boot");

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.FortPointsId));
        StringAssert.Contains(rig.Of(LongDefeatChecks.FortPointsId)[0], "bad=hornburg.post[1](navmesh=no boundary=yes)");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.StartsWith("WARN ") && l.Contains("hornburg.post[1]")), "the boot line names the point");
    }
}
