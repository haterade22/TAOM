using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>#1109: the two MCM knobs behind gold mode, their defaults and their clamp.</summary>
[TestClass]
public class SpecialResourceGoldSettingsTests
{
    [TestMethod]
    public void UseSpecialResources_Defaults_True()
    {
        Assert.IsTrue(new TaomSettings().UseSpecialResources);
        Assert.AreEqual(100, new TaomSettings().GoldPerResourcePoint);
        Assert.IsTrue(new SpecialResourceGoldSettings(new TaomSettings()).UseSpecialResources);
    }

    [TestMethod]
    public void UseSpecialResources_McmOff_False() =>
        Assert.IsFalse(new SpecialResourceGoldSettings(new TaomSettings { UseSpecialResources = false }).UseSpecialResources);

    [TestMethod]
    public void GoldPerResourcePoint_Zero_ClampsTo1() =>
        Assert.AreEqual(1, new SpecialResourceGoldSettings(new TaomSettings { GoldPerResourcePoint = 0 }).GoldPerResourcePoint);

    [TestMethod]
    public void GoldPerResourcePoint_5000_ClampsTo1000() =>
        Assert.AreEqual(1000, new SpecialResourceGoldSettings(new TaomSettings { GoldPerResourcePoint = 5000 }).GoldPerResourcePoint);

    [TestMethod]
    public void GoldPerResourcePoint_InRange_PassesThrough() =>
        Assert.AreEqual(250, new SpecialResourceGoldSettings(new TaomSettings { GoldPerResourcePoint = 250 }).GoldPerResourcePoint);
}
