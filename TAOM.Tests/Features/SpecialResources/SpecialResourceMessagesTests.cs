using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The four outflow messages (#558). Every number rides on the TextObject as a slot, never baked
/// into the default text: the desertion popup shipped with the count interpolated into the string,
/// which made it unlocalizable by construction (#434). Assertions read <c>Value</c> and
/// <c>Attributes</c>, never <c>ToString()</c>, which needs MBTextManager and a running Module.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SpecialResourceMessagesTests
{
    private static void AssertNoBakedNumber(TextObject message)
    {
        Assert.IsFalse(Regex.IsMatch(message.Value, "[0-9]"),
            $"A number is baked into the default text, so no translation can carry it: {message.Value}");
    }

    [TestMethod]
    public void DailyUpkeep_BindsIncomeUpkeepAndBalanceAsSlots()
    {
        var message = SpecialResourceMessages.DailyUpkeep("War Spoils", income: 2.4f, upkeep: 3f, balance: 12f);

        StringAssert.Contains(message.Value, "{=taom_res_daily_upkeep}");
        StringAssert.Contains(message.Value, "{RESOURCE}");
        StringAssert.Contains(message.Value, "{INCOME}");
        StringAssert.Contains(message.Value, "{UPKEEP}");
        StringAssert.Contains(message.Value, "{BALANCE}");
        AssertNoBakedNumber(message);
        Assert.AreEqual("War Spoils", message.Attributes["RESOURCE"]);
        Assert.AreEqual("2.4", message.Attributes["INCOME"]);
        Assert.AreEqual("3", message.Attributes["UPKEEP"]);
        Assert.AreEqual("12", message.Attributes["BALANCE"]);
    }

    [TestMethod]
    public void UpkeepOverdraft_BindsTheBillAndTheBalanceItExceeded()
    {
        var message = SpecialResourceMessages.UpkeepOverdraft("Gems", upkeep: 4.5f, balanceBefore: 1.25f);

        StringAssert.Contains(message.Value, "{=taom_res_upkeep_overdraft}");
        StringAssert.Contains(message.Value, "{RESOURCE}");
        StringAssert.Contains(message.Value, "{UPKEEP}");
        StringAssert.Contains(message.Value, "{BALANCE}");
        AssertNoBakedNumber(message);
        Assert.AreEqual("Gems", message.Attributes["RESOURCE"]);
        Assert.AreEqual("4.5", message.Attributes["UPKEEP"]);
        Assert.AreEqual("1.25", message.Attributes["BALANCE"]);
    }

    [TestMethod]
    public void UpgradeSpend_BindsAmountAndRemainingBalance()
    {
        var message = SpecialResourceMessages.UpgradeSpend("War Spoils", amount: 12f, balance: 88f);

        StringAssert.Contains(message.Value, "{=taom_res_upgrade_spend}");
        StringAssert.Contains(message.Value, "{RESOURCE}");
        StringAssert.Contains(message.Value, "{AMOUNT}");
        StringAssert.Contains(message.Value, "{BALANCE}");
        AssertNoBakedNumber(message);
        Assert.AreEqual("12", message.Attributes["AMOUNT"]);
        Assert.AreEqual("88", message.Attributes["BALANCE"]);
    }

    [TestMethod]
    public void RecruitCharge_BindsAmountCountAndTroopName()
    {
        var message = SpecialResourceMessages.RecruitCharge("War Drums", amount: 100f, count: 2, troopName: "Harad Elephant Rider");

        StringAssert.Contains(message.Value, "{=taom_res_recruit_charge}");
        StringAssert.Contains(message.Value, "{RESOURCE}");
        StringAssert.Contains(message.Value, "{AMOUNT}");
        StringAssert.Contains(message.Value, "{COUNT}");
        StringAssert.Contains(message.Value, "{TROOP}");
        AssertNoBakedNumber(message);
        Assert.AreEqual("100", message.Attributes["AMOUNT"]);
        Assert.AreEqual(2, message.Attributes["COUNT"]);
        Assert.AreEqual("Harad Elephant Rider", message.Attributes["TROOP"]);
    }

    [TestMethod]
    public void FormatAmount_TrimsTrailingZerosAndKeepsTwoDecimals()
    {
        Assert.AreEqual("3", SpecialResourceMessages.FormatAmount(3f));
        Assert.AreEqual("2.4", SpecialResourceMessages.FormatAmount(2.4f));
        Assert.AreEqual("0.05", SpecialResourceMessages.FormatAmount(0.05f));
        Assert.AreEqual("1.25", SpecialResourceMessages.FormatAmount(1.25f));
    }
}
