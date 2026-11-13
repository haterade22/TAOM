using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>upgrades</c> block of shop.json (#1161): one step per row from a troop to the next troop of its line, with a price per soldier. A bad row is skipped with one warning, and a row that
/// would close a loop is skipped, so a line always ends.
/// </summary>
[TestClass]
public class LongDefeatUpgradesReaderTests
{
    // The warnings about the upgrades block; the scope's own summary ("loaded, ..., but N value(s) ... were skipped") is a LogWarning too, and names no row.
    private static int Warnings(IModLogger logger) => logger.ReceivedCalls()
        .Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning) && call.GetArguments()[0] is string m && m.Contains(".upgrades"));

    private static ShopPlan Read(string upgrades, out IModLogger logger)
    {
        logger = Substitute.For<IModLogger>();
        var text = "{ \"shops\": { \"rohan\": { \"startingGold\": 600, \"offers\": [ { \"id\": \"militia\", \"troop\": \"fow_m\", \"class\": \"Infantry\", \"packSize\": 12, \"price\": 180 } ]"
            + (upgrades.Length > 0 ? ", \"upgrades\": " + upgrades : "") + " } } }";
        return ShopReader.Read(text, new LongDefeatConfigScope(logger, "shop.json"))["rohan"];
    }

    private static string Row(string from, string to, object price, string? name = null) =>
        new JObject { ["from"] = from, ["to"] = to, ["price"] = JToken.FromObject(price), ["name"] = name }.ToString(Newtonsoft.Json.Formatting.None);

    [TestMethod]
    public void Read_NoUpgradesBlock_GivesNoStepAndNoWarning()
    {
        var shop = Read("", out var logger);

        Assert.AreEqual(0, shop.Upgrades.Count);
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void Read_AValidLine_KeepsEveryStepInOrder_WithItsIdNameAndPrice()
    {
        var shop = Read("[" + Row("fow_m", "fow_a", 15, "{=K_A}Militia to Axemen") + "," + Row("fow_a", "vet_a", 25) + "]", out var logger);

        Assert.AreEqual(0, Warnings(logger));
        Assert.AreEqual(2, shop.Upgrades.Count);
        var first = shop.Upgrades[0];
        Assert.AreEqual("fow_m>fow_a", first.Id);
        Assert.AreEqual("{=K_A}Militia to Axemen", first.Name);
        Assert.AreEqual("fow_m", first.FromTroop);
        Assert.AreEqual("fow_a", first.ToTroop);
        Assert.AreEqual(15, first.Price);
        Assert.AreEqual("vet_a", shop.Upgrades[1].Name, "a step with no name shows its target troop id");
    }

    [TestMethod]
    public void Read_ABranch_KeepsBothStepsFromTheSameTroop()
    {
        var shop = Read("[" + Row("fow_a", "vet_a", 25) + "," + Row("fow_a", "two_a", 25) + "]", out var logger);

        Assert.AreEqual(0, Warnings(logger));
        CollectionAssert.AreEqual(new[] { "vet_a", "two_a" }, shop.Upgrades.Select(s => s.ToTroop).ToList());
    }

    [DataTestMethod]
    [DataRow("{ \"to\": \"b\", \"price\": 5 }", "from")]
    [DataRow("{ \"from\": \"a\", \"price\": 5 }", "to")]
    [DataRow("{ \"from\": \"a\", \"to\": \"A\", \"price\": 5 }", "itself")]
    [DataRow("{ \"from\": \"a\", \"to\": \"b\", \"price\": 0 }", "price")]
    [DataRow("{ \"from\": \"a\", \"to\": \"b\", \"price\": 100001 }", "price")]
    [DataRow("{ \"from\": \"a\", \"to\": \"b\", \"price\": \"ten\" }", "price")]
    [DataRow("{ \"from\": \"a\", \"to\": \"b\" }", "price")]
    [DataRow("7", "object")]
    public void Read_ABadRow_IsSkippedWithOneWarningThatNamesIt(string row, string word)
    {
        var shop = Read("[" + row + "," + Row("c", "d", 5) + "]", out var logger);

        CollectionAssert.AreEqual(new[] { "c>d" }, shop.Upgrades.Select(s => s.Id).ToList());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("shops.rohan.upgrades[0]") && m.Contains(word)));
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void Read_ARepeatedStep_KeepsTheFirst()
    {
        var shop = Read("[" + Row("a", "b", 5) + "," + Row("A", "B", 9) + "]", out var logger);

        Assert.AreEqual(1, shop.Upgrades.Count);
        Assert.AreEqual(5, shop.Upgrades[0].Price);
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shops.rohan.upgrades[1]") && m.Contains("repeats")));
    }

    [TestMethod]
    public void Read_ARowThatClosesALoop_IsSkipped_SoEveryLineEnds()
    {
        var shop = Read("[" + Row("a", "b", 5) + "," + Row("b", "c", 5) + "," + Row("c", "a", 5) + "]", out var logger);

        CollectionAssert.AreEqual(new[] { "a>b", "b>c" }, shop.Upgrades.Select(s => s.Id).ToList());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shops.rohan.upgrades[2]") && m.Contains("loop")));
    }

    [TestMethod]
    public void Read_UpgradesNotAnArray_GivesNoStepAndOneWarning()
    {
        var shop = Read("{ \"from\": \"a\" }", out var logger);

        Assert.AreEqual(0, shop.Upgrades.Count);
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shops.rohan.upgrades") && m.Contains("array")));
    }
}
