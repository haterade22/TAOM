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
/// Phase 3 Slice 4, field care end to end (phase3-plan.md D21): the heal is bought between waves, lands on the tick after the buy through the existing <c>RestDefenders</c>, never in a wave and
/// never beside the clear's own rest, and the <c>field-care</c> check reads it back (the arithmetic with the engine's ceil, the purse, and the next tick's reading).
/// </summary>
[TestClass]
public class LongDefeatDirectorCareTests
{
    private const string Heal = "Rest:0.75";

    private static FortPlan Fort(CarePlan care) => new(new GatePlan(200, new[] { new GateLevelPlan(50f, 200) }), null, care, null);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;

        public void Tick(float dt = 1f) => Director.Tick(dt);

        public void PumpUntil(System.Func<bool> done, int limit = 300)
        {
            for (var i = 0; i < limit && !done(); i++)
                Director.Tick(1f);
            Assert.IsTrue(done(), "the condition never came true");
        }

        public List<string> Care => Outcomes.Of(LongDefeatChecks.FieldCareId);

        /// <summary>Starts the run, plays wave 1 and clears it: the tick that returns is the clear's, whose rest has already been applied.</summary>
        public void ClearWaveOne()
        {
            Assert.IsTrue(Director.Start());
            PumpUntil(() => Field.Calls.Contains("Refresh"));
            for (var i = 0; i < 110; i++)
            {
                Field.AttackersStanding--;
                Director.OnAgentRemoved("A:orc", true);
            }

            Tick();
            Assert.AreEqual(RunPhase.Intermission, Session.GetView()!.Phase);
        }
    }

    private static Rig NewRig(FortOffer? offered = null, int? startGold = null, bool autoplay = false, RunRules? rules = null, CarePlan? care = null)
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
            }, Fort(care ?? new CarePlan(0.75f, 150, 1))),
            rules ?? RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 0.5f },
            new RunOptions { Autoplay = autoplay, StartGold = startGold })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
            Offered = offered ?? new FortOffer(0, 0, true, 0),
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null);
        return rig;
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void ACareBoughtAfterTheClear_HealsOnTheNextTick_NotOnTheBuy_AndNotAgain()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        CollectionAssert.Contains(rig.Field.Calls, "Rest:0.5", "the clear's own rest");
        Assert.IsFalse(rig.Field.Calls.Contains(Heal), "the clear's tick carries no care: it was bought after that tick returned");
        var gold = rig.Session.GetView()!.Gold;

        Assert.AreEqual(FortResult.Ok, rig.Session.TryFieldCare());
        Assert.IsFalse(rig.Field.Calls.Contains(Heal), "a buy never touches a soldier by itself");
        Assert.AreEqual(gold - 150, rig.Session.GetView()!.Gold);

        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count(Heal), "the tick after the buy");
        rig.Tick();
        rig.Tick();
        Assert.AreEqual(1, rig.Field.Count(Heal), "told once");
    }

    [TestMethod]
    public void TheHeal_NeverSharesATickWithTheClearsRest()
    {
        var rig = NewRig();
        rig.ClearWaveOne();

        Assert.AreEqual(1, rig.Field.Count("Rest:"), "the clear's rest is the only heal so far");
        rig.Session.TryFieldCare();
        rig.Tick();

        Assert.AreEqual(2, rig.Field.Count("Rest:"));
        Assert.IsTrue(rig.Field.Index("Rest:0.5") < rig.Field.Index(Heal), "the clear's rest first, the care a tick later");
    }

    [TestMethod]
    public void ACareBoughtDuringAWave_IsRefused_AndHealsNobody()
    {
        var rig = NewRig();
        rig.Director.Start();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));

        Assert.AreEqual(FortResult.NotOpen, rig.Session.TryFieldCare());
        rig.Tick();

        Assert.IsFalse(rig.Field.Calls.Contains(Heal));
        Assert.AreEqual(0, rig.Care.Count);
    }

    [TestMethod]
    public void ACareBoughtBesideAPack_HealsAfterThePackSpawned_SoTheNewSoldiersAreInTheHeal()
    {
        var rig = NewRig();
        rig.ClearWaveOne();

        Assert.AreEqual(BuyResult.Ok, rig.Session.TryBuy("militia"));
        Assert.AreEqual(FortResult.Ok, rig.Session.TryFieldCare());
        rig.Tick();

        var pack = rig.Field.Calls.FindLastIndex(c => c.StartsWith("Spawn:def:fow_militia"));
        Assert.IsTrue(pack >= 0 && pack < rig.Field.Index(Heal), string.Join(",", rig.Field.Calls));
    }

    [TestMethod]
    public void TheCareCheck_PassesOnTheTickAfterTheHeal_ReadingTheMeanBackFromTheField()
    {
        var rig = NewRig();
        rig.Field.DefenderHealthMean = 0.6f;
        rig.ClearWaveOne();
        rig.Session.TryFieldCare();
        rig.Tick();
        Assert.AreEqual(0, rig.Care.Count, "the line waits for the next reading, which proves the heal stayed");

        rig.Tick();

        Assert.AreEqual(1, rig.Care.Count, string.Join("\n", rig.Outcomes.Lines));
        StringAssert.StartsWith(rig.Care[0], "c1 ");
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Care[0], "paid=150 purse=150");
        StringAssert.Contains(rig.Care[0], "reread=");
        Assert.IsFalse(rig.Care[0].Contains("reread=none"));
    }

    [TestMethod]
    public void TheCareCheck_AllowsTheEnginesCeil_ButNotAHealThatDidNotLand()
    {
        var ceil = NewRig();
        ceil.Field.RestCeil = 0.004f;
        ceil.ClearWaveOne();
        ceil.Session.TryFieldCare();
        ceil.Tick();
        ceil.Tick();
        Assert.IsTrue(ceil.Outcomes.Has("pass", LongDefeatChecks.FieldCareId), "a ceil under 1% is the engine's rounding: " + string.Join("\n", ceil.Outcomes.Lines));

        var unhealed = NewRig();
        unhealed.ClearWaveOne();
        unhealed.Field.RestHeals = false;
        unhealed.Field.RestResult = new LdRest(40, 0.6f, 0.6f);
        unhealed.Session.TryFieldCare();
        unhealed.Tick();
        unhealed.Tick();
        Assert.IsTrue(unhealed.Outcomes.Has("fail", LongDefeatChecks.FieldCareId), "the write never took: " + string.Join("\n", unhealed.Outcomes.Lines));
    }

    [TestMethod]
    public void TheCareCheck_FailsWhenTheNextReadingShowsTheHealUndone()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Session.TryFieldCare();
        rig.Tick();

        rig.Field.DefenderHealthMean = 0.3f;
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Care[0], "reread=0.300");
    }

    [TestMethod]
    public void TheCareCheck_SkipsWhenNobodyWasMissingHealth_ItProvesNothing()
    {
        var rig = NewRig(rules: RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 60f, RestShare = 1f });
        rig.ClearWaveOne();
        Assert.AreEqual(1f, rig.Field.DefenderHealthMean, 1e-6f, "the clear's full rest left nobody hurt");
        rig.Session.TryFieldCare();
        rig.Tick();
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Has("skip", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Care[0], "reason=too-little-missing");
    }

    [TestMethod]
    public void TheCareCheck_FailsWhenTheNextTickReadsNobody_ItIsNotTheRunEnding()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Session.TryFieldCare();
        rig.Tick();

        rig.Field.DefenderHealthMean = -1f;
        rig.Tick();

        Assert.IsTrue(rig.Outcomes.Has("fail", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void ARunThatEndsAfterTheHeal_FlushesTheLine_WithNoRereadToCompareAgainst()
    {
        var rig = NewRig();
        rig.ClearWaveOne();
        rig.Session.TryFieldCare();
        rig.Tick();
        Assert.AreEqual(0, rig.Care.Count);

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Care.Count);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Care[0], "reread=none");
    }

    [TestMethod]
    public void TwoCaresBeforeOneTick_MakeOneHeal_AndTheCheckAddsUpBothPrices()
    {
        var rig = NewRig(care: new CarePlan(0.5f, 100, 2));
        rig.ClearWaveOne();

        Assert.AreEqual(FortResult.Ok, rig.Session.TryFieldCare());
        Assert.AreEqual(FortResult.Ok, rig.Session.TryFieldCare());
        rig.Tick();
        rig.Tick();

        Assert.AreEqual(1, rig.Field.Count("Rest:0.75"), "two heals of half the missing health leave a quarter of it missing, in one call");
        Assert.AreEqual(1, rig.Care.Count);
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        StringAssert.Contains(rig.Care[0], "paid=200 purse=200");
    }

    [TestMethod]
    public void TheReport_ListsTheCare_AndTheGoldSpentOnIt()
    {
        var rig = NewRig(startGold: 5000);
        rig.ClearWaveOne();
        rig.Session.TryFieldCare();
        rig.Tick();
        rig.Director.Shutdown();

        var report = JObject.Parse(rig.Director.ReportText);
        Assert.AreEqual(5000, (int)report["startGold"]!);
        var purchase = (JObject)report["purchases"]!.Single(p => (string?)p["kind"] == "fieldCare");
        Assert.AreEqual((2, 150, 150, 0.75), ((int)purchase["beforeWave"]!, (int)purchase["basePrice"]!, (int)purchase["price"]!, (double)purchase["share"]!));
        Assert.AreEqual(150, (int)report["gold"]!["spentOn"]!["fieldCare"]!);
        Assert.AreEqual((long)report["gold"]!["spent"]!, (long)report["gold"]!["spentOn"]!["fieldCare"]!, "nothing else was bought");
    }

    [TestMethod]
    public void TheUnattendedBot_WithAStartGold_BuysCareAtClearOne_AndTheHealLandsAfterTheClearsTick()
    {
        var rig = NewRig(startGold: 5000, autoplay: true);
        Assert.IsTrue(rig.Director.Start());
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        for (var i = 0; i < 110; i++)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        rig.Tick();
        Assert.IsFalse(rig.Field.Calls.Contains(Heal), "the bot buys after the clear's tick has applied its effects");

        rig.PumpUntil(() => rig.Field.Calls.Contains(Heal), 10);
        rig.PumpUntil(() => rig.Care.Count > 0, 5);

        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.FieldCareId), string.Join("\n", rig.Outcomes.Lines));
        rig.Director.Shutdown();
        var report = JObject.Parse(rig.Director.ReportText);
        Assert.AreEqual(150, (int)report["gold"]!["spentOn"]!["fieldCare"]!);
        Assert.AreEqual(5000, (int)report["startGold"]!);
    }

    [TestMethod]
    public void WithNothingOffered_TheBotBuysNoCare_AndNothingHeals()
    {
        var rig = NewRig(FortOffer.None, 5000, true);
        rig.Director.Start();
        rig.PumpUntil(() => rig.Field.Calls.Contains("Refresh"));
        for (var i = 0; i < 110; i++)
        {
            rig.Field.AttackersStanding--;
            rig.Director.OnAgentRemoved("A:orc", true);
        }

        rig.Tick();
        for (var i = 0; i < 20; i++)
            rig.Tick();

        Assert.IsFalse(rig.Field.Calls.Contains(Heal));
        Assert.AreEqual(0, rig.Care.Count);
    }
}
