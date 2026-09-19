using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Which elephant harness gets the howdah platform, and which also gets a crew (#627, Mike 2026-09-19). The howdah
/// harness (the elite howdah mesh) gets both. The plain armour keeps the crewless platform it always had: crew on it
/// would stand on an invisible deck over a back with no howdah.
/// </summary>
[TestClass]
public class HowdahHarnessTests
{
    [TestMethod]
    public void GetsPlatform_HowdahHarness_IsTrue()
    {
        Assert.IsTrue(HowdahHarness.GetsPlatform(ElephantConfig.HowdahHarnessStringId));
    }

    [TestMethod]
    public void GetsPlatform_PlainArmour_StaysTrue()
    {
        Assert.IsTrue(HowdahHarness.GetsPlatform(ElephantConfig.HarnessStringId));
    }

    [TestMethod]
    public void GetsPlatform_OtherHarnessOrNone_IsFalse()
    {
        Assert.IsFalse(HowdahHarness.GetsPlatform("sk_spider_armor_a"));
        Assert.IsFalse(HowdahHarness.GetsPlatform(""));
        Assert.IsFalse(HowdahHarness.GetsPlatform(null));
    }

    [TestMethod]
    public void CarriesCrew_HowdahHarness_IsTrue()
    {
        Assert.IsTrue(HowdahHarness.CarriesCrew(ElephantConfig.HowdahHarnessStringId));
    }

    [TestMethod]
    public void CarriesCrew_PlainArmour_IsFalse()
    {
        Assert.IsFalse(HowdahHarness.CarriesCrew(ElephantConfig.HarnessStringId));
    }

    [TestMethod]
    public void CarriesCrew_OtherHarnessOrNone_IsFalse()
    {
        Assert.IsFalse(HowdahHarness.CarriesCrew("sk_spider_armor_a"));
        Assert.IsFalse(HowdahHarness.CarriesCrew(null));
    }

    [TestMethod]
    public void TheTwoHarnessIds_AreDistinct()
    {
        Assert.AreNotEqual(ElephantConfig.HarnessStringId, ElephantConfig.HowdahHarnessStringId);
    }
}
