using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Which elephant harness gets the howdah platform and its crew (#627): the three howdahs, which share one deck
/// placement (Mike 2026-09-29). The plain armours carry no howdah, so crew on them would stand on an invisible deck.
/// </summary>
[TestClass]
public class HowdahHarnessTests
{
    [DataTestMethod]
    [DataRow("sk_elephant_armor_howdah_med")]
    [DataRow("sk_elephant_armor_howdah_heavy")]
    [DataRow("sk_elephant_armor_howdah_elite")]
    public void GetsPlatform_Howdah_IsTrue(string harnessId)
    {
        Assert.IsTrue(HowdahHarness.GetsPlatform(harnessId));
    }

    [DataTestMethod]
    [DataRow("sk_elephant_armor_a")]
    [DataRow("sk_elephant_armor_heavy")]
    [DataRow("sk_elephant_armor_elite")]
    [DataRow("sk_spider_armor_a")]
    [DataRow("")]
    [DataRow(null)]
    public void GetsPlatform_PlainArmourOtherHarnessOrNone_IsFalse(string? harnessId)
    {
        Assert.IsFalse(HowdahHarness.GetsPlatform(harnessId));
    }

    [TestMethod]
    public void HowdahHarnessStringIds_AreExactlyTheThreeHowdahs()
    {
        CollectionAssert.AreEquivalent(
            new[] { "sk_elephant_armor_howdah_med", "sk_elephant_armor_howdah_heavy", "sk_elephant_armor_howdah_elite" },
            ElephantConfig.HowdahHarnessStringIds.ToArray());
    }
}
