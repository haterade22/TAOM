using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits.Diagnostics;

// Creature Bandits diagnostics (#692): every line is key=value, invariant culture (FileLogger applies none, so a
// de-DE machine would write 1,5), with engine floats printed, never gated, NaN included.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureDiagFormatTests
{
    [TestMethod]
    public void F_FormatsTwoDecimalsInvariant_EvenUnderAGermanCulture()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual("1.50", CreatureDiagFormat.F(1.5f));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }

    [TestMethod]
    public void F_PrintsNonFiniteValuesByName()
    {
        Assert.AreEqual("NaN", CreatureDiagFormat.F(float.NaN));
        Assert.AreEqual("+Inf", CreatureDiagFormat.F(float.PositiveInfinity));
        Assert.AreEqual("-Inf", CreatureDiagFormat.F(float.NegativeInfinity));
    }

    [TestMethod]
    public void Name_QuotesAndStripsLogBreakingCharacters()
    {
        Assert.AreEqual("\"Pale Broodmother\"", CreatureDiagFormat.Name("Pale Broodmother"));
        Assert.AreEqual("\"a'b c\"", CreatureDiagFormat.Name("a\"b\nc"));
        Assert.AreEqual("-", CreatureDiagFormat.Name(null));
        Assert.AreEqual("-", CreatureDiagFormat.Name(""));
    }

    [TestMethod]
    public void Line_HasThePrefixKindTimeSerialAndPairsInOrder()
    {
        string line = CreatureDiagFormat.Line("spawn-begin", 12.345f, 3, "troop", "taom_spider_brood_forest", "hp", "180.00");
        Assert.AreEqual("[CreatureBandits][diag] spawn-begin t=12.35 cb=3 troop=taom_spider_brood_forest hp=180.00", line);
    }

    [TestMethod]
    public void Line_WithNoSerial_OmitsTheCreatureKey()
        => Assert.AreEqual("[CreatureBandits][diag] mission t=0.00 scene=x",
            CreatureDiagFormat.Line("mission", 0f, 0, "scene", "x"));

    [TestMethod]
    public void CampaignLine_CarriesTheCampaignDayInsteadOfMissionTime()
        => Assert.AreEqual("[CreatureBandits][diag] brood-spawn day=3.50 id=x",
            CreatureDiagFormat.CampaignLine("brood-spawn", 3.5, "id", "x"));

    [TestMethod]
    public void Line_OddPairCount_KeepsTheDanglingKey()
        => Assert.AreEqual("[CreatureBandits][diag] snap t=1.00 alive=",
            CreatureDiagFormat.Line("snap", 1f, 0, "alive"));
}
