using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// Phase 4 Slice 1 builds the places' config and plan with no run code and offers nothing (D46, <see cref="LocationOffer.Offered"/>), so a run of a build that offers nothing must play exactly as it did:
/// the same calls to the field and the same gold at every step, whether or not shop.json carries an armoury, a yard and a Marshal, and fortresses.json records their places. Nothing reads the new plan
/// fields yet, so this passes before the slice's last line is written; it is the pin the later slices must keep passing until each raises its kind in <see cref="LocationOffer"/>.
/// </summary>
[TestClass]
public class LongDefeatDirectorLocationsTests
{
    private static readonly ArmouryPlan Armoury = new(4f, new[]
    {
        new GearSet("armour_1", "Armour", "", GearKind.Armour, null, new[] { new LdKitSlot("Head", "h1"), new LdKitSlot("Body", "b1") }, 250),
        new GearSet("weapons_archer", "Bow", "", GearKind.Weapons, new[] { "archer" }, new[] { new LdKitSlot("Item0", "bow") }, 200),
    });

    private static readonly YardPlan Yard = new(6f, new[] { new EngineOffer("mangonel", 450), new EngineOffer("trebuchet", 700) });

    private static readonly MarshalPlan Marshal = new("lord_4_7", 500, "Theodred", "A hero.");

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool locations, int? startGold)
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
            }, null, locations ? new LocationsPlan(Armoury, Yard, Marshal) : null),
            RunRules.Default with { PreparationSeconds = 90f, IntermissionSeconds = 90f },
            new RunOptions { Autoplay = true, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = FortOffer.None,
            ArmouryPoint = locations ? new MusterFrame(10f, 20f, 30f, 0f, 1f) : null,
            YardPoint = locations ? new MusterFrame(40f, 50f, 60f, 1f, 0f) : null,
            HiddenGearSets = locations ? new[] { "weapons_archer" } : new string[0],
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
    public void AnUnattendedRun_WithAnArmouryAYardAMarshalAndTheirPlaces_PlaysExactlyAsOneWithNone()
    {
        var plain = Play(NewRig(false, null));
        var placed = Play(NewRig(true, null));

        CollectionAssert.AreEqual(plain.Gold, placed.Gold, "the gold, tick by tick");
        CollectionAssert.AreEqual(plain.Calls, placed.Calls, "every call to the field");
        Assert.IsTrue(plain.Gold.Min() < 600, "the bot spent gold on packs, and the wave was played");
    }

    [TestMethod]
    public void AnUnattendedRun_WithAStartGold_BuysOnlyPacks_WhileNothingIsOffered()
    {
        var plain = Play(NewRig(false, 5000));
        var placed = Play(NewRig(true, 5000));

        CollectionAssert.AreEqual(plain.Gold, placed.Gold, "the gold, tick by tick");
        CollectionAssert.AreEqual(plain.Calls, placed.Calls);
        Assert.IsTrue(placed.Gold.Min() < 5000, "the bot spent its start gold on packs");
    }

    [TestMethod]
    public void TheBuildOffersNoLocation_SoTheShippedPlanCannotSellOne()
    {
        Assert.AreEqual(LocationOffer.None, LocationOffer.Offered);
        Assert.AreEqual(LocationOffer.None, new RunPlan("helms", new GarrisonRow[0], new WavePlan(new[] { new WaveSpec(1, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(1, 1)), new ShopPlan(0, new ShopOffer[0]), RunRules.Default, RunOptions.Default).Locations);
    }
}
