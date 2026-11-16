using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// A shop's <c>heroes</c> list (#1325): each row is one named hero with his character, price and health factor. A bad row is skipped
/// with one warning that names it, and the rest of the shop still loads.
/// </summary>
[TestClass]
public class LongDefeatHeroesReaderTests
{
    // The warnings about the heroes block; the scope's own summary is a LogWarning too, and names no row.
    private static int Warnings(IModLogger logger) => logger.ReceivedCalls()
        .Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning) && call.GetArguments()[0] is string m && m.Contains(".heroes"));

    private static ShopPlan Read(string heroes, out IModLogger logger)
    {
        logger = Substitute.For<IModLogger>();
        var text = "{ \"shops\": { \"rohan\": { \"startingGold\": 600, \"offers\": [ { \"id\": \"militia\", \"troop\": \"fow_m\", \"class\": \"Infantry\", \"packSize\": 12, \"price\": 180 } ]"
            + (heroes.Length > 0 ? ", \"heroes\": " + heroes : "") + " } } }";
        return ShopReader.Read(text, new LongDefeatConfigScope(logger, "shop.json"))["rohan"];
    }

    private static string Row(string? id, string? troop, object price, object? healthFactor = null, string? name = null, string? text = null)
    {
        var row = new JObject { ["id"] = id, ["troop"] = troop, ["price"] = JToken.FromObject(price), ["name"] = name, ["text"] = text };
        if (healthFactor != null)
            row["healthFactor"] = JToken.FromObject(healthFactor);
        return row.ToString(Newtonsoft.Json.Formatting.None);
    }

    [TestMethod]
    public void Read_NoHeroesBlock_GivesNoHeroAndNoWarning()
    {
        var shop = Read("", out var logger);

        Assert.AreEqual(0, shop.Heroes.Count);
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void Read_AHeroRow_KeepsEveryField()
    {
        var shop = Read("[" + Row("aragorn", "taom_hero_aragorn", 300, 3.0, "{=K_A}Aragorn", "{=T_A}Isildur's heir") + "]", out var logger);

        Assert.AreEqual(new HeroOffer("aragorn", "{=K_A}Aragorn", "{=T_A}Isildur's heir", "taom_hero_aragorn", 300, 3f), shop.Heroes.Single());
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void Read_NoNameTextOrHealthFactor_TakesTheIdAnEmptyTextAndOne()
    {
        var hero = Read("[" + Row("gimli", "taom_hero_gimli", 250) + "]", out var logger).Heroes.Single();

        Assert.AreEqual(("gimli", "", 1f), (hero.Name, hero.Text, hero.HealthFactor));
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void Read_HeroesNotAnArray_SellsNoHeroAndWarnsOnce()
    {
        var shop = Read("{ \"aragorn\": 1 }", out var logger);

        Assert.AreEqual(0, shop.Heroes.Count);
        Assert.AreEqual(1, Warnings(logger));
        Assert.AreEqual(1, shop.Offers.Count, "the packs still load");
    }

    public static System.Collections.Generic.IEnumerable<object[]> BadRows() => new[]
    {
        new object[] { "7", "not an object" },
        new object[] { Row(null, "taom_hero_x", 300), "'id'" },
        new object[] { Row("x", null, 300), "'troop'" },
        new object[] { Row("gimli son", "taom_hero_x", 300), "'id'" },
        new object[] { Row("Gimli", "taom_hero_x", 300), "'id'" },
        new object[] { Row("x", "taom_hero_x", 0), "price" },
        new object[] { Row("x", "taom_hero_x", 100_001), "price" },
        new object[] { Row("x", "taom_hero_x", "cheap"), "price" },
        new object[] { Row("x", "taom_hero_x", 300, 0.5), "healthFactor" },
        new object[] { Row("x", "taom_hero_x", 300, 10.5), "healthFactor" },
        new object[] { Row("x", "taom_hero_x", 300, "strong"), "healthFactor" },
    };

    [TestMethod]
    [DynamicData(nameof(BadRows), DynamicDataSourceType.Method)]
    public void Read_ABadRow_IsSkippedWithOneWarningThatNamesIt(string row, string word)
    {
        var shop = Read("[" + row + "," + Row("gimli", "taom_hero_gimli", 250) + "]", out var logger);

        CollectionAssert.AreEqual(new[] { "gimli" }, shop.Heroes.Select(h => h.Id).ToList());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("shops.rohan.heroes[0]") && m.Contains(word)));
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void Read_ARepeatedId_KeepsTheFirst()
    {
        var shop = Read("[" + Row("aragorn", "taom_hero_aragorn", 300) + "," + Row("aragorn", "taom_hero_other", 9) + "]", out var logger);

        Assert.AreEqual(300, shop.Heroes.Single().Price);
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void Read_ARepeatedTroop_KeepsTheFirst()
    {
        // One character, one body on the field: his fall is read by his character.
        var shop = Read("[" + Row("aragorn", "taom_hero_aragorn", 300) + "," + Row("strider", "TAOM_HERO_ARAGORN", 9) + "]", out var logger);

        Assert.AreEqual("aragorn", shop.Heroes.Single().Id);
        Assert.AreEqual(1, Warnings(logger));
    }
}
