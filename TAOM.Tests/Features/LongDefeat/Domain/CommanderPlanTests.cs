using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The commanders' schedule (phase2-plan.md section 2, "Commanders"): a commander every <c>every</c> waves, the rank by order, the steps once the ranks run out.</summary>
[TestClass]
public class CommanderPlanTests
{
    internal static CommanderAbility WarCry(float first = 20f, float cooldown = 60f) =>
        new("war_cry", CommanderAbilityKind.WarCry, "{=TAOM_LD_Ability_WarCry_Isengard}The White Hand Calls", first, cooldown)
        { DurationSeconds = 15f, SwingSpeedPercent = 15f, MoveSpeedPercent = 10f };

    internal static CommanderAbility Summon(float first = 30f, float cooldown = 120f, int count = 20) =>
        new("summon_uruks", CommanderAbilityKind.Summon, "{=TAOM_LD_Ability_Summon_Isengard}Uruk-hai of Isengard", first, cooldown)
        {
            Count = count,
            Mix = new[] { new WaveTroop("urukinf", FormationSlot.Infantry, 3), new WaveTroop("urukxbow", FormationSlot.Ranged, 1) },
        };

    internal static CommanderPlan Plan(int ranks = 3, int every = 4, CommanderBeyond? beyond = null)
    {
        var all = new[]
        {
            new CommanderRank("captain", "{=TAOM_LD_Rank_Captain}Captain", "lugdush", 150, 2f, new[] { "war_cry" }),
            new CommanderRank("lieutenant", "{=TAOM_LD_Rank_Lieutenant}Lieutenant", "ugluk", 250, 3f, new[] { "war_cry", "summon_uruks" }),
            new CommanderRank("general", "{=TAOM_LD_Rank_General}General", "lurtz", 400, 4f, new[] { "war_cry", "summon_uruks" }),
        };
        var abilities = new Dictionary<string, CommanderAbility> { ["war_cry"] = WarCry(), ["summon_uruks"] = Summon() };
        return new CommanderPlan(every, all.Take(ranks).ToList(), abilities, beyond ?? new CommanderBeyond(0.25f, 0.9f, 5, 10));
    }

    [TestMethod]
    public void ForWave_IsNullUnlessTheWaveIsAMultipleOfEvery()
    {
        var plan = Plan();

        foreach (var wave in new[] { -4, 0, 1, 2, 3, 5, 6, 7, 9, 11 })
            Assert.IsNull(plan.ForWave(wave), "wave " + wave);
        foreach (var wave in new[] { 4, 8, 12, 16, 40 })
            Assert.IsNotNull(plan.ForWave(wave), "wave " + wave);
    }

    [TestMethod]
    public void ANonePlan_NeverHasACommander()
    {
        Assert.IsNull(CommanderPlan.None.ForWave(4));
        Assert.IsNull(new CommanderPlan(4, new CommanderRank[0], new Dictionary<string, CommanderAbility>(), CommanderBeyond.None).ForWave(4));
        Assert.IsNull(new CommanderPlan(0, Plan().Ranks, Plan().Abilities, CommanderBeyond.None).ForWave(0));
    }

    [TestMethod]
    public void Waves4And8And12_TakeTheThreeRanksInOrder_AtFullStrength()
    {
        var plan = Plan();

        var captain = plan.ForWave(4)!;
        var lieutenant = plan.ForWave(8)!;
        var general = plan.ForWave(12)!;

        Assert.AreEqual(("captain", "lieutenant", "general"), (captain.Rank.Id, lieutenant.Rank.Id, general.Rank.Id));
        Assert.AreEqual((0, 0, 0), (captain.Steps, lieutenant.Steps, general.Steps), "steps use max(0, ...), so the first rounds get full strength");
        Assert.AreEqual((2f, 3f, 4f), (captain.Health, lieutenant.Health, general.Health), "D11: 2x, 3x, 4x");
        Assert.AreEqual(20, lieutenant.Abilities.Single(a => a.Kind == CommanderAbilityKind.Summon).Count, "the summon count is unchanged at steps 0");
        Assert.AreEqual(20, general.Abilities.Single(a => a.Kind == CommanderAbilityKind.Summon).Count);
        Assert.AreEqual(60f, general.Abilities.Single(a => a.Kind == CommanderAbilityKind.WarCry).CooldownSeconds);
    }

    [TestMethod]
    public void TheCaptainHasOnlyHisWarCry_TheOthersAlsoSummon()
    {
        var plan = Plan();

        CollectionAssert.AreEqual(new[] { "war_cry" }, plan.ForWave(4)!.Abilities.Select(a => a.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "war_cry", "summon_uruks" }, plan.ForWave(8)!.Abilities.Select(a => a.Id).ToArray());
    }

    [TestMethod]
    public void TheGeneralReturnsStrongerFromWave16_AndMoreSoFromWave20()
    {
        var plan = Plan();

        var at16 = plan.ForWave(16)!;
        var at20 = plan.ForWave(20)!;

        Assert.AreEqual(("general", 1, "general", 2), (at16.Rank.Id, at16.Steps, at20.Rank.Id, at20.Steps));
        Assert.AreEqual(4f * 1.25f, at16.Health, 1e-4f);
        Assert.AreEqual(4f * 1.5f, at20.Health, 1e-4f);
        Assert.AreEqual(25, at16.Abilities.Single(a => a.Kind == CommanderAbilityKind.Summon).Count);
        Assert.AreEqual(30, at20.Abilities.Single(a => a.Kind == CommanderAbilityKind.Summon).Count);
        Assert.AreEqual(54f, at16.Abilities.Single(a => a.Kind == CommanderAbilityKind.WarCry).CooldownSeconds, 1e-3f);
        Assert.AreEqual(60f * 0.81f, at20.Abilities.Single(a => a.Kind == CommanderAbilityKind.WarCry).CooldownSeconds, 1e-3f);
    }

    [TestMethod]
    public void ValidRanks_ShiftWhereTheStepsBegin()
    {
        var plan = Plan(ranks: 2);

        var at12 = plan.ForWave(12)!;

        Assert.AreEqual(("lieutenant", 1), (at12.Rank.Id, at12.Steps), "with two valid ranks the third commander is the second rank, one step on");
        Assert.AreEqual(0, plan.ForWave(8)!.Steps);
    }

    [TestMethod]
    public void TheSteps_StopAtMaxSteps()
    {
        var plan = Plan(beyond: new CommanderBeyond(0.25f, 0.9f, 5, 2));

        Assert.AreEqual(2, plan.ForWave(200)!.Steps);
        Assert.AreEqual(4f * 1.5f, plan.ForWave(200)!.Health, 1e-4f);
    }

    [TestMethod]
    public void ACooldown_NeverFallsUnderFiveSeconds_AndASummonNeverPassesFifty()
    {
        var abilities = new Dictionary<string, CommanderAbility> { ["war_cry"] = WarCry(first: 1f, cooldown: 60f), ["summon_uruks"] = Summon(first: 1f, cooldown: 8f, count: 48) };
        var plan = new CommanderPlan(4, Plan().Ranks, abilities, new CommanderBeyond(0f, 0.5f, 5, 10));

        var late = plan.ForWave(60)!;

        Assert.AreEqual(CommanderPlan.MinCooldownSeconds, late.Abilities.Single(a => a.Id == "summon_uruks").CooldownSeconds);
        Assert.AreEqual(50, late.Abilities.Single(a => a.Id == "summon_uruks").Count);
    }

    [TestMethod]
    public void AWarCrysCooldown_StaysAboveItsDuration_HoweverManyStepsShortenIt()
    {
        // The reader requires duration < cooldown on the raw value; Fire overwrites the war cry's end, so a stepped cooldown at or under the duration would keep it up for good.
        var abilities = new Dictionary<string, CommanderAbility> { ["war_cry"] = WarCry(first: 1f, cooldown: 60f), ["summon_uruks"] = Summon() };
        var plan = new CommanderPlan(4, Plan().Ranks, abilities, new CommanderBeyond(0f, 0.1f, 5, 10));

        var late = plan.ForWave(60)!.Abilities.Single(a => a.Id == "war_cry");

        Assert.IsTrue(late.CooldownSeconds >= late.DurationSeconds + CommanderPlan.MinCooldownSeconds, "cooldown " + late.CooldownSeconds + " vs duration " + late.DurationSeconds);
    }

    [TestMethod]
    public void TroopIds_ListEveryRankTroopAndEverySummonTroop_EachOnce()
    {
        var ids = Plan().TroopIds.ToList();

        CollectionAssert.AreEquivalent(new[] { "lugdush", "ugluk", "lurtz", "urukinf", "urukxbow" }, ids);
    }

    [TestMethod]
    public void ARankNamingAnUnknownAbility_JustLacksIt()
    {
        var ranks = new[] { new CommanderRank("captain", "Captain", "lugdush", 10, 2f, new[] { "war_cry", "nonsense" }) };
        var plan = new CommanderPlan(4, ranks, new Dictionary<string, CommanderAbility> { ["war_cry"] = WarCry() }, CommanderBeyond.None);

        CollectionAssert.AreEqual(new[] { "war_cry" }, plan.ForWave(4)!.Abilities.Select(a => a.Id).ToArray());
    }
}
