using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Phase 3 Slice 8, the towers' level 3 volley at the director (phase3-plan.md D20, D22, D30, D31): each tower with a recorded volley spot looses on its own clock through the same fire path as the Elven Volley,
/// from its own spots, with the tower's arrow and one of its post archers as owner. A tower with no archer standing withholds for the wave, a withheld shot keeps its cooldown, the clear stops it, and each use has its
/// own hit window (the arrows are told apart by the missile each one is) and its own <c>tower-volley</c> line.
/// </summary>
[TestClass]
public class LongDefeatDirectorTowerVolleyTests
{
    private static readonly TowerPlan Towers = new("fow_rohan_eastfold_bowman",
        new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) },
        new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f));

    private static MusterFrame Post(float x) => new(x, 0f, 30f, 0f, 1f);

    private static IReadOnlyList<LdTower> TwoTowers() => new[]
    {
        new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), new[] { new LdPoint(1f, 2f, 3f), new LdPoint(2f, 2f, 3f) }),
        new LdTower("wall", Enumerable.Range(0, 8).Select(i => Post(10 + i)).ToList(), new[] { new LdPoint(4f, 5f, 6f) }),
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

        public List<string> Of() => Outcomes.Of(LongDefeatChecks.TowerVolleyId);

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

        /// <summary>A foe hit by one tower's arrow: the arrow is told apart by its missile (the fire handed the indices back).</summary>
        public LdVolleyHit Hit(int missile, bool kill = false) => new("fow_tower_arrow", LdVolleyHitKind.Foe, kill, false, false, 45f, 30f, missile);
    }

    private static Rig NewRig(IReadOnlyList<LdTower>? towers = null, FortOffer? offered = null, FortPlan? fort = null, bool autoplay = false, int? startGold = null, bool elven = false)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[]
            {
                new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
                new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
            }, fort ?? new FortPlan(null, Towers, null, null)),
            RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 0.5f },
            new RunOptions { Autoplay = autoplay, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? FortOffer.Offered,
            Towers = towers ?? TwoTowers(),
            Volley = elven ? new LdVolleyOrigins(new[] { new LdPoint(0f, 0f, 0f), new LdPoint(10f, 0f, 0f) }) : null,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, null, rig.Field);
        return rig;
    }

    /// <summary>A started run with the three tower levels bought in the preparation and the first wave begun, thirty attackers in reach, the posts manned.</summary>
    private static Rig InWaveOne(IReadOnlyList<LdTower>? towers = null, int attackers = 30)
    {
        var rig = NewRig(towers);
        rig.Field.PlaceAttackers(attackers);
        Assert.IsTrue(rig.Director.Start());
        for (var level = 0; level < 3; level++)
        {
            Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
            rig.Tick();
        }

        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    // ---- firing ----

    [TestMethod]
    public void EachTowerWithASpot_LoosesAtItsFirstDelay_FromItsOwnSpots_WithTheTowerArrow_AndItsOwnTowerAsOwner()
    {
        var rig = InWaveOne();
        Assert.AreEqual(0, rig.Field.Volleys.Count, "nothing before the first delay");

        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);

        Assert.AreEqual(2, rig.Field.Volleys.Count);
        CollectionAssert.AreEquivalent(new[] { "hornburg", "wall" }, rig.Field.Volleys.Select(v => v.Tower).ToArray());
        Assert.IsTrue(rig.Field.Volleys.All(v => v.Item == "fow_tower_arrow" && v.Shots.Count == 6), "six of the tower's arrows each");
        var horn = rig.Field.Volleys.Single(v => v.Tower == "hornburg");
        Assert.IsTrue(horn.Shots.All(s => (s.Origin.Z == 5.5f && s.Origin.X <= 2f) && s.Speed == 45f), "from the tower's own spots, raised by its origin height: " + string.Join(", ", horn.Shots.Select(s => s.Origin)));
        var wall = rig.Field.TargetReads.Where(r => r.Origins.Count == 1 && r.Origins[0].X == 4f).ToList();
        Assert.AreEqual(1, wall.Count, "the wall tower read its targets from its one spot");
        Assert.AreEqual((80f, 2.5f), (wall[0].Range, wall[0].Height));
        var firedAt = rig.Log.Lines.Where(l => l.Contains("[LongDefeat][Volley]") && l.Contains("tower-")).ToList();
        Assert.IsTrue(firedAt.Any(l => l.Contains("tower-hornburg") && l.Contains("6 of 6 arrows")), string.Join("\n", firedAt));
    }

    [TestMethod]
    public void ATowerWithNoSpot_HasNoClock_AndNeverOrders()
    {
        var towers = new[]
        {
            new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), new[] { new LdPoint(1f, 2f, 3f) }),
            new LdTower("blind", Enumerable.Range(0, 8).Select(i => Post(10 + i)).ToList(), System.Array.Empty<LdPoint>()),
        };
        var rig = InWaveOne(towers);

        rig.PumpUntil(() => rig.Field.Volleys.Count >= 1, 40);
        rig.Ticks(5);

        CollectionAssert.AreEqual(new[] { "hornburg" }, rig.Field.Volleys.Select(v => v.Tower).ToArray());
    }

    [TestMethod]
    public void WhileNoPostArcherOfTheTowerStands_TheShotIsWithheldForNoOwner_AndTheTowerStopsForTheWave()
    {
        var rig = InWaveOne();
        rig.Field.KillArchers("wall", 8);

        rig.PumpUntil(() => rig.Field.Volleys.Count >= 1, 40);
        rig.Ticks(10);

        CollectionAssert.AreEqual(new[] { "hornburg" }, rig.Field.Volleys.Select(v => v.Tower).ToArray(), "only the tower with a post archer standing fired");
        Assert.AreEqual(2, rig.Field.Calls.Count(c => c == "FireVolley:fow_tower_arrow"), "the hornburg's shot fired, and the wall's was asked for once, answered and then left alone");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("tower-wall") && l.Contains("withheld: NoOwner")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Volley"))));
        Assert.IsFalse(rig.Session.GetView()!.Volleys.Towers.Any(t => t.Source.TowerId == "wall" && t.Ready), "the wall's clock is stopped for the wave");
    }

    [TestMethod]
    public void AWithheldShot_KeepsTheCooldown_AndFiresOnceSomeoneIsInReach()
    {
        var rig = InWaveOne(attackers: 0);
        rig.Ticks(25);

        Assert.AreEqual(0, rig.Field.Volleys.Count, "nobody in reach");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("tower-hornburg") && l.Contains("withheld: NoTargets")));
        Assert.IsTrue(rig.Session.GetView()!.Volleys.Towers.All(t => t.SecondsLeft == 0f), "the cooldown was never spent");

        rig.Field.PlaceAttackers(30);
        rig.Ticks(2);

        Assert.AreEqual(2, rig.Field.Volleys.Count, "both towers, on the next ask");
    }

    [TestMethod]
    public void AfterAFire_TheTowerWaitsItsCooldown_ThenLoosesAgain_UnderTheNextUseNumber()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);
        rig.Ticks(20);

        Assert.AreEqual(2, rig.Field.Volleys.Count, "30 s cooldown");

        rig.Ticks(15);

        Assert.AreEqual(4, rig.Field.Volleys.Count);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("w1 use2 tower-hornburg")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Volley"))));
    }

    [TestMethod]
    public void TheClear_StopsTheTowers_AndNothingFiresBetweenWaves()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);

        rig.ClearWave();
        var fired = rig.Field.Volleys.Count;
        rig.Ticks(60);

        Assert.AreEqual(fired, rig.Field.Volleys.Count);
        Assert.IsFalse(rig.Director.WantsVolleyHits, "the windows are shut");
    }

    // ---- the hits and the check ----

    [TestMethod]
    public void EachUse_HasItsOwnWindow_AndItsHitsAreToldApartByTheMissileEachOneIs()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);
        var horn = rig.Field.Volleys.FindIndex(v => v.Tower == "hornburg");
        var wall = rig.Field.Volleys.FindIndex(v => v.Tower == "wall");
        Assert.IsTrue(rig.Director.WantsVolleyHits);

        for (var i = 0; i < 4; i++)
            rig.Director.OnVolleyHit(rig.Hit(rig.Field.VolleyMissiles[horn][i], kill: i == 0));
        for (var i = 0; i < 6; i++)
            rig.Director.OnVolleyHit(rig.Hit(rig.Field.VolleyMissiles[wall][i]));
        rig.Ticks(7);

        var lines = rig.Of();
        Assert.AreEqual(2, lines.Count, string.Join("\n", lines));
        var hornLine = lines.Single(l => l.StartsWith("tower-hornburg-w1u1 "));
        var wallLine = lines.Single(l => l.StartsWith("tower-wall-w1u1 "));
        StringAssert.Contains(hornLine, " hits=4 ");
        StringAssert.Contains(hornLine, "kills=1");
        StringAssert.Contains(wallLine, " hits=6 ");
        StringAssert.Contains(wallLine, "kills=0");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.TowerVolleyId), string.Join("\n", rig.Outcomes.Lines));
        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.VolleyId).Count, "the Elven Volley's own check is not written for a tower's use");
    }

    [TestMethod]
    public void ATowerHit_IsBookedInTheWavesTowerVolleyRow_NotTheElvenRow()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);
        var horn = rig.Field.Volleys.FindIndex(v => v.Tower == "hornburg");
        for (var i = 0; i < 3; i++)
            rig.Director.OnVolleyHit(rig.Hit(rig.Field.VolleyMissiles[horn][i], kill: true));
        rig.Tick();
        rig.ClearWave();

        rig.Director.Shutdown();

        var wave = Newtonsoft.Json.Linq.JObject.Parse(rig.Director.ReportText)["waves"]![0]!;
        Assert.AreEqual((2, 12, 3, 3), (((int)wave["towerVolley"]!["uses"]!), (int)wave["towerVolley"]!["arrows"]!, (int)wave["towerVolley"]!["hits"]!, (int)wave["towerVolley"]!["kills"]!));
        Assert.AreEqual(0, (int)wave["volley"]!["uses"]!, "the Elven Volley's row is untouched");
        Assert.AreEqual(0, (int)wave["volley"]!["hits"]!);
    }

    [TestMethod]
    public void AHitNamingNoMissile_BelongsToNoWindow_AndIsNotBookedInTheRunTally()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);

        rig.Director.OnVolleyHit(rig.Hit(-1));
        rig.Ticks(7);
        rig.ClearWave();
        rig.Director.Shutdown();

        CollectionAssert.AreEqual(new[] { "0", "0" }, rig.Of().Select(l => Regex.Match(l, @" hits=(\d+) ").Groups[1].Value).ToArray(), "the match is by the exact missile; the production hooks always name one");
        var wave = Newtonsoft.Json.Linq.JObject.Parse(rig.Director.ReportText)["waves"]![0]!;
        Assert.AreEqual(0, (int)wave["towerVolley"]!["hits"]!, "the wave's hits are the sum of the check lines' hits, so a hit with no window is in neither");
    }

    [TestMethod]
    public void AHitNamingAMissileNoOpenWindowLoosed_IsDroppedWhenSeveralWindowsAreOpen_NotHandedToAnotherTower()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);

        rig.Director.OnVolleyHit(rig.Hit(99999));
        rig.Ticks(7);

        CollectionAssert.AreEqual(new[] { "0", "0" }, rig.Of().Select(l => Regex.Match(l, @" hits=(\d+) ").Groups[1].Value).ToArray(), "a late arrow of a closed window is nobody else's");
    }

    [TestMethod]
    public void AHitNamingAnUnknownMissile_IsDropped_EvenWithOneWindowOpen()
    {
        var towers = new[] { new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), new[] { new LdPoint(1f, 2f, 3f) }) };
        var rig = InWaveOne(towers);
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 1, 40);

        rig.Director.OnVolleyHit(rig.Hit(99999));
        rig.Ticks(7);

        StringAssert.Contains(rig.Of().Single(), " hits=0 ", "a late arrow, or one a person picked up and fired again, is nobody's");
    }

    [TestMethod]
    public void TheClear_CutsAnOpenTowerWindow_AndRunEndWritesItsLine_EndedRun()
    {
        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);

        rig.ClearWave();

        Assert.AreEqual(2, rig.Of().Count);
        Assert.IsTrue(rig.Of().All(l => l.Contains("ended=wave")), string.Join("\n", rig.Of()));

        var next = InWaveOne();
        next.PumpUntil(() => next.Field.Volleys.Count >= 1, 40);
        next.Director.Shutdown();
        Assert.IsTrue(next.Of().All(l => l.Contains("ended=run")), string.Join("\n", next.Of()));
    }

    [TestMethod]
    public void TheTowerVolleyLine_AsTheLogWritesIt_MatchesThePassPatternOfItsManifestRow()
    {
        // The same transform the game applies (OutcomeLog.SanitiseDetail) and the same text the verdict tool reads: a renamed key or a bare word fails here instead of grading every real pass NOT_REACHED.
        var toml = RepoPaths.ReadSource("tools/ingame_checks/1079.toml");
        var row = toml.Substring(toml.IndexOf("id = \"taom.long-defeat.tower-volley\"", System.StringComparison.Ordinal));
        var pattern = Regex.Match(row, @"^pass = \['(.*)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.IsTrue(pattern.Length > 0, "the tower-volley row has no pass pattern");

        var rig = InWaveOne();
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);
        for (var v = 0; v < 2; v++)
        {
            for (var i = 0; i < 6; i++)
                rig.Director.OnVolleyHit(rig.Hit(rig.Field.VolleyMissiles[v][i]));
        }

        rig.Ticks(7);

        var passes = rig.Outcomes.Lines.Where(l => l.StartsWith("pass " + LongDefeatChecks.TowerVolleyId + " ")).ToList();
        Assert.AreEqual(2, passes.Count, string.Join("\n", rig.Outcomes.Lines));
        foreach (var pass in passes)
        {
            var detail = pass.Substring(pass.IndexOf(" w=", System.StringComparison.Ordinal) + 1);
            var line = "[Check] id=taom.long-defeat.tower-volley result=pass " + OutcomeLog.SanitiseDetail(detail);
            Assert.IsTrue(Regex.IsMatch(line, pattern), line + "\n" + pattern);
            foreach (var word in detail.Split(' '))
                Assert.IsTrue(word.IndexOf('=') > 0, $"'{word}' would be glued to the value before it by the log sanitiser");
        }
    }

    // ---- the Elven Volley beside it ----

    [TestMethod]
    public void TheElvenVolleysHits_AreNotTakenByATowersWindow_WhenBothAreOpen()
    {
        var volley = new VolleyPlan("ld_elven_arrow", 3f, new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto) });
        var rig = NewRig(fort: new FortPlan(null, Towers, null, volley), elven: true);
        rig.Field.PlaceAttackers(30);
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyVolleyLevel());
        for (var level = 0; level < 3; level++)
        {
            Assert.AreEqual(FortResult.Ok, rig.Session.TryBuyTowerLevel());
            rig.Tick();
        }

        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        rig.PumpUntil(() => rig.Field.Volleys.Count >= 2, 40);
        Assert.AreEqual(VolleyArm.Armed, rig.Session.TryVolley());
        rig.Tick();
        Assert.AreEqual(3, rig.Field.Volleys.Count, "the towers' two and the Elven Volley's one, windows open together");
        var elven = rig.Field.Volleys.FindIndex(v => v.Item == "ld_elven_arrow");
        var horn = rig.Field.Volleys.FindIndex(v => v.Tower == "hornburg");

        rig.Director.OnVolleyHit(new LdVolleyHit("ld_elven_arrow", LdVolleyHitKind.Foe, false, false, false, 45f, 30f, rig.Field.VolleyMissiles[elven][0]));
        rig.Director.OnVolleyHit(rig.Hit(rig.Field.VolleyMissiles[horn][0]));
        rig.Ticks(7);

        StringAssert.Contains(rig.Outcomes.Of(LongDefeatChecks.VolleyId).Single(), " hits=1 ");
        StringAssert.Contains(rig.Of().Single(l => l.StartsWith("tower-hornburg")), " hits=1 ");
    }

    // ---- the bot's schedule (D27) ----

    [TestMethod]
    public void TheBot_BuysTowerLevelThreeAtClearTwo_AndEachTowerLoosesItsVolleyInWaveThree()
    {
        var rig = NewRig(autoplay: true, startGold: 5000);
        rig.Field.PlaceAttackers(30);
        Assert.IsTrue(rig.Director.Start());

        rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Wave);
        Assert.AreEqual(1, rig.Session.GetView()!.Fort.Towers.Level, "bought in the preparation");
        rig.ClearWave();
        rig.PumpUntil(() => rig.Session.GetView()!.Fort.Towers.Level == 2, 100);
        rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Wave, 200);
        rig.ClearWave();
        rig.PumpUntil(() => rig.Session.GetView()!.Fort.Towers.Level == 3, 100);
        var volleysBeforeWaveThree = rig.Field.Volleys.Count;
        rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Wave, 200);

        rig.PumpUntil(() => rig.Field.Volleys.Count >= volleysBeforeWaveThree + 2, 60);

        CollectionAssert.AreEquivalent(new[] { "hornburg", "wall" }, rig.Field.Volleys.Skip(volleysBeforeWaveThree).Take(2).Select(v => v.Tower).ToArray());
        Assert.AreEqual(3, rig.Session.GetView()!.WavesCleared + 1, "this is wave 3");
    }

    [TestMethod]
    public void WithNoSpotRecorded_TheBotBuysTwoTowerLevels_AndNeverTheThird()
    {
        var noSpots = new[]
        {
            new LdTower("hornburg", Enumerable.Range(0, 8).Select(i => Post(i)).ToList(), System.Array.Empty<LdPoint>()),
        };
        var rig = NewRig(noSpots, autoplay: true, startGold: 5000);
        rig.Field.PlaceAttackers(30);
        Assert.IsTrue(rig.Director.Start());

        for (var clear = 0; clear < 3; clear++)
        {
            rig.PumpUntil(() => rig.Session.GetView()!.Phase == RunPhase.Wave, 300);
            rig.ClearWave();
        }

        rig.Ticks(5);
        Assert.AreEqual(2, rig.Session.GetView()!.Fort.Towers.Level, "level 3 would be a volley with no clock, so it is never sold");
        Assert.AreEqual(0, rig.Field.Volleys.Count);
    }
}
