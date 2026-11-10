using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Arachnophobia;

namespace TAOM.Tests.Features.Arachnophobia;

[TestClass]
public class ArachnophobiaRulesTests
{
    [DataTestMethod]
    [DataRow("spider_mount_a", "warg_brown")]
    [DataRow("spider_mount_mountain_a1", "warg_brown")]
    [DataRow("spider_mount_brown", "warg_dark")]
    [DataRow("spider_mount_mountain_a2", "warg_dark")]
    [DataRow("spider_mount_pale", "fell_warg")]
    public void WargFor_ShippedSpiderItem_ReturnsItsWarg(string spiderItem, string warg)
        => Assert.AreEqual(warg, ArachnophobiaRules.WargFor(spiderItem));

    [TestMethod]
    public void WargFor_UnknownSpiderItem_ReturnsTheBrownWarg()
        => Assert.AreEqual("warg_brown", ArachnophobiaRules.WargFor("spider_mount_added_later"));

    [TestMethod]
    public void RequiredItemIds_ListsEveryWargAndTheHarness()
        => CollectionAssert.AreEquivalent(new[] { "warg_brown", "warg_dark", "fell_warg", "warg_saddle" },
            ArachnophobiaRules.RequiredItemIds.ToList());
}
