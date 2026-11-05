using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>What the boons owned do to the run's numbers (phase2-plan.md section 2, "Boons"): the gold factors, the effective rules and the affix cut. Pure.</summary>
[TestClass]
public class RunModifiersBoonTests
{
    private static BoonRow Boon(string id, params (string Key, float Value)[] effects) =>
        new(id, BoonCategory.WarChest, "{=K}" + id, "{=T}text", effects.Select(e => new BoonEffect(e.Key, e.Value)).ToList());

    private static BoonRow WithDrawback(BoonRow row, params (string Key, float Value)[] drawback) =>
        row with { Drawback = drawback.Select(e => new BoonEffect(e.Key, e.Value)).ToList(), DrawbackText = "{=D}drawback" };

    [TestMethod]
    public void NoBoons_LeaveEveryNumberAsItWas()
    {
        var rules = RunRules.Default;

        Assert.AreEqual(rules, RunModifiers.Rules(rules, Array.Empty<BoonRow>()));
        Assert.AreEqual(GoldFactors.One, RunModifiers.Gold(Array.Empty<BoonRow>()));
        Assert.AreEqual(0, RunModifiers.AffixCut(Array.Empty<BoonRow>()));
        Assert.AreEqual(0, RunModifiers.GoldPaid(Array.Empty<BoonRow>()));
    }

    [TestMethod]
    public void WarChestPercents_SumIntoOneFactorEach_DrawbacksIncluded()
    {
        var owned = new[]
        {
            WithDrawback(Boon("a", (BoonKeys.BountyPercent, 30f)), (BoonKeys.WaveBonusPercent, -15f)),
            Boon("b", (BoonKeys.BountyPercent, 20f), (BoonKeys.PricePercent, -20f)),
            Boon("c", (BoonKeys.EarlyGoldPercent, 100f)),
        };

        var f = RunModifiers.Gold(owned);

        Assert.AreEqual(1.5f, f.Bounty, 1e-5f);
        Assert.AreEqual(0.85f, f.WaveBonus, 1e-5f);
        Assert.AreEqual(2.0f, f.EarlyGold, 1e-5f);
        Assert.AreEqual(0.8f, f.Price, 1e-5f);
    }

    [TestMethod]
    public void AFactor_NeverGoesBelowZero()
    {
        var f = RunModifiers.Gold(new[] { Boon("a", (BoonKeys.BountyPercent, -60f)), Boon("b", (BoonKeys.BountyPercent, -60f)) });

        Assert.AreEqual(0f, f.Bounty);
    }

    [TestMethod]
    public void GoldPaid_IsTheSumOfTheGoldEffectsOwned()
    {
        var owned = new[] { Boon("a", (BoonKeys.Gold, 300f)), Boon("b", (BoonKeys.Gold, 150f), (BoonKeys.BountyPercent, 5f)) };

        Assert.AreEqual(450, RunModifiers.GoldPaid(owned));
    }

    [TestMethod]
    public void FortressKeys_FoldIntoTheRules_AndEveryOtherNumberIsUntouched()
    {
        var owned = new[]
        {
            Boon("a", (BoonKeys.GarrisonCap, 30f), (BoonKeys.IntermissionSeconds, 20f)),
            Boon("b", (BoonKeys.RestShare, 0.25f), (BoonKeys.KeepHoldPercent, 40f), (BoonKeys.PackBonus, 3f)),
        };

        var rules = RunModifiers.Rules(RunRules.Default, owned);

        Assert.AreEqual(250, rules.GarrisonCap);
        Assert.AreEqual(80f, rules.IntermissionSeconds, 1e-4f);
        Assert.AreEqual(0.75f, rules.RestShare, 1e-5f);
        Assert.AreEqual(63f, rules.KeepHoldSeconds, 1e-4f, "45 s plus 40 percent");
        Assert.AreEqual(3, rules.PackBonus);
        Assert.AreEqual(RunRules.Default.WaveBonusBase, rules.WaveBonusBase);
        Assert.AreEqual(RunRules.Default.LiveCap, rules.LiveCap, "boons never touch the live room");
    }

    [TestMethod]
    public void TheRulesFold_HoldsItsFloors_SoNoBoonStrandsTheShopOrTheGarrison()
    {
        var owned = new[]
        {
            WithDrawback(Boon("a", (BoonKeys.GarrisonCap, -500f)), (BoonKeys.IntermissionSeconds, -500f)),
            Boon("b", (BoonKeys.RestShare, 5f), (BoonKeys.KeepHoldPercent, -90f), (BoonKeys.PackBonus, -50f)),
        };

        var rules = RunModifiers.Rules(RunRules.Default, owned);

        Assert.AreEqual(1, rules.GarrisonCap);
        Assert.AreEqual(5f, rules.IntermissionSeconds, "a shop that stays open");
        Assert.AreEqual(1f, rules.RestShare, "a share, never above all of it");
        Assert.AreEqual(5f, rules.KeepHoldSeconds);
        Assert.AreEqual(-50, rules.PackBonus, "kept as owned; the pack size floor of 1 is held where a pack is bought");
    }

    [TestMethod]
    public void TheAffixCut_IsTheSumOfTheNegativeAffixCountsOwned()
    {
        var owned = new[] { Boon("a", (BoonKeys.AffixCount, -1f)), Boon("b", (BoonKeys.AffixCount, -2f)), Boon("c", (BoonKeys.BountyPercent, 5f)) };

        Assert.AreEqual(-3, RunModifiers.AffixCut(owned));
    }

    [TestMethod]
    public void Apply_RoundsHalfAwayFromZero_AndLeavesAZeroAmountAlone()
    {
        Assert.AreEqual(5, GoldFactors.Apply(4, 1.3f));
        Assert.AreEqual(195, GoldFactors.Apply(150, 1.3f));
        Assert.AreEqual(0, GoldFactors.Apply(0, 3f));
        Assert.AreEqual(7, GoldFactors.Apply(4, 1.75f));
        Assert.AreEqual(0, GoldFactors.Apply(100, 0f));
        Assert.AreEqual(180, GoldFactors.Apply(180, 1f));
    }

    [TestMethod]
    public void TheVocabulary_IsClosed_AndNamesEveryKeyOfSlices4To7()
    {
        var expected = new[]
        {
            "bountyPercent", "waveBonusPercent", "earlyGoldPercent", "pricePercent", "gold",
            "garrisonCap", "intermissionSeconds", "restShare", "keepHoldPercent", "packBonus", "affixCount",
            "swingSpeedPercent", "moveSpeedPercent", "drawSpeedPercent", "reloadSpeedPercent", "blockPercent", "parryPercent", "aimErrorPercent",
            "damagePercent", "damageTakenPercent", "healthPercent", "commanderCooldownPercent",
        };

        CollectionAssert.AreEquivalent(expected, BoonKeys.Rules.Keys.ToArray());
        Assert.IsTrue(BoonKeys.Rules["gold"].Whole);
        Assert.IsTrue(BoonKeys.Rules["packBonus"].Whole);
        Assert.IsFalse(BoonKeys.Rules["restShare"].Whole);
        Assert.IsTrue(BoonKeys.Rules["affixCount"].Max < 0, "affixCount is negative only");
        Assert.IsTrue(BoonKeys.Rules.Keys.Where(BoonKeys.IsTargeted).All(k => !BoonKeys.Rules[k].Whole), "the troop and class percents are not whole numbers");
        CollectionAssert.AreEquivalent(
            new[] { "swingSpeedPercent", "moveSpeedPercent", "drawSpeedPercent", "reloadSpeedPercent", "blockPercent", "parryPercent", "aimErrorPercent", "damagePercent", "damageTakenPercent", "healthPercent" },
            BoonKeys.Rules.Keys.Where(BoonKeys.IsTargeted).ToArray(), "exactly the stat, damage and health keys name a target");
    }
}
