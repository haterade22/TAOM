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
/// The director's half of the mission heroes' recorded lines (#1569): a spawn the field took plays the hero's next line through the voice adapter and writes one
/// <c>hero-line</c> check, and nothing here can stop the spawn loop.
/// </summary>
[TestClass]
public class LongDefeatDirectorHeroLineTests
{
    private static readonly HeroOffer Aragorn = new("aragorn", "{=K_A}Aragorn", "", "taom_hero_aragorn", 300, 3f);
    private static readonly HeroOffer Legolas = new("legolas", "{=K_L}Legolas", "", "taom_hero_legolas", 250, 3f);

    private static readonly HeroLinePlan Lines = new(new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
    {
        ["legolas"] = new IReadOnlyList<string>[]
        {
            new[] { "M/Legolas/a_01.ogg" },
            new[] { "M/Legolas/b_01.ogg" },
            new[] { "M/Legolas/c_01.ogg" },
        },
    });

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool voice = true, HeroSpawn[]? free = null, HeroLinePlan? lines = null)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }) { Heroes = new[] { Aragorn, Legolas } },
            RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 60f, GarrisonCap = 220 },
            new RunOptions { Seed = 0UL })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            FreeHeroes = free ?? Array.Empty<HeroSpawn>(),
            HeroLines = lines ?? Lines,
        };
        var effects = new FakeLongDefeatEffects(rig.Field.Calls);
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, effects, rig.Field, null, voice ? rig.Field : null);
        return rig;
    }

    private static void Buy(Rig rig, string id)
    {
        Assert.AreEqual(HeroResult.Ok, rig.Session.TryBuyHero(id));
        rig.Director.Tick(0.1f);
    }

    private static Rig Started(bool voice = true)
    {
        var rig = NewRig(voice);
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        return rig;
    }

    private static List<string> LineChecks(Rig rig) => rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatHeroChecks.LineId)).ToList();

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void ASpawnTheFieldTook_PlaysTheFirstLine_AndRecordsAPass()
    {
        var rig = Started();

        Buy(rig, "legolas");

        CollectionAssert.AreEqual(new[] { "M/Legolas/a_01.ogg" }, rig.Field.HeroLinesPlayed);
        var line = LineChecks(rig).Single();
        StringAssert.StartsWith(line, "pass ");
        StringAssert.Contains(line, " m1");
        StringAssert.Contains(line, "hero=legolas");
        StringAssert.Contains(line, "file=a_01.ogg");
        Assert.IsFalse(line.Contains("M/Legolas"), "the detail names the file, not the full path");
    }

    [TestMethod]
    public void ASpawnTheFieldRefuses_PlaysNothing_AndRecordsNoLineCheck()
    {
        var rig = Started();
        rig.Field.HeroAnswer = new LdHeroReport(false, "no character 'taom_hero_legolas'");

        Buy(rig, "legolas");

        Assert.AreEqual(0, rig.Field.HeroLinesPlayed.Count);
        Assert.AreEqual(0, LineChecks(rig).Count);
    }

    [TestMethod]
    public void AHeroWithNoLines_PlaysNothing_AndRecordsNoLineCheck()
    {
        var rig = Started();

        Buy(rig, "aragorn");

        Assert.AreEqual(1, rig.Field.HeroSpawns.Count, "he was spawned");
        Assert.AreEqual(0, rig.Field.HeroLinesPlayed.Count);
        Assert.AreEqual(0, LineChecks(rig).Count);
    }

    [TestMethod]
    public void ASecondSpawnOfTheSameHero_PlaysTheSecondLine()
    {
        var rig = Started();
        Buy(rig, "legolas");
        rig.Director.OnAgentRemoved("H:legolas", true);
        rig.Director.Tick(0.1f);

        Buy(rig, "legolas");

        CollectionAssert.AreEqual(new[] { "M/Legolas/a_01.ogg", "M/Legolas/b_01.ogg" }, rig.Field.HeroLinesPlayed);
        Assert.AreEqual(2, LineChecks(rig).Count);
        Assert.AreEqual(2, LineChecks(rig).Select(l => l.Split(' ')[2]).Distinct().Count(), "each attempt has its own key");
    }

    [TestMethod]
    public void ARefusedSpawn_DoesNotUseUpALine()
    {
        var rig = Started();
        rig.Field.HeroAnswer = new LdHeroReport(false, "no home");
        Buy(rig, "legolas");

        Buy(rig, "legolas");

        CollectionAssert.AreEqual(new[] { "M/Legolas/a_01.ogg" }, rig.Field.HeroLinesPlayed);
    }

    [TestMethod]
    public void AnAdapterThatAnswersFalse_RecordsAFailNamingTheHeroAndFile_AndTheSpawnStillCounts()
    {
        var rig = Started();
        rig.Field.HeroLineAnswer = false;

        Buy(rig, "legolas");

        var line = LineChecks(rig).Single();
        StringAssert.StartsWith(line, "fail ");
        StringAssert.Contains(line, "hero=legolas");
        StringAssert.Contains(line, "file=a_01.ogg");
        Assert.IsFalse(line.Contains("M/Legolas"));
        Assert.AreEqual(HeroState.Standing, rig.Session.GetView()!.Heroes.Single(h => h.Offer.Id == "legolas").State);
        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.StartsWith("pass " + LongDefeatHeroChecks.SpawnId)), "the spawn check passed");
    }

    [TestMethod]
    public void AnAdapterThatThrows_RecordsAFail_LogsTheFault_AndTheNextHeroStillSpawns()
    {
        var free = new[] { new HeroSpawn("legolas", "taom_hero_legolas", 3f), new HeroSpawn("aragorn", "taom_hero_aragorn", 3f) };
        var rig = NewRig(free: free);
        rig.Field.HeroLineFault = new InvalidOperationException("sound engine down");
        Assert.IsTrue(rig.Director.Start());

        rig.Director.Tick(0.1f);

        Assert.AreEqual(2, rig.Field.HeroSpawns.Count, "both heroes spawned");
        var line = LineChecks(rig).Single();
        StringAssert.StartsWith(line, "fail ");
        StringAssert.Contains(line, "hero=legolas");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[LongDefeat]") && l.Contains("sound engine down")), string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void Shutdown_StopsTheLine()
    {
        var rig = Started();
        Buy(rig, "legolas");

        rig.Director.Shutdown();

        Assert.AreEqual(1, rig.Field.HeroLineStops);
    }

    [TestMethod]
    public void ADirectorWithNoVoiceAdapter_SpawnsAsBefore_AndWritesNoLineCheck()
    {
        var rig = Started(voice: false);

        Buy(rig, "legolas");

        Assert.AreEqual(1, rig.Field.HeroSpawns.Count);
        Assert.AreEqual(0, rig.Field.HeroLinesPlayed.Count);
        Assert.AreEqual(0, LineChecks(rig).Count);
        rig.Director.Shutdown();
        Assert.AreEqual(0, rig.Field.HeroLineStops);
    }

    [TestMethod]
    public void ADirectorWithNoSeed_StillPlaysALineOfTheFirstLinesTakes()
    {
        var lines = new HeroLinePlan(new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
        {
            ["legolas"] = new IReadOnlyList<string>[] { new[] { "M/a_01.ogg", "M/a_02.ogg", "M/a_03.ogg" } },
        });
        var rig = new Rig();
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default)
        {
            FreeHeroes = new[] { new HeroSpawn("legolas", "taom_hero_legolas", 3f) },
            HeroLines = lines,
        };
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, new FakeLongDefeatEffects(rig.Field.Calls), rig.Field, null, rig.Field);

        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        StringAssert.StartsWith(rig.Field.HeroLinesPlayed.Single(), "M/a_0");
    }

    [TestMethod]
    public void AHeroOfTheSideThePlayerAttacks_PlaysNothing_AndRecordsNoLineCheck()
    {
        var rig = NewRig(free: new[] { new HeroSpawn("legolas", "taom_hero_legolas", 3f) });
        rig.Field.PlayerAttacks = true;

        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        Assert.AreEqual(1, rig.Field.HeroSpawns.Count, "the enemy hero still spawns");
        Assert.AreEqual(0, rig.Field.HeroLinesPlayed.Count, "a recruitment line from an enemy is the wrong line");
        Assert.AreEqual(0, LineChecks(rig).Count);
    }
}
