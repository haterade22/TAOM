using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Slice 2 builds the fortification domain, config and bot with no engine code, and ships <see cref="FortOffer.Offered"/> empty (D31), so a run of the shipped game must play exactly as it did:
/// the same calls to the field and the same gold at every step, whether or not shop.json carries a fortifications block, recorded towers and a <c>start_gold</c>.
/// </summary>
[TestClass]
public class LongDefeatDirectorFortTests
{
    private static readonly FortPlan Everything = new(
        new GatePlan(200, new[] { new GateLevelPlan(50f, 200), new GateLevelPlan(100f, 300), new GateLevelPlan(150f, 450) }),
        new TowerPlan("fow_militia", new[] { new TowerLevelPlan(4, false, 250), new TowerLevelPlan(8, false, 300), new TowerLevelPlan(8, true, 400) }, new TowerVolleyPlan("fow_tower_arrow", 6, 20f, 30f, 6f, 80f, 2.5f)),
        new CarePlan(0.75f, 150, 1),
        new VolleyPlan("ld_elven_arrow", 3f, new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto) }));

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool fortifications, int? startGold, bool autoplay = true)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30), new GarrisonRow("fow_yeomen", FormationSlot.Ranged, 10) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75), new WaveTroop("skirm", FormationSlot.Ranged, 25) }) }, new WaveScaling(10, 300)),
            new ShopPlan(600, new[]
            {
                new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180),
                new ShopOffer("yeomen", "Yeomen", "fow_yeomen", FormationSlot.Ranged, 10, 200),
            }, fortifications ? Everything : null),
            RunRules.Default with { PreparationSeconds = 90f, IntermissionSeconds = 90f },
            new RunOptions { Autoplay = autoplay, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Towers = fortifications ? new[] { new LdTower("hornburg", new[] { new MusterFrame(0f, 0f, 0f, 0f, 1f) }, new[] { new LdPoint(1f, 2f, 3f) }) } : System.Array.Empty<LdTower>(),
            Volley = fortifications ? new LdVolleyOrigins(new[] { new LdPoint(0f, 0f, 0f) }) : null,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null);
        return rig;
    }

    /// <summary>Two waves of an unattended run, by the same script every time: the calls the director made on the field and the gold after every step.</summary>
    private static (List<string> Calls, List<int> Gold) Play(Rig rig)
    {
        var gold = new List<int>();
        void Step(float dt)
        {
            rig.Director.Tick(dt);
            gold.Add(rig.Session.GetView()!.Gold);
        }

        Assert.IsTrue(rig.Director.Start());
        Step(1f);
        Step(1f);
        Step(35f);
        Step(1f);
        for (var i = 0; i < 200 && !rig.Field.Calls.Contains("Refresh"); i++)
            Step(1f);
        for (var i = 0; i < 110; i++)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        for (var i = 0; i < 6; i++)
            Step(1f);
        Step(35f);
        Step(1f);
        return (rig.Field.Calls.ToList(), gold);
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void AnUnattendedRun_WithAFortificationsBlockAndRecordedTowers_PlaysExactlyAsOneWithNone()
    {
        var plain = Play(NewRig(false, null));
        var fortified = Play(NewRig(true, null));

        CollectionAssert.AreEqual(plain.Gold, fortified.Gold, "the gold, tick by tick");
        CollectionAssert.AreEqual(plain.Calls, fortified.Calls, "every call to the field");
        Assert.IsTrue(plain.Gold.Min() < 600, "the bot spent gold on packs, and the wave was played");
    }

    [TestMethod]
    public void AnUnattendedRun_WithAStartGold_BuysOnlyPacks_WhileNothingIsOffered()
    {
        var plain = Play(NewRig(false, 5000));
        var fortified = Play(NewRig(true, 5000));

        CollectionAssert.AreEqual(plain.Gold, fortified.Gold, "the gold, tick by tick");
        CollectionAssert.AreEqual(plain.Calls, fortified.Calls);
        Assert.IsTrue(fortified.Gold.Min() < 5000, "the bot spent its start gold on packs");
    }

    [TestMethod]
    public void TheBotsView_ShowsNoRowAvailable_WhileNothingIsOffered()
    {
        var rig = NewRig(true, 5000);
        rig.Director.Start();
        rig.Director.Tick(1f);

        var fort = rig.Session.GetView()!.Fort;

        Assert.IsFalse(fort.Gate.Available || fort.Repair.Available || fort.Towers.Available || fort.Care.Available || fort.Volley.Available);
        Assert.IsTrue(fort.BuysFirst, "a start_gold run would buy fortifications first, and finds none");
    }
}
