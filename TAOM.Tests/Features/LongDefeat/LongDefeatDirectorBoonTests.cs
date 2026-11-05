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
/// The boons at the director (phase2-plan.md section 6, <c>boon</c> and <c>reroll</c>): the unattended bot answers an open pick between waves, each pick and each reroll
/// writes its check, the horn's check follows the war chest factor in force, a Legend boon's cut shows in the affix check, and the report lists every pick.
/// </summary>
[TestClass]
public class LongDefeatDirectorBoonTests
{
    private static BoonRow Boon(string id, BoonCategory category, params (string Key, float Value)[] effects) =>
        new(id, category, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static BoonPlan Many() => new(new[]
    {
        Boon("gold", BoonCategory.WarChest, ("gold", 300f)),
        Boon("horn", BoonCategory.WarChest, ("earlyGoldPercent", 100f)),
        Boon("long", BoonCategory.Fortress, ("intermissionSeconds", 30f)),
        Boon("mend", BoonCategory.Fortress, ("restShare", 0.25f)),
        Boon("phial", BoonCategory.Legend, ("affixCount", -1f)),
    }, 1);

    private static AffixPlan Affixes() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>(),
        AffixPlan.Ids.ToDictionary(id => id, id => "{=N_" + id + "}" + id));

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; }
        public LongDefeatSession Session { get; } = new();

        public Rig(BoonPlan boons, bool autoplay = true, int stopAfter = 4, int rerolls = 1, int startWave = 1, ulong seed = 1)
        {
            var waves = new WavePlan(
                new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) },
                new WaveScaling(10, 300));
            var plan = new RunPlan(
                "helms",
                new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
                waves,
                new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
                RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 60f },
                new RunOptions { Autoplay = autoplay, StopAfterWaves = stopAfter, StartRerolls = rerolls, StartWave = startWave, Seed = seed })
            {
                Bounties = new Dictionary<string, int> { ["orc"] = 4, ["skirm"] = 6 },
                Affixes = Affixes(),
                Boons = boons,
            };
            Director = new LongDefeatDirector(plan, Field, Session, Log, Outcomes, "m1", null, null);
        }

        /// <summary>One second of mission time in which every attacker that stands is gone by the next read, so each wave clears as soon as its queue is out.</summary>
        public void Step()
        {
            Director.Tick(1f);
            Field.AttackersStanding = 0;
        }

        public void RunToTheEnd()
        {
            Assert.IsTrue(Director.Start());
            for (var i = 0; i < 4000 && Director.Result == null; i++)
                Step();
        }
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void TheBot_AnswersTheThirdClearsPick_WithARerollFirst_AndEachWritesItsCheck()
    {
        var rig = new Rig(Many());

        rig.RunToTheEnd();

        Assert.IsNotNull(rig.Director.Result);
        Assert.AreEqual(1, rig.Outcomes.Of(LongDefeatChecks.RerollId).Count, string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.RerollId));
        var boon = rig.Outcomes.Of(LongDefeatChecks.BoonId).Single();
        Assert.IsTrue(rig.Outcomes.Has("pass", LongDefeatChecks.BoonId), boon);
        StringAssert.StartsWith(boon, "p1 p=1 cards=3 ");
        StringAssert.Contains(boon, "draws=2");
    }

    [TestMethod]
    public void AViewersRun_MakesNoPickByItself_AndWritesNoBoonCheck()
    {
        var rig = new Rig(Many(), autoplay: false, stopAfter: 3);

        rig.Director.Start();
        for (var i = 0; i < 3000 && rig.Director.Result == null; i++)
        {
            rig.Step();
            if (rig.Session.GetView()?.Phase == RunPhase.Intermission)
                rig.Session.TryStartWave();
        }

        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.BoonId).Count);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Boon]") && l.Contains("offered")), string.Join("\n", rig.Log.Lines.Where(l => l.Contains("Boon"))));
    }

    [TestMethod]
    public void AHornBoon_DoublesTheHorn_AndTheEarlyStartCheckFollowsTheFactor()
    {
        var only = new BoonPlan(new[] { Boon("horn", BoonCategory.WarChest, ("earlyGoldPercent", 100f)) }, 1);
        var rig = new Rig(only, stopAfter: 5, rerolls: 0);

        rig.RunToTheEnd();

        var early = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.EarlyStartId)).ToList();
        Assert.IsTrue(early.Count >= 4, "the bot sounds the horn every intermission");
        Assert.IsFalse(early.Any(l => l.StartsWith("fail", System.StringComparison.Ordinal)), string.Join("\n", early));
        Assert.AreEqual(1, rig.Outcomes.Of(LongDefeatChecks.BoonId).Count);
    }

    [TestMethod]
    public void ALegendCut_ShowsInTheAffixCheck_AndPassesIt()
    {
        var only = new BoonPlan(new[] { Boon("phial", BoonCategory.Legend, ("affixCount", -1f)) }, 1);
        var rig = new Rig(only, stopAfter: 4, rerolls: 0, startWave: 3, seed: 1);

        rig.RunToTheEnd();

        var lines = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.AffixId)).ToList();
        Assert.IsTrue(lines.Count >= 1, "wave 6 carries an affix before the cut, or none after it; at least wave 5 wrote its line");
        Assert.IsFalse(lines.Any(l => l.StartsWith("fail", System.StringComparison.Ordinal)), string.Join("\n", lines));
        Assert.IsTrue(lines.Any(l => l.Contains("cut=-1")), string.Join("\n", lines));
    }

    [TestMethod]
    public void TheReport_ListsThePicksAndTheBoonGold()
    {
        var only = new BoonPlan(new[] { Boon("gold", BoonCategory.WarChest, ("gold", 300f)) }, 1);
        var rig = new Rig(only, stopAfter: 4, rerolls: 0);

        rig.RunToTheEnd();

        var report = JObject.Parse(rig.Director.ReportText);
        var pick = (JObject)((JArray)report["boons"]!).Single();
        Assert.AreEqual("gold", (string?)pick["taken"]);
        Assert.AreEqual(3, (int)pick["wave"]!);
        Assert.AreEqual(300, (int)report["gold"]!["boon"]!);
    }

    [TestMethod]
    public void AShopPackBoughtWithAPackBonus_IsJudgedAgainstItsOwnOffer()
    {
        var only = new BoonPlan(new[] { Boon("wains", BoonCategory.Fortress, ("packBonus", 3f)) }, 1);
        var rig = new Rig(only, stopAfter: 5, rerolls: 0);

        rig.RunToTheEnd();

        var shop = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.ShopId)).ToList();
        Assert.IsFalse(shop.Any(l => l.StartsWith("fail", System.StringComparison.Ordinal)), string.Join("\n", shop));
        Assert.IsTrue(shop.Any(l => l.Contains(" militia ")), "the pack keeps its offer id: " + string.Join("\n", shop));
    }

    [TestMethod]
    public void ARunWithNoPool_LogsTheLapse_AndWritesNoBoonCheck()
    {
        var rig = new Rig(BoonPlan.None, stopAfter: 3, rerolls: 0);

        rig.RunToTheEnd();

        Assert.AreEqual(0, rig.Outcomes.Of(LongDefeatChecks.BoonId).Count);
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Boon]") && l.Contains("lapses")));
    }
}
