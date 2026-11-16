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
/// The director's half of the shop's heroes (#1325): a bought hero is spawned on the next tick at the plan's home place, the run hears
/// whether the field took him, his fall is booked by his id, and each step writes its check line.
/// </summary>
[TestClass]
public class LongDefeatDirectorHeroTests
{
    private static readonly HeroOffer Aragorn = new("aragorn", "{=K_A}Aragorn", "", "taom_hero_aragorn", 300, 3f);

    private sealed class Rig
    {
        public FakeLongDefeatField Field { get; } = new() { RestHeals = true };
        public LongDefeatSession Session { get; } = new();
        public RecordingLogger Log { get; } = new();
        public RecordingOutcomeLog Outcomes { get; } = new();
        public LongDefeatDirector Director { get; set; } = null!;
    }

    private static Rig NewRig(bool keep = true, bool autoplay = false, HeroOffer[]? heroes = null, HeroSpawn[]? free = null)
    {
        var rig = new Rig();
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(60, new[] { new WaveTroop("orc", FormationSlot.Infantry, 75) }) }, new WaveScaling(10, 300)),
            new ShopPlan(3000, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }) { Heroes = heroes ?? new[] { Aragorn } },
            RunRules.Default with { PreparationSeconds = 50f, IntermissionSeconds = 60f, GarrisonCap = autoplay ? 31 : 220 },
            new RunOptions { Autoplay = autoplay })
        {
            Bounties = new Dictionary<string, int> { ["orc"] = 4 },
            FreeHeroes = free ?? System.Array.Empty<HeroSpawn>(),
        };
        if (!keep)
            rig.Field.Keep = null;
        var effects = new FakeLongDefeatEffects(rig.Field.Calls);
        rig.Director = new LongDefeatDirector(plan, rig.Field, rig.Session, rig.Log, rig.Outcomes, "m1", null, effects, rig.Field);
        return rig;
    }

    private static HeroState StateOf(Rig rig) => rig.Session.GetView()!.Heroes.Single().State;

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void ABoughtHero_IsSpawnedOnTheNextTick_AtTheHomePlace_AndStands()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        Assert.AreEqual(HeroResult.Ok, rig.Session.TryBuyHero("aragorn"));
        rig.Director.Tick(0.1f);

        var spawn = rig.Field.HeroSpawns.Single();
        Assert.AreEqual(new HeroSpawn("aragorn", "taom_hero_aragorn", 3f), spawn.Hero);
        Assert.AreEqual(LdPlace.At(rig.Field.Keep!.Value), spawn.Place, "no foot muster is recorded, so the keep is his home");
        Assert.AreEqual(HeroState.Standing, StateOf(rig));
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatHeroChecks.SpawnId).Single(), "m1h1 ");
        Assert.IsTrue(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.SpawnId)).StartsWith("pass "));
        rig.Director.Tick(0.1f);
        Assert.AreEqual(1, rig.Field.HeroSpawns.Count, "one spawn for one buy");
    }

    [TestMethod]
    public void ThreeHeroesBoughtInOneCalm_AreEachSpawnedAtTheHomePlace()
    {
        var heroes = new[]
        {
            Aragorn,
            new HeroOffer("legolas", "{=K_L}Legolas", "", "taom_hero_legolas", 250, 3f),
            new HeroOffer("gimli", "{=K_G}Gimli", "", "taom_hero_gimli", 250, 3f),
        };
        var rig = NewRig(heroes: heroes);
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        foreach (var hero in heroes)
            Assert.AreEqual(HeroResult.Ok, rig.Session.TryBuyHero(hero.Id));
        rig.Director.Tick(0.1f);

        var home = LdPlace.At(rig.Field.Keep!.Value);
        CollectionAssert.AreEqual(new[] { home, home, home }, rig.Field.HeroSpawns.Select(s => s.Place).ToList());
    }

    [TestMethod]
    public void TheHeroSpawn_FollowsTheDefenderBatchesOfItsTick_AndPrecedesTheWavesAttackers()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(HeroResult.Ok, rig.Session.TryBuyHero("aragorn"));
        rig.Session.TryStartWave();

        for (var i = 0; i < 6 && !rig.Field.Calls.Any(c => c.StartsWith("Spawn:att")); i++)
            rig.Director.Tick(0.1f);

        var calls = rig.Field.Calls;
        var defenders = calls.FindLastIndex(c => c.StartsWith("Spawn:def"));
        var hero = calls.IndexOf("SpawnHero");
        var attackers = calls.FindIndex(c => c.StartsWith("Spawn:att"));
        Assert.IsTrue(defenders >= 0 && hero >= 0 && attackers >= 0, string.Join(",", calls));
        Assert.IsTrue(defenders < hero, "after the defenders' batches: " + string.Join(",", calls));
        Assert.IsTrue(hero < attackers, "before the wave's attackers: " + string.Join(",", calls));
    }

    [TestMethod]
    public void UnderAutoplay_TheBotBuysTheHero_AndHeIsSpawned()
    {
        // 30 stand under a cap of 31: no pack of 12 fits, a hero does.
        var rig = NewRig(autoplay: true);
        Assert.IsTrue(rig.Director.Start());

        for (var i = 0; i < 4; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual("aragorn", rig.Field.HeroSpawns.Single().Hero.Id);
        Assert.AreEqual(HeroState.Standing, StateOf(rig));
    }

    [TestMethod]
    public void ASpawnTheFieldRefuses_IsPaidBack_SoldAgain_AndFailsItsCheck()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Field.HeroAnswer = new LdHeroReport(false, "no character 'taom_hero_aragorn'");

        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);

        Assert.AreEqual(HeroState.ForSale, StateOf(rig));
        Assert.AreEqual(3000, rig.Session.GetView()!.Gold);
        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.SpawnId));
        StringAssert.StartsWith(line, "fail ");
        StringAssert.Contains(line, "problem=no_character_'taom_hero_aragorn'");
    }

    [TestMethod]
    public void WithNoHomePlace_TheHeroIsNotSpawned_AndIsPaidBack()
    {
        // With no place the engine would lay him out from a deployment plan nobody built for his formation.
        var rig = NewRig(keep: false);
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);

        Assert.AreEqual(0, rig.Field.HeroSpawns.Count);
        Assert.AreEqual(HeroState.ForSale, StateOf(rig));
        StringAssert.Contains(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.SpawnId)), "problem=no_home_place");
    }

    [TestMethod]
    public void AHerosFall_SellsHimAgain_AndWritesTheFallLine()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);

        rig.Director.OnAgentRemoved("H:aragorn", true);
        rig.Director.Tick(0.1f);

        Assert.AreEqual(HeroState.ForSale, StateOf(rig));
        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.FallId));
        StringAssert.StartsWith(line, "pass ");
        StringAssert.Contains(line, "hero=aragorn cause=killed");
    }

    [TestMethod]
    public void AFreeHero_IsSpawnedOnTheFirstTick_AfterTheMainIsAdopted_AtTheHomePlace()
    {
        var free = new HeroSpawn("aragorn", "taom_hero_aragorn", 3f);
        var rig = NewRig(free: new[] { free });
        Assert.IsTrue(rig.Director.Start());
        Assert.AreEqual(0, rig.Field.HeroSpawns.Count, "Start spawns no hero");

        rig.Director.Tick(0.1f);

        var spawn = rig.Field.HeroSpawns.Single();
        Assert.AreEqual(free, spawn.Hero);
        Assert.AreEqual(LdPlace.At(rig.Field.Keep!.Value), spawn.Place, "no foot muster is recorded, so the keep is his home");
        Assert.AreEqual(HeroState.Standing, StateOf(rig));
        Assert.AreEqual(3000, rig.Session.GetView()!.Gold, "a free hero costs nothing");
        Assert.IsTrue(rig.Field.Calls.IndexOf("Adopt:False") >= 0 && rig.Field.Calls.IndexOf("Adopt:False") < rig.Field.Calls.IndexOf("SpawnHero"), string.Join(",", rig.Field.Calls));
        StringAssert.StartsWith(rig.Outcomes.Of(LongDefeatHeroChecks.SpawnId).Single(), "m1h1 ");
        rig.Director.Tick(0.1f);
        Assert.AreEqual(1, rig.Field.HeroSpawns.Count, "one spawn for one free hero");
    }

    [TestMethod]
    public void AFreeHerosFall_WritesTheFallLine()
    {
        // As in a story mission, the shop sells no hero (StoryPlanBuilder).
        var rig = NewRig(heroes: System.Array.Empty<HeroOffer>(), free: new[] { new HeroSpawn("aragorn", "taom_hero_aragorn", 3f) });
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        rig.Director.OnAgentRemoved("H:aragorn", true);
        rig.Director.Tick(0.1f);

        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.FallId));
        StringAssert.StartsWith(line, "pass ");
        StringAssert.Contains(line, "hero=aragorn cause=killed");
        Assert.AreEqual(1, rig.Field.HeroSpawns.Count, "nothing brings him back");
        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Hero] aragorn fell; a free hero does not come back")), string.Join("\n", rig.Log.Lines));
        Assert.IsFalse(rig.Log.Lines.Any(l => l.Contains("for sale again")), "the story mission has no shop");
    }

    [TestMethod]
    public void TheHeroLogTexts_NameASaleAndARefund_OnlyForAHeroTheShopSells()
    {
        Assert.AreEqual("aragorn fell; a hero of the shop is for sale again", LongDefeatDirector.HeroLeftText("aragorn", true, sold: true));
        Assert.AreEqual("aragorn fled; a hero of the shop is for sale again", LongDefeatDirector.HeroLeftText("aragorn", false, sold: true));
        Assert.AreEqual("aragorn fell; a free hero does not come back", LongDefeatDirector.HeroLeftText("aragorn", true, sold: false));
        Assert.AreEqual("a price he cost is paid back", LongDefeatDirector.HeroRefusedTail(sold: true));
        Assert.AreEqual("he was free, so nothing is paid back", LongDefeatDirector.HeroRefusedTail(sold: false));
    }

    [TestMethod]
    public void AFreeHeroTheFieldRefuses_LogsNoRefund()
    {
        var rig = NewRig(heroes: System.Array.Empty<HeroOffer>(), free: new[] { new HeroSpawn("aragorn", "taom_hero_aragorn", 3f) });
        rig.Field.HeroAnswer = new LdHeroReport(false, "no character 'taom_hero_aragorn'");
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        Assert.IsTrue(rig.Log.Lines.Any(l => l.Contains("[Hero] aragorn (taom_hero_aragorn) could not be spawned (no character 'taom_hero_aragorn'); he was free, so nothing is paid back")),
            string.Join("\n", rig.Log.Lines));
    }

    [TestMethod]
    public void ATakenOverFreeHerosFall_IsBookedAsHisFall_AndAsThePlayersDeath()
    {
        // #1366: in a campaign the fallen player can take over a free hero. Classify matches the held hero first, so the field reports that the body was the player's as well;
        // a player's death is counted, splits the defenders again (the engine hands every formation to the AI when the main agent falls, Mission.cs:3017, :3039-3041) and is
        // checked at the next body.
        var rig = NewRig(free: new[] { new HeroSpawn("aragorn", "taom_hero_aragorn", 3f) });
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Director.OnAgentRemoved("P", true);
        rig.Field.MainBody = 0;
        rig.Director.Tick(0.1f);
        rig.Field.MainIsHeldHero = true;
        rig.Field.MainBody = 2;
        rig.Director.Tick(0.1f);

        rig.Director.OnAgentRemoved("HP:aragorn", true);
        rig.Field.MainBody = 0;
        rig.Director.Tick(0.1f);
        rig.Field.MainIsHeldHero = false;
        rig.Field.MainBody = 3;
        rig.Director.Tick(0.1f);

        StringAssert.Contains(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.FallId)), "hero=aragorn cause=killed");
        var deaths = rig.Outcomes.Lines.Where(l => l.Contains(LongDefeatChecks.PlayerDeathId)).ToList();
        Assert.IsTrue(deaths.Any(l => l.StartsWith("pass " + LongDefeatChecks.PlayerDeathId + " d1 ") && l.Contains("via=takeover")), string.Join("\n", rig.Outcomes.Lines));
        Assert.IsTrue(deaths.Any(l => l.StartsWith("pass " + LongDefeatChecks.PlayerDeathId + " d2 ") && l.Contains("via=takeover")), string.Join("\n", rig.Outcomes.Lines));
    }

    [TestMethod]
    public void AHerosRout_SellsHimAgain_AsFled()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);

        rig.Director.OnAgentRemoved("H:aragorn", false);
        rig.Director.Tick(0.1f);

        Assert.AreEqual(HeroState.ForSale, StateOf(rig));
        StringAssert.Contains(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.FallId)), "hero=aragorn cause=fled");
    }

    private static void ClearOneWave(Rig rig)
    {
        rig.Session.TryStartWave();
        for (var i = 0; i < 400 && rig.Outcomes.Lines.All(l => !l.Contains(LongDefeatChecks.WaveId)); i++)
        {
            rig.Director.Tick(1f);
            rig.Field.AttackersStanding = 0;
        }
    }

    [TestMethod]
    public void AClearedWave_AfterAHeroSpawn_WritesOneHuntersLine_WithThatWavesNumber()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);
        rig.Field.HeroFacts = new List<LdHeroFacts> { new("aragorn", 4, HeroGroup.FormationIndex, 0.75f, 2f, 0f, Weapon: "TwoHandedSword") };

        ClearOneWave(rig);

        var line = rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.HuntersId));
        Assert.AreEqual("pass taom.long-defeat.hunters m1hw1 w=1 standing=1 in_group=1 units=1 held=1 order=Move own=1 settled=600 near=1 kills=4 by=aragorn:4 health=aragorn:0.75 weapons=aragorn:TwoHandedSword"
            + " retaken=0 moved_out=0 moved_back=0", line);
    }

    [TestMethod]
    public void AtAWaveClear_TheGroupIsHeld_BeforeItIsRead()
    {
        var rig = RigWithAragornStanding();

        ClearOneWave(rig);

        var read = rig.Field.Calls.IndexOf("ReadHeroes");
        Assert.IsTrue(read > 0, "the heroes were read");
        Assert.AreEqual("HoldHunters", rig.Field.Calls[read - 1], "a transfer in the frame of the clear is repaired before the line is written");
    }

    [TestMethod]
    public void AHoldAtTheWaveClear_IsCountedInThatWavesLine()
    {
        var rig = RigWithAragornStanding();
        rig.Field.HeroFacts = new List<LdHeroFacts> { new("aragorn", 0, HeroGroup.FormationIndex, 1f, 2f, 0f) };
        var before = HoldCalls(rig);
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(true, 0, 0);

        ClearOneWave(rig);

        var holdsUpToTheRead = rig.Field.Calls.Take(rig.Field.Calls.IndexOf("ReadHeroes")).Count(c => c == "HoldHunters") - before;
        StringAssert.EndsWith(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.HuntersId)), $" retaken={holdsUpToTheRead} moved_out=0 moved_back=0");
    }

    [TestMethod]
    public void TheHuntersLine_CarriesTheMissionsRunningTotalsOfTheHolds()
    {
        var rig = RigWithAragornStanding();
        rig.Field.HeroFacts = new List<LdHeroFacts> { new("aragorn", 0, HeroGroup.FormationIndex, 1f, 2f, 0f) };
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(true, 2, 1);
        rig.Director.Tick(0.1f);
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(false, 3, 0);
        rig.Director.Tick(0.1f);
        rig.Field.HoldHuntersAnswer = LdHuntersHold.Nothing;

        ClearOneWave(rig);

        StringAssert.EndsWith(rig.Outcomes.Lines.Single(l => l.Contains(LongDefeatHeroChecks.HuntersId)), " retaken=1 moved_out=5 moved_back=1");
    }

    [TestMethod]
    public void AMissionWithNoHeroSpawn_WritesNoHuntersLine_AndNeverReadsTheHeroes()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);

        ClearOneWave(rig);

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.WaveId)), "the wave cleared");
        Assert.AreEqual(0, rig.Outcomes.Lines.Count(l => l.Contains(LongDefeatHeroChecks.HuntersId)));
        Assert.IsFalse(rig.Field.Calls.Contains("ReadHeroes"));
    }

    private static int HoldCalls(Rig rig) => rig.Field.Calls.Count(c => c == "HoldHunters");

    private static Rig RigWithAragornStanding()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);
        return rig;
    }

    [TestMethod]
    public void BeforeAnyHeroSpawn_TheHeroesGroupIsNeverHeld()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        for (var i = 0; i < 3; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(0, HoldCalls(rig));
    }

    [TestMethod]
    public void AfterASuccessfulSpawn_TheHeroesGroupIsHeldOnEveryTick()
    {
        var rig = RigWithAragornStanding();
        var after = HoldCalls(rig);
        Assert.AreEqual(1, after, "the spawn's own tick holds the group once, after the spawn");

        rig.Director.Tick(0.1f);
        rig.Director.Tick(0.1f);

        Assert.AreEqual(after + 2, HoldCalls(rig));
    }

    [TestMethod]
    public void AfterOnlyARefusedSpawn_TheHeroesGroupIsNeverHeld_AndNoHuntersLineIsWritten()
    {
        var rig = NewRig();
        Assert.IsTrue(rig.Director.Start());
        rig.Director.Tick(0.1f);
        rig.Field.HeroAnswer = new LdHeroReport(false, "the engine returned no agent");
        rig.Session.TryBuyHero("aragorn");
        rig.Director.Tick(0.1f);
        rig.Director.Tick(0.1f);

        Assert.AreEqual(0, HoldCalls(rig));

        ClearOneWave(rig);

        Assert.IsTrue(rig.Outcomes.Lines.Any(l => l.Contains(LongDefeatChecks.WaveId)), "the wave cleared");
        Assert.AreEqual(0, rig.Outcomes.Lines.Count(l => l.Contains(LongDefeatHeroChecks.HuntersId)));
        Assert.IsFalse(rig.Field.Calls.Contains("ReadHeroes"));
        Assert.AreEqual(0, HoldCalls(rig));
    }

    [TestMethod]
    public void AGroupTakenBackFromTheAI_WritesOneLogLine_WithARunningCount()
    {
        var rig = RigWithAragornStanding();
        const string Line = "INFO [LongDefeat][Hero] the heroes' group was taken back from the AI";
        Assert.AreEqual(0, rig.Log.Lines.Count(l => l.StartsWith(Line)), "a false answer writes nothing");

        rig.Field.HoldHuntersAnswer = new LdHuntersHold(true, 0, 0);
        rig.Director.Tick(0.1f);
        rig.Director.Tick(0.1f);
        rig.Field.HoldHuntersAnswer = LdHuntersHold.Nothing;
        rig.Director.Tick(0.1f);

        var lines = rig.Log.Lines.Where(l => l.StartsWith(Line)).ToList();
        CollectionAssert.AreEqual(new[] { Line + " (1 so far)", Line + " (2 so far)" }, lines);
    }

    [TestMethod]
    public void EveryRegainIsLogged_PastTheFirstFive()
    {
        var rig = RigWithAragornStanding();
        const string Line = "INFO [LongDefeat][Hero] the heroes' group was taken back from the AI";
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(true, 0, 0);

        for (var i = 0; i < 7; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(7, rig.Log.Lines.Count(l => l.StartsWith(Line)));
    }

    [TestMethod]
    public void OnlyTheFirstFiveMembershipRepairs_AreLogged()
    {
        var rig = RigWithAragornStanding();
        const string Line = "INFO [LongDefeat][Hero] the heroes' group repaired:";
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(false, 2, 1);

        rig.Director.Tick(0.1f);
        Assert.AreEqual("INFO [LongDefeat][Hero] the heroes' group repaired: 2 soldier(s) moved out, 1 hero(es) moved back", rig.Log.Lines.Single(l => l.StartsWith(Line)));
        for (var i = 0; i < 6; i++)
            rig.Director.Tick(0.1f);

        Assert.AreEqual(5, rig.Log.Lines.Count(l => l.StartsWith(Line)));
    }

    [TestMethod]
    public void AHoldThatOnlyRegained_WritesNoRepairLine()
    {
        var rig = RigWithAragornStanding();
        rig.Field.HoldHuntersAnswer = new LdHuntersHold(true, 0, 0);

        rig.Director.Tick(0.1f);

        Assert.IsFalse(rig.Log.Lines.Any(l => l.Contains("the heroes' group repaired")));
    }

    [TestMethod]
    public void AfterTheLastHeroFalls_TheHoldStops()
    {
        var rig = RigWithAragornStanding();
        rig.Director.OnAgentRemoved("H:aragorn", true);
        rig.Director.Tick(0.1f);
        var after = HoldCalls(rig);

        rig.Director.Tick(0.1f);
        rig.Director.Tick(0.1f);

        Assert.AreEqual(after, HoldCalls(rig));
        Assert.AreEqual(1, after, "only the spawn's tick held the group: the fall is drained before that tick's hold");
    }
}
