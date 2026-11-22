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
/// The live gear swap and the bot's errand at the director (phase4-plan.md Slice 5, spike S11): an unattended run walks the lord to the armoury, buys a set there and has it put on his own body on the next tick,
/// releases the walk, builds the merged kit at every later spawn of him, and writes the lines the game run reads. The engine half is faked, so these pin the order and the bookkeeping, never the swap itself.
/// </summary>
[TestClass]
public class LongDefeatDirectorArmouryTests
{
    private static readonly MusterFrame ArmouryAt = new(100f, 20f, 30f, 0f, 1f);
    private static readonly MusterFrame SallyFrame = new(100f, 200f, 30f, 0f, 1f);

    private static readonly GearSet Armour = new("armour_1", "Armour", "", GearKind.Armour, null, new[] { new LdKitSlot("Head", "h1"), new LdKitSlot("Body", "b1") }, 250);
    private static readonly GearSet Weapons = new("weapons_axe", "Axe", "", GearKind.Weapons, new[] { "fighter" }, new[] { new LdKitSlot("Item0", "axe") }, 200);

    private static LdLoadout Fighter() => new("fighter", false, new[] { new LdKitSlot("Item0", "sword") });

    private static LdLoadout Rider() => new("fighter", true, new[] { new LdKitSlot("Item0", "sword"), new LdKitSlot("Horse", "horse_a") });

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }

        public Rig(bool lordAdapter = true, bool fort = false, LdLoadout? loadout = null, float prep = 90f, bool atPoint = false, MusterFrame? sally = null, int stopAfter = 3)
        {
            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }, null, new LocationsPlan(new ArmouryPlan(4f, new[] { Armour, Weapons }), null)),
                RunRules.Default with { PreparationSeconds = prep, IntermissionSeconds = 90f },
                new RunOptions { Autoplay = true, StopAfterWaves = stopAfter, Seed = 1UL, StartGold = 5000, ClassId = "fighter" })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Keep = new KeepPoint(138f, 159f, 32f, 20f),
                Loadout = loadout ?? Fighter(),
                ArmouryPoint = ArmouryAt,
                Locations = new LocationOffer(true, false, false),
                Sally = sally,
            };
            if (atPoint)
                Field.Lord = Field.Lord with { At = new LdPoint(100f, 20f, 30f) };
            Director = new LongDefeatDirector(plan, Field, Session, Log, Outcomes, "m1", null, null, fort ? Field : null, lordAdapter ? Field : null);
        }

        public int Ticks { get; private set; }

        public void Tick(float dt = 1f)
        {
            Ticks++;
            Director.Tick(dt);
        }

        public void TickUntil(Func<bool> done, string what, int max = 400)
        {
            for (var i = 0; i < max && !done(); i++)
                Tick();
            Assert.IsTrue(done(), what + "\n" + string.Join("\n", Field.Calls.Skip(Math.Max(0, Field.Calls.Count - 30))));
        }

        public void StartRun() => Assert.IsTrue(Director.Start());

        public int At(string prefix, int from = 0)
        {
            for (var i = from; i < Field.Calls.Count; i++)
            {
                if (Field.Calls[i].StartsWith(prefix, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public string? Line(string contains) => Log.Lines.FirstOrDefault(l => l.Contains(contains));

        public int LineAt(string contains) => Log.Lines.FindIndex(l => l.Contains(contains));

        public List<string> Warnings => Log.Lines.Where(l => l.StartsWith("WARN ")).ToList();

        public void LordFalls()
        {
            Director.OnAgentRemoved("P", true);
            Field.MainBody = 0;
            Tick();
        }

        public List<string> Lines(string id) => Outcomes.Lines.Where(l => l.Split(' ')[1] == id).ToList();
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void ABoughtSet_IsPutOnTheLordsOwnBody_OnTheTickAfterTheBuy_BeforeTheWaveStarts()
    {
        var rig = new Rig();
        rig.StartRun();

        rig.TickUntil(() => rig.Session.GetView()!.Locations.Gear.Any(g => g.State == GearState.Owned), "the bot never bought the armour set");
        Assert.AreEqual(0, rig.Field.Count("Swap:"), "nothing is put on in the tick that buys");
        var boughtAt = rig.Ticks;

        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("Swap:fighter"));
        Assert.AreEqual(boughtAt + 1, rig.Ticks);
        Assert.IsTrue(rig.At("ErrandEnd") >= 0 && rig.At("ErrandEnd") < rig.At("Swap:"), "the walk is released, then he is dressed");
        Assert.AreEqual(0, rig.Field.Count("Refresh"), "no wave has started");
        rig.TickUntil(() => rig.Field.Count("Refresh") > 0, "the wave never started");
        Assert.IsTrue(rig.LineAt("[Armoury] swap") < rig.LineAt("[Wave] 1 starts"), "the swap is booked before the wave's first line");
    }

    [TestMethod]
    public void TheSwap_CarriesTheClassKitWithTheSetLaidOverIt()
    {
        var rig = new Rig();
        rig.StartRun();

        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "no swap");

        var kit = rig.Field.Swaps.Single().Slots.ToDictionary(s => s.Slot, s => s.ItemId);
        Assert.AreEqual("sword", kit["Item0"], "the class's weapon stays");
        Assert.AreEqual("h1", kit["Head"]);
        Assert.AreEqual("b1", kit["Body"]);
    }

    [TestMethod]
    public void TheBotsErrand_WalksTheLord_BuysThere_AndReleasesTheWalk()
    {
        var rig = new Rig();
        rig.StartRun();

        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "no swap");

        Assert.AreEqual(1, rig.Field.ErrandStarts.Count);
        Assert.AreEqual(100f, rig.Field.ErrandStarts.Single().X);
        Assert.AreEqual(0, rig.Field.ErrandTeleports.Count, "he arrived on his own");
        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"));
        StringAssert.Contains(rig.Line("[Armoury] swap")!, "reach=walk");
        StringAssert.Contains(rig.Line("[Errand] start")!, "place=Armoury");
        StringAssert.Contains(rig.Line("[Errand] end")!, "why=end");
    }

    [TestMethod]
    public void AnErrandThatDoesNotArrive_IsTeleportedOnceAfter20Seconds_AndBooksTeleport()
    {
        var rig = new Rig();
        rig.Field.ErrandArrives = false;
        rig.StartRun();

        rig.TickUntil(() => rig.Field.ErrandStarts.Count > 0, "no errand");
        var startedAt = rig.Ticks;
        rig.TickUntil(() => rig.Field.ErrandTeleports.Count > 0, "no teleport");

        Assert.IsTrue(Math.Abs(rig.Ticks - startedAt - 20) <= 1, "the teleport comes after the 20 s walk budget: " + (rig.Ticks - startedAt));
        Assert.AreEqual(1, rig.Field.Count("ErrandTeleport:"));
        StringAssert.Contains(rig.Line("[Errand] teleport")!, "place=Armoury");
        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "the bot never bought after the teleport");
        StringAssert.Contains(rig.Line("[Armoury] swap")!, "reach=teleport");
        Assert.AreEqual(1, rig.Field.Count("ErrandTeleport:"), "asked once");
    }

    [TestMethod]
    public void AnErrandWhoseTeleportFails_IsGivenUp5SecondsLater_ReleasedOnce_AndNotTriedAgainThisStretch()
    {
        var rig = new Rig();
        rig.Field.ErrandArrives = false;
        rig.Field.TeleportOk = false;
        rig.StartRun();

        rig.TickUntil(() => rig.Field.ErrandTeleports.Count > 0, "no teleport");
        var teleportedAt = rig.Ticks;
        rig.TickUntil(() => rig.Field.Count("ErrandEnd") > 0, "the errand was never released");

        Assert.IsTrue(Math.Abs(rig.Ticks - teleportedAt - 5) <= 2, "given up about 5 s after the teleport: " + (rig.Ticks - teleportedAt));
        for (var i = 0; i < 30; i++)
            rig.Tick();
        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"), "released once");
        Assert.AreEqual(1, rig.Field.ErrandStarts.Count, "not tried again this stretch");
        Assert.AreEqual(1, rig.Field.ErrandTeleports.Count);
        Assert.AreEqual(0, rig.Field.Swaps.Count);
    }

    [TestMethod]
    public void ALordWhoFallsOnHisErrand_IsReleased_AndNoTeleportIsAsked()
    {
        var rig = new Rig();
        rig.Field.ErrandArrives = false;
        rig.StartRun();
        rig.TickUntil(() => rig.Field.ErrandStarts.Count > 0, "no errand");

        rig.LordFalls();
        for (var i = 0; i < 40; i++)
            rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"));
        Assert.AreEqual(0, rig.Field.Count("ErrandTeleport:"), "no teleport for a body that is gone");
        Assert.AreEqual(0, rig.Field.Count("Swap:"));
    }

    [TestMethod]
    public void AWaveThatBeginsDuringAnErrand_ReleasesTheWalk()
    {
        var probe = new Rig();
        probe.Field.ErrandArrives = false;
        probe.StartRun();
        probe.TickUntil(() => probe.Field.ErrandStarts.Count > 0, "no errand");
        var rig = new Rig(prep: probe.Ticks + 21f);
        rig.Field.ErrandArrives = false;
        rig.Field.TeleportOk = false;
        rig.StartRun();

        rig.TickUntil(() => rig.Field.Count("Refresh") > 0, "the wave never started");

        rig.Tick();

        Assert.AreEqual(1, rig.Field.ErrandStarts.Count);
        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"), "the wave's start ends the errand and the walk is released");
        Assert.IsTrue(rig.LineAt("[Wave] 1 starts") < rig.LineAt("[Errand] end"), "released after the wave began");
    }

    [TestMethod]
    public void ARunThatEndsWithAnErrandOpen_ReleasesTheWalkOnce()
    {
        var rig = new Rig();
        rig.Field.ErrandArrives = false;
        rig.StartRun();
        rig.TickUntil(() => rig.Field.ErrandStarts.Count > 0, "no errand");
        rig.Tick();

        rig.Director.Shutdown();
        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"));
        StringAssert.Contains(rig.Line("[Errand] end")!, "why=run-ended");
    }

    [TestMethod]
    public void ARunThatEndsAfterTheErrandWasServedItsEnd_ReleasesNothingMore()
    {
        var rig = new Rig();
        rig.StartRun();
        rig.TickUntil(() => rig.Field.Count("ErrandEnd") > 0, "the errand never ended");

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Field.Count("ErrandEnd"));
    }

    [TestMethod]
    public void TheRiseAfterABuy_SpawnsTheLordInTheMergedKit()
    {
        var rig = new Rig();
        rig.StartRun();
        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "no swap");
        rig.TickUntil(() => rig.Field.Count("Refresh") > 0, "no wave");
        rig.LordFalls();

        rig.Field.AttackersStanding = 0;
        rig.TickUntil(() => rig.Field.Rises.Count > 0, "no rise");
        rig.Tick();

        var slots = rig.Field.Rises.Single().Loadout!.Slots.ToDictionary(s => s.Slot, s => s.ItemId);
        Assert.AreEqual("h1", slots["Head"]);
        Assert.AreEqual("sword", slots["Item0"]);
        var line = rig.Lines(LongDefeatChecks.ClassId).Last();
        StringAssert.Contains(line, "Head=h1");
        StringAssert.Contains(line, "Body=b1");
        Assert.IsTrue(line.StartsWith("pass "), line);
    }

    [TestMethod]
    public void TheSallyRemountAfterABuy_BuildsTheMergedKit()
    {
        var rig = new Rig(loadout: Rider(), sally: SallyFrame);
        rig.Field.Lord = rig.Field.Lord with { HasMount = false };
        rig.StartRun();

        rig.TickUntil(() => rig.Field.Remounts.Count > 0, "no remount");

        var slots = rig.Field.Remounts.Single().Loadout.Slots.ToDictionary(s => s.Slot, s => s.ItemId);
        Assert.AreEqual("h1", slots["Head"]);
        Assert.AreEqual("horse_a", slots["Horse"]);
    }

    [TestMethod]
    public void ARefusedSwap_KeepsTheSetOwned_AndTheNextRiseBuildsIt()
    {
        var rig = new Rig();
        rig.Field.SwapAnswer = LdSwap.Refused("not-own-body");
        rig.StartRun();
        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "no swap");
        StringAssert.Contains(rig.Line("[Armoury] swap")!, "done=0");
        StringAssert.Contains(rig.Line("[Armoury] swap")!, "problem=not-own-body");
        rig.TickUntil(() => rig.Field.Count("Refresh") > 0, "no wave");

        rig.LordFalls();
        rig.Field.AttackersStanding = 0;
        rig.TickUntil(() => rig.Field.Rises.Count > 0, "no rise");

        Assert.IsTrue(rig.Field.Rises.Single().Loadout!.Slots.Any(s => s.Slot == "Head" && s.ItemId == "h1"), "the refused set reaches the next spawn");
    }

    [TestMethod]
    public void WithNoLordAdapter_NeitherIsServed_AndEachIsWarnedOnce()
    {
        var rig = new Rig(lordAdapter: false);
        rig.Field.ErrandArrives = false;
        rig.StartRun();

        rig.TickUntil(() => rig.Warnings.Any(w => w.Contains("the bot's errand")), "no warning for the errand");
        rig.Field.Lord = rig.Field.Lord with { At = new LdPoint(100f, 20f, 30f) };
        rig.TickUntil(() => rig.Warnings.Any(w => w.Contains("the gear swap")), "no warning for the swap");
        for (var i = 0; i < 60; i++)
            rig.Tick();

        Assert.AreEqual(1, rig.Warnings.Count(w => w.Contains("the bot's errand")));
        Assert.AreEqual(1, rig.Warnings.Count(w => w.Contains("the gear swap")));
        Assert.AreEqual(0, rig.Field.Count("Swap:") + rig.Field.Count("ErrandStart:") + rig.Field.Count("ErrandEnd"));
    }

    [TestMethod]
    public void TheArmouryLine_NamesVariantSetReachArmourHealthAndMount()
    {
        var rig = new Rig();
        rig.Field.SwapAnswer = new LdSwap(true, "", "A", new LdArmour(1f, 2f, 3f, 4f, 5f), new LdArmour(6f, 7f, 8f, 9f, 10f), new LdArmour(6f, 7f, 8f, 9f, 10.5f), 90f, 100f, 91f, 101f, true, true, "sword", 2);
        rig.StartRun();

        rig.TickUntil(() => rig.Field.Swaps.Count > 0, "no swap");

        Assert.AreEqual("INFO [LongDefeat][Armoury] swap variant=A set=armour_1 sets=1 reach=walk done=1 before=1.0/2.0/3.0/4.0 after=6.0/7.0/8.0/9.0 sums=6.0/7.0/8.0/9.0 enc=5.0->10.0 model_enc=10.5 health=90.0/100.0->91.0/101.0 mounted=1 mount_after=1 wield=sword kit_wrong=2",
            rig.Line("[Armoury] swap"));
    }

    [TestMethod]
    public void AnUnusableArmouryPoint_FailsFortPoints_AndHidesTheArmouryForTheRun()
    {
        var rig = new Rig(fort: true);
        rig.Field.FortPointFacts = p => p.X == 100f ? new LdPointFacts(false, true) : new LdPointFacts(true, true);
        rig.StartRun();

        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        Assert.IsTrue(line.StartsWith("fail "), line);
        StringAssert.Contains(line, "armoury=unusable armoury_navmesh=no armoury_boundary=yes");
        for (var i = 0; i < 100; i++)
            rig.Tick();
        Assert.AreEqual(0, rig.Field.Count("ErrandStart:"), "an armoury that is hidden sends nobody");
        Assert.AreEqual(0, rig.Field.Count("Swap:"));
    }

    [TestMethod]
    public void AnUsableArmouryPoint_IsNamedRecorded_AndIsAskedBeforeTheRisePlace()
    {
        var rig = new Rig(fort: true);
        rig.StartRun();

        var line = rig.Lines(LongDefeatChecks.FortPointsId).Single();
        Assert.IsTrue(line.StartsWith("pass "), line);
        StringAssert.Contains(line, "armoury=recorded");
        CollectionAssert.AreEqual(new[] { 100f, 138f }, rig.Field.FortPointsAsked.Select(p => p.X).ToArray(), "the rise stays last");
    }

    [TestMethod]
    public void TheRiseReadBack_LogsTheFreshBodysDrivenArmour()
    {
        var rig = new Rig();
        rig.Field.ArmourRead = new LdArmour(11f, 12f, 13f, 14f, 15f);
        rig.StartRun();
        rig.TickUntil(() => rig.Field.Count("Refresh") > 0, "no wave");
        rig.LordFalls();
        rig.Field.AttackersStanding = 0;
        rig.TickUntil(() => rig.Field.Rises.Count > 0, "no rise");

        rig.TickUntil(() => rig.Lines(LongDefeatChecks.RiseId).Count > 0, "the rise was never read back");

        StringAssert.Contains(rig.Line("read back")!, "armour=11.0/12.0/13.0/14.0/15.0");
    }

    [TestMethod]
    public void AStartTheFieldRefuses_OpensNoWalk_SoNothingIsReleased_AndNoSetIsBought()
    {
        var rig = new Rig();
        rig.Field.ErrandStartOk = false;
        rig.StartRun();
        rig.TickUntil(() => rig.Field.ErrandStarts.Count > 0, "no errand");

        for (var i = 0; i < 40; i++)
            rig.Tick();

        Assert.AreEqual(0, rig.Field.Count("ErrandEnd"), "the director opened no walk, so it has none to release");
        Assert.AreEqual(0, rig.Field.Count("Swap:"), "he never stood at the armoury, so nothing was bought");
    }
}
