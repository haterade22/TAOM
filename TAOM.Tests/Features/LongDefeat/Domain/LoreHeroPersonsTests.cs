using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>A mission hero is not fielded when the player is the person he stands for: the table of ids and the plan filter.</summary>
[TestClass]
public class LoreHeroPersonsTests
{
    private static RunPlan Plan(string? mustLive = "boromir")
    {
        var waves = new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300));
        var shop = new ShopPlan(600, new ShopOffer[0])
        {
            Heroes = new[]
            {
                new HeroOffer("gimli", "Gimli", "t", "taom_hero_gimli", 500, 8f),
                new HeroOffer("aragorn", "Aragorn", "t", "taom_hero_aragorn", 500, 8f),
            },
        };
        return new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves, shop, RunRules.Default, RunOptions.Default)
        {
            FreeHeroes = new[]
            {
                new HeroSpawn("boromir", "taom_hero_boromir", 8f),
                new HeroSpawn("aragorn", "taom_hero_aragorn", 8f),
                new HeroSpawn("legolas", "taom_hero_legolas", 8f),
                new HeroSpawn("gimli", "taom_hero_gimli", 8f),
            },
            MustLive = mustLive,
        };
    }

    [DataTestMethod]
    [DataRow("taom_hero_gimli", "named_companion_gimli", true)]
    [DataRow("taom_hero_gimli", "taom_hero_gimli", true)]
    [DataRow("taom_hero_aragorn", "named_companion_aragorn", true)]
    [DataRow("taom_hero_legolas", "lord_M1_11", true)]
    [DataRow("taom_hero_boromir", "lord_1_75", true)]
    [DataRow("taom_hero_gimli", "named_companion_aragorn", false)]
    [DataRow("taom_hero_gimli", "lord_1_75", false)]
    [DataRow("taom_hero_unknown", "taom_hero_unknown", false)]
    public void IsPlayed_MatchesOnlyThePersonsOwnIds(string hero, string player, bool expected) =>
        Assert.AreEqual(expected, LoreHeroPersons.IsPlayed(hero, new[] { "main_hero", player }));

    [TestMethod]
    public void IsPlayed_NoPlayerIds_IsFalse() => Assert.IsFalse(LoreHeroPersons.IsPlayed("taom_hero_gimli", new string[0]));

    [TestMethod]
    public void PlayerIsGimli_DropsGimliFromFreeHeroesAndShop_KeepsTheRest()
    {
        var plan = LoreHeroPersons.WithoutPlayed(Plan(), new[] { "named_companion_gimli" });

        CollectionAssert.AreEqual(new[] { "boromir", "aragorn", "legolas" }, plan.FreeHeroes.Select(h => h.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "aragorn" }, plan.Shop.Heroes.Select(h => h.Id).ToArray());
        Assert.AreEqual("boromir", plan.MustLive);
    }

    [TestMethod]
    public void PlayerIsBoromirLord_DropsBoromirAndHisMustLive()
    {
        var plan = LoreHeroPersons.WithoutPlayed(Plan(), new[] { "lord_1_75" });

        Assert.IsFalse(plan.FreeHeroes.Any(h => h.Id == "boromir"));
        Assert.IsNull(plan.MustLive);
    }

    [TestMethod]
    public void PlayerIsNobodyLore_ReturnsTheSamePlan()
    {
        var plan = Plan();

        Assert.AreSame(plan, LoreHeroPersons.WithoutPlayed(plan, new[] { "lord_4_1" }));
        Assert.AreSame(plan, LoreHeroPersons.WithoutPlayed(plan, new string[0]));
    }
}
