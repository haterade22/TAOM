using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class CommissionSkillBudgetTests
{
    private readonly CommissionSkillBudget _sut = new CommissionSkillBudget();

    [TestMethod]
    public void Compute_HeroLevel_EqualsTroopLevel()
    {
        var plan = _sut.Compute(12, new Dictionary<string, int>(), 5, 300);

        Assert.AreEqual(12, plan.HeroLevel);
    }

    [TestMethod]
    public void Compute_TroopLevelZero_ClampsHeroLevelToOne()
    {
        var plan = _sut.Compute(0, new Dictionary<string, int>(), 5, 300);

        Assert.AreEqual(1, plan.HeroLevel);
    }

    [TestMethod]
    public void Compute_TroopLevelNegative_ClampsHeroLevelToOne()
    {
        var plan = _sut.Compute(-5, new Dictionary<string, int>(), 5, 300);

        Assert.AreEqual(1, plan.HeroLevel);
    }

    [TestMethod]
    public void Compute_SkillBelowBudget_KeepsTemplateValue()
    {
        // level 10 * 5 points/level = 50 budget; template has 30 -> stays 30
        var template = new Dictionary<string, int> { ["OneHanded"] = 30 };

        var plan = _sut.Compute(10, template, 5, 300);

        Assert.AreEqual(30, plan.SkillValues["OneHanded"]);
    }

    [TestMethod]
    public void Compute_SkillAboveBudget_CapsToLevelDerivedBudget()
    {
        // level 10 * 5 points/level = 50 budget; template has 200 -> capped to 50
        var template = new Dictionary<string, int> { ["OneHanded"] = 200 };

        var plan = _sut.Compute(10, template, 5, 300);

        Assert.AreEqual(50, plan.SkillValues["OneHanded"]);
    }

    [TestMethod]
    public void Compute_BudgetExceedsMaxSkillValue_CapsToMaxSkillValue()
    {
        // level 100 * 5 = 500 budget, but maxSkillValue is 300 -> capped to 300
        var template = new Dictionary<string, int> { ["OneHanded"] = 1000 };

        var plan = _sut.Compute(100, template, 5, 300);

        Assert.AreEqual(300, plan.SkillValues["OneHanded"]);
    }

    [TestMethod]
    public void Compute_NegativeTemplateSkillValue_ClampsToZero()
    {
        var template = new Dictionary<string, int> { ["OneHanded"] = -10 };

        var plan = _sut.Compute(10, template, 5, 300);

        Assert.AreEqual(0, plan.SkillValues["OneHanded"]);
    }

    [TestMethod]
    public void Compute_MultipleSkills_EachCappedIndependently()
    {
        var template = new Dictionary<string, int> { ["OneHanded"] = 200, ["Bow"] = 10, ["Athletics"] = 0 };

        var plan = _sut.Compute(10, template, 5, 300);

        Assert.AreEqual(50, plan.SkillValues["OneHanded"]);
        Assert.AreEqual(10, plan.SkillValues["Bow"]);
        Assert.AreEqual(0, plan.SkillValues["Athletics"]);
    }

    [TestMethod]
    public void Compute_ReturnsConfiguredFocusPerNonZeroSkillAndFlatAttributeBonus()
    {
        var plan = _sut.Compute(10, new Dictionary<string, int>(), 5, 300);

        Assert.AreEqual(1, plan.FocusPerNonZeroSkill);
        Assert.AreEqual(2, plan.FlatAttributeBonus);
    }

    [TestMethod]
    public void Compute_NonPositiveSkillPointsPerLevel_ClampsToOne()
    {
        var template = new Dictionary<string, int> { ["OneHanded"] = 200 };

        // 0 points/level would zero every budget; the clamp keeps at least 1/level so a
        // misconfigured JSON value can't make every promoted companion unskilled.
        var plan = _sut.Compute(10, template, 0, 300);

        Assert.AreEqual(10, plan.SkillValues["OneHanded"]);
    }
}
