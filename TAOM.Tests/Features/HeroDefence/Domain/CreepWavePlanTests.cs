using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroDefence.Domain;
using static TAOM.Tests.Features.HeroDefence.Domain.WaveFixture;

namespace TAOM.Tests.Features.HeroDefence.Domain;

[TestClass]
public class CreepWavePlanTests
{
    private static CultureDef[] Many(int count, string signature = "wall") =>
        Enumerable.Range(0, count).Select(i => Culture("c" + i, signature)).ToArray();

    private static CreepWavePlan Plan(params CultureDef[] cultures) => new(Catalog(cultures), 7UL);

    /// <summary>Every value of a brief in one line, so two briefs compare in full.</summary>
    private static string Describe(WaveBrief b) =>
        $"{b.Wave} {b.CultureId} {b.LevelLow}-{b.LevelHigh} h{b.HealthFactor} d{b.DamageFactor} a{b.ArmourPercent} s{b.SwingPercent} l{b.LeaderHealthFactor} " +
        $"p[{string.Join(",", b.PowerIds)}] " +
        string.Join("|", b.Packs.Select(p => $"{p.Class}+{p.HealthBonus}/{p.DamageBonus}:" + string.Join(",", p.Creeps.Select(c => $"{c.TroopId}@{c.Level}{c.Rank}{c.Mounted}"))));

    [TestMethod]
    public void Brief_AnOpeningCultureThatHasNotEntered_DrawsInstead()
    {
        var late = Culture("elves", roster: new[] { Row("e", 36) });
        var catalog = Catalog(Many(3).Append(late).ToArray()) with { Opening = new[] { "elves" } };

        var brief = new CreepWavePlan(catalog, 7UL).Brief(1);

        Assert.AreNotEqual("elves", brief.CultureId);
        Assert.AreEqual(30, brief.Creeps().Count());
    }

    [TestMethod]
    public void Brief_AnotherSignatureHasEntered_TheSignatureNeverRepeats()
    {
        var cultures = new[] { Culture("a", "wall"), Culture("b", "wall"), Culture("x", "horse") };
        for (var seed = 0UL; seed < 40UL; seed++)
        {
            var plan = new CreepWavePlan(Catalog(cultures), seed);
            for (var wave = 2; wave <= 30; wave++)
            {
                var before = cultures.Single(c => c.Id == plan.Brief(wave - 1).CultureId).Signature;
                var now = cultures.Single(c => c.Id == plan.Brief(wave).CultureId).Signature;
                Assert.AreNotEqual(before, now, $"seed {seed}, wave {wave}");
            }
        }
    }

    [TestMethod]
    public void Brief_OpeningWaves_AreThePinnedCultures()
    {
        var catalog = Catalog(Many(6)) with { Opening = new[] { "c4", "c2" } };
        var plan = new CreepWavePlan(catalog, 7UL);

        Assert.AreEqual("c4", plan.Brief(1).CultureId);
        Assert.AreEqual("c2", plan.Brief(2).CultureId);
    }

    [TestMethod]
    public void Brief_AnOpeningCultureThatIsUnknown_DrawsInstead()
    {
        var catalog = Catalog(Many(6)) with { Opening = new[] { "nobody" } };

        var brief = new CreepWavePlan(catalog, 7UL).Brief(1);

        StringAssert.StartsWith(brief.CultureId, "c");
    }

    [DataTestMethod]
    [DataRow(1UL)]
    [DataRow(7UL)]
    [DataRow(99UL)]
    public void Brief_NeverRepeatsTheCultureOfTheWaveBefore(ulong seed)
    {
        var plan = new CreepWavePlan(Catalog(Many(4)), seed);

        for (var wave = 2; wave <= 60; wave++)
            Assert.AreNotEqual(plan.Brief(wave - 1).CultureId, plan.Brief(wave).CultureId, $"wave {wave}");
    }

    [TestMethod]
    public void Brief_EveryCultureAppearsBeforeOneReturns()
    {
        var plan = Plan(Many(6));

        var firstSix = Enumerable.Range(1, 6).Select(w => plan.Brief(w).CultureId).ToList();

        Assert.AreEqual(6, firstSix.Distinct().Count());
    }

    [TestMethod]
    public void Brief_OpeningCultureCountsAsDrawnInTheFirstCycle()
    {
        var catalog = Catalog(Many(4)) with { Opening = new[] { "c1" } };
        var plan = new CreepWavePlan(catalog, 7UL);

        var firstFour = Enumerable.Range(1, 4).Select(w => plan.Brief(w).CultureId).ToList();

        Assert.AreEqual(4, firstFour.Distinct().Count());
    }

    [TestMethod]
    public void Brief_PrefersADifferentSignature()
    {
        var cultures = new[] { Culture("a", "wall"), Culture("b", "wall"), Culture("x", "horse"), Culture("y", "horse") };
        var plan = new CreepWavePlan(Catalog(cultures), 3UL);

        for (var wave = 2; wave <= 4; wave++)
        {
            var before = cultures.Single(c => c.Id == plan.Brief(wave - 1).CultureId);
            var now = cultures.Single(c => c.Id == plan.Brief(wave).CultureId);
            Assert.AreNotEqual(before.Signature, now.Signature, $"wave {wave}");
        }
    }

    [TestMethod]
    public void Brief_OnlyTheSameSignatureIsLeft_StillChangesTheCulture()
    {
        var plan = Plan(Many(3, "wall"));

        Assert.AreNotEqual(plan.Brief(1).CultureId, plan.Brief(2).CultureId);
    }

    [TestMethod]
    public void Brief_OneCultureOnly_RepeatsItAndDoesNotHang()
    {
        var plan = Plan(Culture("only"));

        Assert.AreEqual("only", plan.Brief(3).CultureId);
    }

    [TestMethod]
    public void Brief_SameSeed_GivesTheSameWaves()
    {
        var a = new CreepWavePlan(Catalog(Many(9)), 5UL);
        var b = new CreepWavePlan(Catalog(Many(9)), 5UL);

        for (var wave = 1; wave <= 20; wave++)
        {
            Assert.AreEqual(a.Brief(wave).CultureId, b.Brief(wave).CultureId);
            CollectionAssert.AreEqual(a.Brief(wave).Creeps().Select(c => c.TroopId).ToList(), b.Brief(wave).Creeps().Select(c => c.TroopId).ToList());
        }
    }

    [TestMethod]
    public void Brief_DifferentSeeds_GiveDifferentRotations()
    {
        var a = new CreepWavePlan(Catalog(Many(9)), 5UL);
        var b = new CreepWavePlan(Catalog(Many(9)), 6UL);

        var rotationA = Enumerable.Range(1, 9).Select(w => a.Brief(w).CultureId).ToList();
        var rotationB = Enumerable.Range(1, 9).Select(w => b.Brief(w).CultureId).ToList();

        CollectionAssert.AreNotEqual(rotationA, rotationB);
    }

    [TestMethod]
    public void Brief_AskedOutOfOrder_MatchesTheInOrderResult()
    {
        var inOrder = new CreepWavePlan(Catalog(Many(9)), 5UL);
        var expected = Enumerable.Range(1, 12).Select(w => Describe(inOrder.Brief(w))).ToList();

        var outOfOrder = new CreepWavePlan(Catalog(Many(9)), 5UL);
        var preview = Describe(outOfOrder.Brief(12));

        Assert.AreEqual(expected[11], preview);
        Assert.AreEqual(expected[3], Describe(outOfOrder.Brief(4)));
    }

    [TestMethod]
    public void Brief_ACultureWhoseInfantryStartsLate_WaitsUntilTheBandReachesIt()
    {
        var late = Culture("elves", roster: new[] { Row("e", 36) });
        var plan = Plan(Many(30).Append(late).ToArray());

        var early = Enumerable.Range(1, 12).Select(w => plan.Brief(w).CultureId);

        CollectionAssert.DoesNotContain(early.ToList(), "elves");
    }

    [TestMethod]
    public void Brief_ACultureWhoseInfantryLevelIsReached_CanBeDrawn()
    {
        var late = Culture("elves", roster: new[] { Row("e", 36) });
        var plan = Plan(Culture("a"), late);

        Assert.AreEqual("elves", plan.Brief(13).CultureId, "wave 13 has the band 31-36, and elves are the only other culture");
    }

    [DataTestMethod]
    [DataRow(1, 1, 6)]
    [DataRow(2, 11, 11)]
    [DataRow(4, 11, 16)]
    [DataRow(9, 21, 26)]
    [DataRow(24, 46, 51)]
    [DataRow(200, 46, 51)]
    public void Brief_LevelBand_FollowsTheWave(int wave, int low, int high)
    {
        var brief = Plan(Many(3)).Brief(wave);

        Assert.AreEqual(low, brief.LevelLow);
        Assert.AreEqual(high, brief.LevelHigh);
    }

    [DataTestMethod]
    [DataRow(1, 1f, 30)]
    [DataRow(5, 1f, 54)]
    [DataRow(1, 0.75f, 23)]
    [DataRow(1, 1.25f, 38)]
    [DataRow(1, 0.01f, 1)]
    [DataRow(30, 1f, 180)]
    [DataRow(30, 1.25f, 180)]
    [DataRow(100, 1f, 180)]
    public void Brief_Count_FollowsTheWaveAndFactorUpToTheCap(int wave, float factor, int count)
    {
        var plan = Plan(Culture("a", countFactor: factor), Culture("b", countFactor: factor));

        Assert.AreEqual(count, plan.Brief(wave).Creeps().Count());
    }

    [TestMethod]
    public void Brief_Mix_SplitsTheCountByWeight()
    {
        var culture = Culture("a", "wall", Mix(60, 25, 15), roster: Ladder("i").Concat(Ladder("r", CreepClass.Ranged)).Concat(Ladder("c", CreepClass.Cavalry)).ToArray());
        var counts = Plan(culture).Brief(1).Packs.GroupBy(p => p.Class).ToDictionary(g => g.Key, g => g.Sum(p => p.Creeps.Count));

        Assert.AreEqual(18, counts[CreepClass.Infantry]);
        Assert.AreEqual(8, counts[CreepClass.Ranged], "7.5 takes the largest remainder");
        Assert.AreEqual(4, counts[CreepClass.Cavalry]);
    }

    [TestMethod]
    public void Brief_AClassWithoutRows_MovesItsWeightToInfantry()
    {
        var culture = Culture("a", "wall", Mix(50, 0, 50), roster: Ladder("i"));

        var brief = Plan(culture).Brief(1);

        Assert.AreEqual(30, brief.Creeps().Count());
        Assert.IsTrue(brief.Packs.All(p => p.Class == CreepClass.Infantry));
    }

    [TestMethod]
    public void Brief_TwoLevelsInTheBand_GivesTheHighLevelSixtyPercent()
    {
        var culture = Culture("a", roster: new[] { Row("low", 1), Row("high", 6) });

        var creeps = Plan(culture).Brief(1).Creeps().ToList();

        Assert.AreEqual(18, creeps.Count(c => c.Level == 6));
        Assert.AreEqual(12, creeps.Count(c => c.Level == 1));
    }

    [TestMethod]
    public void Brief_OneLevelInTheBand_UsesItForAll()
    {
        var plan = Plan(Culture("a", roster: new[] { Row("x", 11), Row("low", 1) }), Culture("b", roster: new[] { Row("y", 11), Row("low2", 1) }));

        var creeps = plan.Brief(2).Creeps().ToList();

        Assert.AreEqual(36, creeps.Count);
        Assert.IsTrue(creeps.All(c => c.Level == 11));
    }

    [TestMethod]
    public void Brief_ManyTroopsAtOneLevel_UsesAtMostTwo()
    {
        var rows = Enumerable.Range(0, 9).Select(i => Row("t" + i, 6)).ToArray();

        var brief = Plan(Culture("a", roster: rows)).Brief(1);

        Assert.AreEqual(2, brief.Creeps().Select(c => c.TroopId).Distinct().Count());
    }

    [TestMethod]
    public void Brief_ThreeLevelsInTheBand_SplitsTheRestEvenlyAndKeepsTheTotal()
    {
        var culture = Culture("a", roster: new[] { Row("a", 1), Row("b", 3), Row("c", 6) });

        var creeps = Plan(culture).Brief(1).Creeps().ToList();

        Assert.AreEqual(18, creeps.Count(c => c.Level == 6));
        Assert.AreEqual(6, creeps.Count(c => c.Level == 3));
        Assert.AreEqual(6, creeps.Count(c => c.Level == 1));
    }

    [TestMethod]
    public void Brief_NoRowInTheBand_UsesTheNearestLowerLevelWithinFifteen()
    {
        var culture = Culture("a", "wall", Mix(50, 50), roster: Ladder("i").Concat(new[] { Row("r5", 6, CreepClass.Ranged), Row("r2", 2, CreepClass.Ranged) }).ToArray());

        var plan = new CreepWavePlan(Catalog(culture) with { Opening = new[] { "a", "a", "a", "a", "a" } }, 7UL);

        var ranged = plan.Brief(5).Packs.Where(p => p.Class == CreepClass.Ranged).SelectMany(p => p.Creeps).ToList();

        Assert.IsTrue(ranged.Count > 0);
        Assert.IsTrue(ranged.All(c => c.TroopId == "r5"), "band 16-21: level 6 is nearest and within 15 of 16");
    }

    [TestMethod]
    public void Brief_TheNearestLowerRowIsOverFifteenAway_MovesTheWeightToInfantry()
    {
        var culture = Culture("a", "wall", Mix(50, 50), roster: Ladder("i").Concat(new[] { Row("r1", 1, CreepClass.Ranged) }).ToArray());
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 7).ToArray() }, 7UL);

        var brief = plan.Brief(7);

        Assert.AreEqual(66, brief.Creeps().Count());
        Assert.IsTrue(brief.Packs.All(p => p.Class == CreepClass.Infantry), "band 21-26: level 1 is 20 below");
    }

    [TestMethod]
    public void Brief_OnlyHigherRowsExist_NeverReachesUpward()
    {
        var culture = Culture("a", "wall", Mix(50, 50), roster: Ladder("i").Concat(new[] { Row("r51", 51, CreepClass.Ranged) }).ToArray());
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = new[] { "a" } }, 7UL);
        var brief = plan.Brief(1);

        Assert.AreEqual(30, brief.Creeps().Count());
        Assert.IsTrue(brief.Packs.All(p => p.Class == CreepClass.Infantry));
    }

    [TestMethod]
    public void Brief_InfantryTopsOutFarBelowTheBand_ClampsToTheTopRows()
    {
        var culture = Culture("a", roster: new[] { Row("low", 1), Row("top", 26) });
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 25).ToArray() }, 7UL);
        var creeps = plan.Brief(25).Creeps().ToList();

        Assert.AreEqual(174, creeps.Count);
        Assert.IsTrue(creeps.All(c => c.TroopId == "top"), "band 46-51 is over 15 above level 26");
    }

    [TestMethod]
    public void Brief_InfantryTopsOutBelowTheBand_OnlyItsPacksCarryAGapBonus()
    {
        var culture = Culture("a", "wall", Mix(50, 0, 50), roster: new[] { Row("top", 26) }.Concat(Ladder("c", CreepClass.Cavalry)).ToArray());
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 24).ToArray() }, 7UL);

        var brief = plan.Brief(24);

        Assert.AreEqual(1f + 23 * 0.05f, brief.HealthFactor, 0.001f, "the gap is not wave-wide");
        Assert.AreEqual(1f + 23 * 0.03f, brief.DamageFactor, 0.001f);
        var foot = brief.Packs.Where(p => p.Class == CreepClass.Infantry).ToList();
        var riders = brief.Packs.Where(p => p.Class == CreepClass.Cavalry).ToList();
        Assert.IsTrue(foot.Count > 0 && riders.Count > 0);
        Assert.IsTrue(foot.All(p => System.Math.Abs(p.HealthBonus - 25 * 0.015f) < 0.001f && System.Math.Abs(p.DamageBonus - 25 * 0.01f) < 0.001f), "51 - 26 = 25 levels");
        Assert.IsTrue(riders.All(p => p.HealthBonus == 0f && p.DamageBonus == 0f));
    }

    [TestMethod]
    public void Brief_InfantryInTheBandBelowItsHigh_CarriesNoGapBonus()
    {
        var culture = Culture("a", roster: new[] { Row("top", 46) });
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 24).ToArray() }, 7UL);

        var brief = plan.Brief(24);

        Assert.AreEqual(1f + 23 * 0.05f, brief.HealthFactor, 0.001f);
        Assert.IsTrue(brief.Packs.Count > 0);
        Assert.IsTrue(brief.Packs.All(p => p.HealthBonus == 0f && p.DamageBonus == 0f), "level 46 is in the band 46-51");
    }

    [TestMethod]
    public void Brief_InfantryReachesTheBandHigh_PaysNoGapBonus()
    {
        var brief = Plan(Many(3)).Brief(24);

        Assert.AreEqual(1f + 23 * 0.05f, brief.HealthFactor, 0.001f);
        Assert.IsTrue(brief.Packs.All(p => p.HealthBonus == 0f && p.DamageBonus == 0f));
    }

    [TestMethod]
    public void Brief_Packs_AreSingleClassAndAtMostThePackSize()
    {
        var culture = Culture("a", "wall", Mix(50, 50), roster: Ladder("i").Concat(Ladder("r", CreepClass.Ranged)).ToArray());

        var brief = Plan(culture).Brief(1);

        Assert.AreEqual(2, brief.Packs.Count(p => p.Class == CreepClass.Infantry) + brief.Packs.Count(p => p.Class == CreepClass.Ranged));
        Assert.IsTrue(brief.Packs.All(p => p.Creeps.Count <= 20));

        var big = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 10).ToArray() }, 7UL).Brief(10);
        Assert.IsTrue(big.Packs.All(p => p.Creeps.Count <= 20));
        Assert.AreEqual(84, big.Creeps().Count());
    }

    [TestMethod]
    public void Brief_FixedSignature_ReleasesInItsArrivalOrder()
    {
        var culture = Culture("a", "horse", Mix(50, 0, 50), roster: Ladder("i").Concat(Ladder("c", CreepClass.Cavalry)).ToArray());

        var brief = Plan(culture).Brief(1);

        Assert.AreEqual(CreepClass.Cavalry, brief.Packs[0].Class);
        Assert.AreEqual(CreepClass.Infantry, brief.Packs.Last().Class);
    }

    [TestMethod]
    public void Brief_ShuffledSignature_OrderDependsOnTheSeedOnly()
    {
        var culture = Culture("a", "horde", Mix(25, 25, 25, 25), roster: Ladder("i").Concat(Ladder("r", CreepClass.Ranged)).Concat(Ladder("c", CreepClass.Cavalry)).Concat(Ladder("h", CreepClass.HorseArcher)).ToArray());

        var orders = Enumerable.Range(1, 12)
            .Select(seed => string.Join(",", new CreepWavePlan(Catalog(culture), (ulong)seed).Brief(1).Packs.Select(p => p.Class).Distinct()))
            .ToList();

        Assert.IsTrue(orders.Distinct().Count() > 1, "twelve seeds gave one order");
        Assert.AreEqual(orders[3], string.Join(",", new CreepWavePlan(Catalog(culture), 4UL).Brief(1).Packs.Select(p => p.Class).Distinct()));
    }

    [TestMethod]
    public void Brief_RidersAreMountedAndFootIsNot()
    {
        var culture = Culture("a", "wall", Mix(0, 0, 50, 50), roster: Ladder("i").Concat(Ladder("c", CreepClass.Cavalry)).Concat(Ladder("h", CreepClass.HorseArcher)).ToArray());
        var foot = Culture("b", roster: Ladder("i"));
        var riders = Plan(culture).Brief(1).Creeps().ToList();
        var walkers = Plan(foot).Brief(1).Creeps().ToList();

        Assert.AreEqual(30, riders.Count);
        Assert.AreEqual(30, walkers.Count);
        Assert.IsTrue(riders.All(c => c.Mounted));
        Assert.IsTrue(walkers.All(c => !c.Mounted));
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(5)]
    public void Brief_NotAFourthWave_HasNoLeader(int wave)
    {
        Assert.IsNull(Plan(Many(3)).Brief(wave).Leader());
    }

    [DataTestMethod]
    [DataRow(4, (int)CreepRank.Captain, "_cap", 2f)]
    [DataRow(8, (int)CreepRank.Lieutenant, "_lt", 3f)]
    [DataRow(12, (int)CreepRank.General, "_gen", 4f)]
    [DataRow(16, (int)CreepRank.General, "_gen", 4f)]
    public void Brief_EveryFourthWave_FieldsTheLeaderOfTheRank(int wave, int rank, string suffix, float health)
    {
        var brief = Plan(Many(3)).Brief(wave);

        var leader = brief.Leader();
        Assert.IsNotNull(leader);
        Assert.AreEqual((CreepRank)rank, leader!.Rank);
        StringAssert.EndsWith(leader.TroopId, suffix);
        Assert.AreEqual(health, brief.LeaderHealthFactor, 0.001f);
    }

    [TestMethod]
    public void Brief_Leader_WalksInTheMiddleInfantryPack()
    {
        var plan = Plan(Many(3));

        var brief = plan.Brief(8);

        Assert.IsFalse(brief.Leader()!.Mounted);
        var holder = brief.Packs.Select((p, i) => (p, i)).Single(x => x.p.Creeps.Any(c => c.Rank != CreepRank.Soldier));
        Assert.AreEqual(CreepClass.Infantry, holder.p.Class);
        Assert.AreEqual(brief.Packs.Count(p => p.Class == CreepClass.Infantry) / 2, holder.i);
    }

    [TestMethod]
    public void Brief_NoLeaderOfThatRankIsListed_FieldsNone()
    {
        var plan = Plan(Culture("a", leaders: new[] { "only_cap" }), Culture("b", leaders: new[] { "only_cap2" }));

        Assert.IsNotNull(plan.Brief(4).Leader());
        Assert.IsNull(plan.Brief(8).Leader());
    }

    [TestMethod]
    public void Brief_NoInfantryPack_TheLeaderGetsAPackOfOne()
    {
        var culture = Culture("a", "wall", Mix(0, 0, 100), roster: Ladder("c", CreepClass.Cavalry).Concat(new[] { Row("i", 1) }).ToArray());
        var plan = new CreepWavePlan(Catalog(culture) with { Opening = Enumerable.Repeat("a", 4).ToArray() }, 7UL);

        var brief = plan.Brief(4);

        var holder = brief.Packs.Single(p => p.Creeps.Any(c => c.Rank != CreepRank.Soldier));
        Assert.AreEqual(CreepClass.Infantry, holder.Class);
        Assert.AreEqual(1, holder.Creeps.Count);
        Assert.AreEqual(48, brief.Creeps().Count(), "the leader replaces one rider, so the count stays at 30 + 6 x 3");
    }

    [DataTestMethod]
    [DataRow(4, 0)]
    [DataRow(5, 1)]
    [DataRow(9, 1)]
    [DataRow(10, 2)]
    [DataRow(25, 5)]
    [DataRow(60, 5)]
    public void Brief_Powers_FollowTheSchedule(int wave, int count)
    {
        var powers = Plan(Many(3)).Brief(wave).PowerIds;

        Assert.AreEqual(count, powers.Count);
        Assert.AreEqual(count, powers.Distinct().Count());
    }

    [TestMethod]
    public void Brief_FirstPower_IsTheCulturesOwn()
    {
        var plan = Plan(Culture("a", firstPower: "eye"), Culture("b", firstPower: "eye"));

        for (var wave = 5; wave <= 30; wave++)
            Assert.AreEqual("eye", plan.Brief(wave).PowerIds[0], $"wave {wave}");
    }

    [TestMethod]
    public void Brief_ScheduleAskedForMoreThanThePool_StopsAtThePool()
    {
        var catalog = Catalog(Many(3)) with { PowerPool = new[] { "hide", "lash" } };

        Assert.AreEqual(2, new CreepWavePlan(catalog, 7UL).Brief(30).PowerIds.Count);
    }

    [TestMethod]
    public void Brief_FirstWave_HasNoGrowth()
    {
        var brief = Plan(Many(3)).Brief(1);

        Assert.AreEqual(1f, brief.HealthFactor, 0.001f);
        Assert.AreEqual(1f, brief.DamageFactor, 0.001f);
        Assert.AreEqual(0f, brief.ArmourPercent, 0.001f);
        Assert.AreEqual(0f, brief.SwingPercent, 0.001f);
    }

    [TestMethod]
    public void Brief_Growth_FollowsTheWave()
    {
        var brief = Plan(Many(3)).Brief(5);

        Assert.AreEqual(1.2f, brief.HealthFactor, 0.001f);
        Assert.AreEqual(1.12f, brief.DamageFactor, 0.001f);
        Assert.AreEqual(4f, brief.ArmourPercent, 0.001f);
        Assert.AreEqual(2f, brief.SwingPercent, 0.001f);
    }

    [TestMethod]
    public void Brief_Growth_StopsAtItsCaps()
    {
        var brief = Plan(Many(3)).Brief(500);

        Assert.AreEqual(60f, brief.ArmourPercent, 0.001f);
        Assert.AreEqual(25f, brief.SwingPercent, 0.001f);
        Assert.AreEqual(20f, brief.HealthFactor, 0.001f);
    }
}
