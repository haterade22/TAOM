using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionDisplayHelperTests
{
    [TestMethod]
    public void Localize_NullInput_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, FactionDisplayHelper.Localize(null));
    }

    [TestMethod]
    public void Localize_EmptyInput_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, FactionDisplayHelper.Localize(string.Empty));
    }

    [TestMethod]
    public void Localize_PlainEnglishString_PassesThrough()
    {
        const string input = "Stewardship of Gondor";
        Assert.AreEqual(input, FactionDisplayHelper.Localize(input));
    }

    [TestMethod]
    public void Localize_KeyedStringWithDefault_ReturnsDefaultWhenKeyUnregistered()
    {
        const string input = "{=taom_test_factionmap_unregistered_key}Fallback Default";
        Assert.AreEqual("Fallback Default", FactionDisplayHelper.Localize(input));
    }

    [TestMethod]
    public void Localize_KeyedStringWithNoDefault_ReturnsEmptyWhenKeyUnregistered()
    {
        const string input = "{=taom_test_factionmap_no_default_key}";
        Assert.AreEqual(string.Empty, FactionDisplayHelper.Localize(input));
    }

    [TestMethod]
    public void Localize_StringWithoutKeyToken_PassesThroughVerbatim()
    {
        const string input = "Garrisons cost 20% less and gain +1 loyalty per day.";
        Assert.AreEqual(input, FactionDisplayHelper.Localize(input));
    }
}
