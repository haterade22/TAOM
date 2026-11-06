using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The affix schedule and the seeded roll (phase2-plan.md section 2, "Affixes"). The pinned rolls are the same rows as
/// <c>tools/tests/test_run_long_defeat.py</c> (<c>test_affix_roll_*</c>), taken from an independent Python implementation, so the bot's
/// replay cannot drift from the game.
/// </summary>
[TestClass]
public class AffixPlanTests
{
    private static readonly IReadOnlyList<AffixStage> Shipped = new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) };

    private static AffixPlan Plan(params AffixStage[] stages) =>
        new(stages.Length == 0 ? Shipped : stages, new Dictionary<string, IReadOnlyDictionary<string, float>>(), new Dictionary<string, string>());

    [DataTestMethod]
    [DataRow(1, 0)]
    [DataRow(4, 0)]
    [DataRow(5, 1)]
    [DataRow(9, 1)]
    [DataRow(10, 2)]
    [DataRow(14, 2)]
    [DataRow(15, 3)]
    [DataRow(40, 3)]
    public void CountFor_IsTheLastScheduleRowAtOrBelowTheWave(int wave, int expected)
    {
        Assert.AreEqual(expected, Plan().CountFor(wave));
    }

    [TestMethod]
    public void CountFor_WithNoSchedule_IsZeroEverywhere()
    {
        Assert.AreEqual(0, AffixPlan.None.CountFor(1));
        Assert.AreEqual(0, AffixPlan.None.CountFor(99));
        Assert.AreEqual(0, AffixPlan.None.Roll(7, 99).Count);
    }

    [TestMethod]
    public void Ids_AreTheEightPowersInTheFixedOrder()
    {
        CollectionAssert.AreEqual(
            new[] { "lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades" },
            AffixPlan.Ids.ToArray());
    }

    [TestMethod]
    public void Pool_IsThePowersWithAnEffect_InTheOrderOfTheEight()
    {
        // Slice 8 added hammer and terror, Slice 9 added blades: each switched on here when its effect landed, and the pinned rolls below changed with it.
        CollectionAssert.AreEqual(new[] { "lash", "drums", "eye", "discipline", "hide", "hammer", "terror", "blades" }, AffixPlan.Pool.ToArray());
        CollectionAssert.AreEqual(AffixPlan.Ids.ToArray(), AffixPlan.Pool.ToArray(), "all eight powers roll now");
        CollectionAssert.AreEqual(AffixPlan.Ids.Where(AffixPlan.Pool.Contains).ToList(), AffixPlan.Pool.ToList());
    }

    [TestMethod]
    public void Roll_BeforeTheFirstScheduledWave_IsEmpty()
    {
        Assert.AreEqual(0, Plan().Roll(1, 4).Count);
    }

    [DataTestMethod]
    [DataRow(1UL, 5, "hide")]
    [DataRow(1UL, 10, "drums,terror")]
    [DataRow(1UL, 15, "hide,hammer,blades")]
    [DataRow(12345UL, 40, "drums,discipline,blades")]
    [DataRow(7UL, 5, "terror")]
    [DataRow(0UL, 15, "lash,drums,discipline")]
    public void Roll_PinnedSeedsAndWaves_GiveThePinnedPowers(ulong seed, int wave, string expected)
    {
        Assert.AreEqual(expected, string.Join(",", Plan().Roll(seed, wave)));
    }

    [TestMethod]
    public void Roll_FindsSeedsForEveryPowerWithAnEffectCheckAtWaveFive()
    {
        // Seeds the in-game proof arms (--start-wave 5 --waves 1 --seed N), the same as tools/ingame_checks/1079.toml: one for each power the affix-effect check reads.
        Assert.AreEqual("eye", Plan().Roll(0, 5).Single());
        Assert.AreEqual("hide", Plan().Roll(1, 5).Single());
        Assert.AreEqual("lash", Plan().Roll(2, 5).Single());
        Assert.AreEqual("blades", Plan().Roll(3, 5).Single());
        Assert.AreEqual("discipline", Plan().Roll(5, 5).Single());
        Assert.AreEqual("terror", Plan().Roll(7, 5).Single());
        Assert.AreEqual("hammer", Plan().Roll(9, 5).Single());
        Assert.AreEqual("drums", Plan().Roll(15, 5).Single());
    }

    [TestMethod]
    public void Roll_OneSeed_GivesOneRoll_EveryTime()
    {
        CollectionAssert.AreEqual(Plan().Roll(99, 12).ToList(), Plan().Roll(99, 12).ToList());
    }

    [TestMethod]
    public void Roll_AShorterCountIsAPrefixOfALongerOne()
    {
        // The cut at wave start (a Legend boon) takes a prefix of the stored roll, so the draws must not depend on the count asked for.
        var three = Plan(new AffixStage(1, 3)).Roll(1, 15);
        var two = Plan(new AffixStage(1, 2)).Roll(1, 15);
        var one = Plan(new AffixStage(1, 1)).Roll(1, 15);

        CollectionAssert.AreEqual(three.Take(2).ToList(), two.ToList());
        CollectionAssert.AreEqual(three.Take(1).ToList(), one.ToList());
    }

    [TestMethod]
    public void Roll_NeverRepeatsAPower_AndOnlyNamesPowersOfThePool()
    {
        var plan = Plan(new AffixStage(1, 8));
        for (ulong seed = 0; seed < 200; seed++)
        {
            var roll = plan.Roll(seed, 20);
            Assert.AreEqual(AffixPlan.Pool.Count, roll.Count, "a schedule asking for more than the pool holds rolls the whole pool");
            Assert.AreEqual(roll.Count, roll.Distinct().Count(), "seed " + seed);
            Assert.IsTrue(roll.All(AffixPlan.Pool.Contains), "seed " + seed);
        }
    }

    [TestMethod]
    public void Roll_NamesEveryPowerOfTheEight_NowThatEachEffectHasLanded()
    {
        // A power with no effect would be a named wave that does nothing; each of the eight now acts (blades since Slice 9), so each can roll.
        var plan = Plan(new AffixStage(1, 3));
        var seen = Enumerable.Range(0, 200).SelectMany(seed => plan.Roll((ulong)seed, 20)).ToHashSet();

        CollectionAssert.AreEquivalent(AffixPlan.Ids.ToList(), seen.ToList());
    }

    [TestMethod]
    public void CountFor_IsHeldToWhatThePoolCanFill()
    {
        Assert.AreEqual(AffixPlan.Pool.Count, Plan(new AffixStage(1, 8)).CountFor(3));
    }

    [TestMethod]
    public void Roll_DrawsFromTheAffixStream_SoAnotherPurposeNeverShiftsIt()
    {
        // Stream 1, wave as the first index: the roll of wave 5 equals a hand-run partial Fisher-Yates over the pool from RunRandom.For(seed, 1, 5, 0).
        var random = RunRandom.For(1, RunRandom.AffixStream, 5, 0);
        var ids = AffixPlan.Pool.ToArray();
        var j = random.Below(ids.Length);
        (ids[0], ids[j]) = (ids[j], ids[0]);

        Assert.AreEqual(ids[0], Plan().Roll(1, 5).Single());
    }

    [TestMethod]
    public void Value_ReadsAPowersNumber_AndZeroWhenAbsent()
    {
        var powers = new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f },
        };
        var plan = new AffixPlan(Shipped, powers, new Dictionary<string, string>());

        Assert.AreEqual(15f, plan.Value("lash", "swingSpeedPercent"));
        Assert.AreEqual(0f, plan.Value("lash", "attackEagernessPercent"));
        Assert.AreEqual(0f, plan.Value("drums", "moveSpeedPercent"));
    }

    [TestMethod]
    public void NameOf_IsTheCulturesName_OrNullWhenTheTableLacksIt()
    {
        var plan = new AffixPlan(Shipped, new Dictionary<string, IReadOnlyDictionary<string, float>>(),
            new Dictionary<string, string> { ["lash"] = "{=K}The Lash" });

        Assert.AreEqual("{=K}The Lash", plan.NameOf("lash"));
        Assert.IsNull(plan.NameOf("drums"));
    }
}
