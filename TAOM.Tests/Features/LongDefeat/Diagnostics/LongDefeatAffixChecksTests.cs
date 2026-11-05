using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>The <c>affix</c> and <c>affix-effect</c> verdicts (phase2-plan.md section 6): pure, over a roll and a read-back.</summary>
[TestClass]
public class LongDefeatAffixChecksTests
{
    private static AffixPlan Plan() => new(
        new[] { new AffixStage(5, 1), new AffixStage(10, 2), new AffixStage(15, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["drums"] = new Dictionary<string, float> { ["moveSpeedPercent"] = 12f, ["accelerationPercent"] = 25f },
            ["eye"] = new Dictionary<string, float>
            {
                ["aimErrorPercent"] = -30f, ["drawSpeedPercent"] = 15f, ["reloadSpeedPercent"] = 15f, ["missileSpeedPercent"] = 10f,
            },
            ["discipline"] = new Dictionary<string, float> { ["blockAbilityPercent"] = 25f, ["parryAbilityPercent"] = 25f },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
        },
        AffixPlan.Ids.ToDictionary(id => id, id => "{=K_" + id + "}" + id));

    private static LdStatSample Sample(params (string Stat, float Base, float Actual)[] rows) =>
        new("fow_urukhai_fighter", rows.Select(r => new LdStatReading(r.Stat, r.Base, r.Actual)).ToList());

    // ---- affix ----

    [TestMethod]
    public void Affix_ARollThatMatchesTheSchedule_Passes()
    {
        var verdict = LongDefeatAffixChecks.Affix(Plan(), 10, new[] { "drums", "terror" });

        Assert.AreEqual(CheckResult.Pass, verdict.Result);
        StringAssert.StartsWith(verdict.Detail, "w=10 ");
        StringAssert.Contains(verdict.Detail, "powers=drums,terror");
        StringAssert.Contains(verdict.Detail, "scheduled=2");
    }

    [TestMethod]
    public void Affix_AWrongCount_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Affix(Plan(), 10, new[] { "drums" }).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Affix(Plan(), 5, new[] { "hide", "lash" }).Result);
    }

    [TestMethod]
    public void Affix_ARepeatedOrUnknownPower_Fails()
    {
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Affix(Plan(), 10, new[] { "drums", "drums" }).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Affix(Plan(), 5, new[] { "nonsense" }).Result);
    }

    [TestMethod]
    public void Affix_APowerWithNoNameInTheCulturesTable_Fails_AndNamesIt()
    {
        var plan = new AffixPlan(new[] { new AffixStage(5, 1) }, new Dictionary<string, IReadOnlyDictionary<string, float>>(), new Dictionary<string, string>());

        var verdict = LongDefeatAffixChecks.Affix(plan, 5, new[] { "hide" });

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "unnamed=hide");
    }

    // ---- expectations ----

    [TestMethod]
    public void Expectations_EachStatPower_NamesItsDrivenPropertiesAndTheirArithmetic()
    {
        var lash = LongDefeatAffixChecks.Expectations(Plan(), "lash");
        var drums = LongDefeatAffixChecks.Expectations(Plan(), "drums");
        var eye = LongDefeatAffixChecks.Expectations(Plan(), "eye");
        var discipline = LongDefeatAffixChecks.Expectations(Plan(), "discipline");
        var hide = LongDefeatAffixChecks.Expectations(Plan(), "hide");

        CollectionAssert.AreEquivalent(new[] { "SwingSpeedMultiplier", "AIAttackOnDecideChance" }, lash.Select(e => e.Stat).ToArray());
        CollectionAssert.AreEquivalent(new[] { "MaxSpeedMultiplier", "CombatMaxSpeedMultiplier", "TopSpeedReachDuration" }, drums.Select(e => e.Stat).ToArray());
        CollectionAssert.AreEquivalent(new[] { "AiShooterError", "ThrustOrRangedReadySpeedMultiplier", "ReloadSpeed", "MissileSpeedMultiplier" }, eye.Select(e => e.Stat).ToArray());
        CollectionAssert.AreEquivalent(new[] { "AIBlockOnDecideAbility", "AIParryOnDecideAbility" }, discipline.Select(e => e.Stat).ToArray());
        CollectionAssert.AreEquivalent(new[] { "ArmorHead", "ArmorTorso", "ArmorLegs", "ArmorArms" }, hide.Select(e => e.Stat).ToArray());
        Assert.AreEqual(AffixMath.Over, drums.Single(e => e.Stat == "TopSpeedReachDuration").Math, "a faster start is a shorter time to top speed");
        Assert.AreEqual(AffixMath.Chance, lash.Single(e => e.Stat == "AIAttackOnDecideChance").Math);
        Assert.AreEqual(25f, lash.Single(e => e.Stat == "AIAttackOnDecideChance").Percent);
    }

    [TestMethod]
    public void Expectations_ForAPowerWithNoStatEffectYet_IsEmpty()
    {
        Assert.AreEqual(0, LongDefeatAffixChecks.Expectations(Plan(), "hammer").Count);
        Assert.AreEqual(0, LongDefeatAffixChecks.Expectations(Plan(), "terror").Count);
        Assert.AreEqual(0, LongDefeatAffixChecks.Expectations(Plan(), "blades").Count);
    }

    // ---- affix-effect ----

    [TestMethod]
    public void Effect_ReadingsAtTheExpectedFactor_Pass()
    {
        var sample = Sample(("SwingSpeedMultiplier", 1.0f, 1.15f), ("AIAttackOnDecideChance", 0.4f, 0.5f));

        var verdict = LongDefeatAffixChecks.Effect("lash", LongDefeatAffixChecks.Expectations(Plan(), "lash"), sample);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
        StringAssert.StartsWith(verdict.Detail, "power=lash ");
        StringAssert.Contains(verdict.Detail, "troop=fow_urukhai_fighter");
    }

    [TestMethod]
    public void Effect_AReadingThatIgnoredThePercent_Fails()
    {
        var sample = Sample(("SwingSpeedMultiplier", 1.0f, 1.0f), ("AIAttackOnDecideChance", 0.4f, 0.5f));

        var verdict = LongDefeatAffixChecks.Effect("lash", LongDefeatAffixChecks.Expectations(Plan(), "lash"), sample);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "SwingSpeedMultiplier");
    }

    [TestMethod]
    public void Effect_AChanceIsClampedToOne_SoAnAlreadyHighBaseStillPasses()
    {
        var sample = Sample(("AIBlockOnDecideAbility", 0.9f, 1.0f), ("AIParryOnDecideAbility", 0.5f, 0.625f));

        var verdict = LongDefeatAffixChecks.Effect("discipline", LongDefeatAffixChecks.Expectations(Plan(), "discipline"), sample);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
    }

    [TestMethod]
    public void Effect_ATimeIsDividedByTheFactor()
    {
        var plan = Plan();
        var sample = Sample(("MaxSpeedMultiplier", 1.0f, 1.12f), ("CombatMaxSpeedMultiplier", 0.9f, 1.008f), ("TopSpeedReachDuration", 2.5f, 2.0f));

        Assert.AreEqual(CheckResult.Pass, LongDefeatAffixChecks.Effect("drums", LongDefeatAffixChecks.Expectations(plan, "drums"), sample).Result);
        var slow = Sample(("MaxSpeedMultiplier", 1.0f, 1.12f), ("CombatMaxSpeedMultiplier", 0.9f, 1.008f), ("TopSpeedReachDuration", 2.5f, 2.5f));
        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Effect("drums", LongDefeatAffixChecks.Expectations(plan, "drums"), slow).Result);
    }

    [TestMethod]
    public void Effect_HideRaisesEveryArmourValueThatHasAnyArmour()
    {
        var plan = Plan();
        var sample = Sample(("ArmorHead", 10f, 13f), ("ArmorTorso", 20f, 26f), ("ArmorLegs", 0f, 0f), ("ArmorArms", 8f, 10.4f));

        var verdict = LongDefeatAffixChecks.Effect("hide", LongDefeatAffixChecks.Expectations(plan, "hide"), sample);

        Assert.AreEqual(CheckResult.Pass, verdict.Result, verdict.Detail);
    }

    [TestMethod]
    public void Effect_NoAttackerSampled_Fails()
    {
        var verdict = LongDefeatAffixChecks.Effect("lash", LongDefeatAffixChecks.Expectations(Plan(), "lash"), LdStatSample.None);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-sample");
    }

    [TestMethod]
    public void Effect_AReadingMissingFromTheSample_Fails()
    {
        var sample = Sample(("SwingSpeedMultiplier", 1.0f, 1.15f));

        var verdict = LongDefeatAffixChecks.Effect("lash", LongDefeatAffixChecks.Expectations(Plan(), "lash"), sample);

        Assert.AreEqual(CheckResult.Fail, verdict.Result);
        StringAssert.Contains(verdict.Detail, "AIAttackOnDecideChance=missing");
    }

    [TestMethod]
    public void Effect_WhenNoReadingCouldShowTheEffect_IsASkipNotAPass()
    {
        // A zero base (an unarmoured soldier) shows nothing: the check must not read PASS for an effect it could not see.
        var sample = Sample(("ArmorHead", 0f, 0f), ("ArmorTorso", 0f, 0f), ("ArmorLegs", 0f, 0f), ("ArmorArms", 0f, 0f));

        var verdict = LongDefeatAffixChecks.Effect("hide", LongDefeatAffixChecks.Expectations(Plan(), "hide"), sample);

        Assert.AreEqual(CheckResult.Skip, verdict.Result);
        StringAssert.Contains(verdict.Detail, "reason=no-signal");
    }

    [TestMethod]
    public void Effect_ANaNReading_Fails()
    {
        var sample = Sample(("SwingSpeedMultiplier", 1.0f, float.NaN), ("AIAttackOnDecideChance", 0.4f, 0.5f));

        Assert.AreEqual(CheckResult.Fail, LongDefeatAffixChecks.Effect("lash", LongDefeatAffixChecks.Expectations(Plan(), "lash"), sample).Result);
    }
}
