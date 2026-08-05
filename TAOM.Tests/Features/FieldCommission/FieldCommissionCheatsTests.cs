using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCommission.Cheats;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class FieldCommissionCheatsTests
{
    [TestMethod]
    public void FormatGrant_IncludesTroopIdAmountAndNewTotal()
    {
        var result = FieldCommissionCheats.FormatGrant("taom_soldier", 8, 20);

        StringAssert.Contains(result, "taom_soldier");
        StringAssert.Contains(result, "8");
        StringAssert.Contains(result, "20");
    }

    [TestMethod]
    public void FormatStatus_NoBankedMerit_ReportsNoMerit()
    {
        var config = new FieldCommissionConfig();

        var result = FieldCommissionCheats.FormatStatus(config, new Dictionary<string, int>(), 0, false, false);

        StringAssert.Contains(result, "no banked merit");
    }

    [TestMethod]
    public void FormatStatus_WithBankedMerit_ListsEachTroopDescendingByAmount()
    {
        var config = new FieldCommissionConfig();
        var merits = new Dictionary<string, int> { ["troop_low"] = 2, ["troop_high"] = 9 };

        var result = FieldCommissionCheats.FormatStatus(config, merits, 3, true, false);

        var highIndex = result.IndexOf("troop_high=9");
        var lowIndex = result.IndexOf("troop_low=2");
        Assert.IsTrue(highIndex >= 0 && lowIndex >= 0 && highIndex < lowIndex, "Higher-merit troop should be listed first.");
    }

    [TestMethod]
    public void FormatStatus_IncludesConfigAndQueueState()
    {
        var config = new FieldCommissionConfig { Enabled = false, RatioThreshold = 1.5f, MeritPerKill = 2, MeritThreshold = 10, RetainerAllowance = 1 };

        var result = FieldCommissionCheats.FormatStatus(config, new Dictionary<string, int>(), 5, true, true);

        StringAssert.Contains(result, "enabled=False");
        StringAssert.Contains(result, "1.50");
        StringAssert.Contains(result, "promoted companions so far: 5");
        StringAssert.Contains(result, "offer pending: True");
        StringAssert.Contains(result, "offer showing now: True");
    }

    [TestMethod]
    public void FormatStatus_NullMerits_ReportsNoMerit()
    {
        var config = new FieldCommissionConfig();

        var result = FieldCommissionCheats.FormatStatus(config, null, 0, false, false);

        StringAssert.Contains(result, "no banked merit");
    }
}
