using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.TroopProgression;

[TestClass]
public class WageModifierServiceTests
{
    private ITroopCostService _costService;
    private WageModifierService _sut;

    [TestInitialize]
    public void Setup()
    {
        _costService = Substitute.For<ITroopCostService>();
        _sut = new WageModifierService(_costService);
    }

    // --- ApplyWageModifiers: garrison feats ---

    [TestMethod]
    public void ApplyWageModifiers_GarrisonNotApplicable_NoGarrisonFeatsApplied()
    {
        var result = new ExplainedNumber(1000f, false);
        var garrison = new WageFeatInputs(
            isApplicable: false,
            ereborGarrisonBonus: -0.25f,
            lothlorienGarrisonBonus: -0.2f,
            isengardGarrisonBonus: -0.2f,
            gondorGarrisonBonus: -0.2f);

        _sut.ApplyWageModifiers(ref result, garrison, WageFeatInputs.None, 0f, 0f, null);

        Assert.AreEqual(1000f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_EreborGarrisonFeat_Applies25PercentReduction()
    {
        var result = new ExplainedNumber(1000f, false);
        var garrison = new WageFeatInputs(isApplicable: true, ereborGarrisonBonus: -0.25f);

        _sut.ApplyWageModifiers(ref result, garrison, WageFeatInputs.None, 0f, 0f, null);

        // ExplainedNumber.AddFactor: factor of -0.25 reduces by 25%
        Assert.AreEqual(750f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_AllGarrisonFeatsZero_LeavesBaseUnchanged()
    {
        var result = new ExplainedNumber(500f, false);
        var garrison = new WageFeatInputs(isApplicable: true); // all zero bonuses

        _sut.ApplyWageModifiers(ref result, garrison, WageFeatInputs.None, 0f, 0f, null);

        Assert.AreEqual(500f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_MultipleGarrisonFeats_AppliesAllAsAdditiveFactors()
    {
        var result = new ExplainedNumber(1000f, false);
        var garrison = new WageFeatInputs(
            isApplicable: true,
            ereborGarrisonBonus: -0.25f,
            gondorGarrisonBonus: -0.2f);

        _sut.ApplyWageModifiers(ref result, garrison, WageFeatInputs.None, 0f, 0f, null);

        // AddFactor is additive in the factor: (-0.25) + (-0.20) = -0.45 → 550
        Assert.AreEqual(550f, result.ResultNumber, 0.01f);
    }

    // --- ApplyWageModifiers: party feats ---

    [TestMethod]
    public void ApplyWageModifiers_PartyNotApplicable_NoPartyFeatsApplied()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(
            isApplicable: false,
            gundabadWageBonus: 0.1f,
            mordorWageBonus: 0.2f);

        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, 0f, 0f, null);

        Assert.AreEqual(1000f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_GundabadWageFeat_Applies10PercentIncrease()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true, gundabadWageBonus: 0.1f);

        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, 0f, 0f, null);

        Assert.AreEqual(1100f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_MordorWageFeat_Applies20PercentIncrease()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true, mordorWageBonus: 0.2f);

        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, 0f, 0f, null);

        Assert.AreEqual(1200f, result.ResultNumber, 0.01f);
    }

    // --- ApplyWageModifiers: Rohan mounted-wage share scaling ---

    [TestMethod]
    public void ApplyWageModifiers_RohanMountedWage_ScaledByShare()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true);

        // -0.15 bonus * 0.5 share = -0.075 factor → 925
        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, -0.15f, 0.5f, null);

        Assert.AreEqual(925f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_RohanFullMountedShare_AppliesFullBonus()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true);

        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, -0.15f, 1.0f, null);

        Assert.AreEqual(850f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_RohanZeroBonus_NoFactorApplied()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true);

        // Rohan feat not present (bonus=0) — should skip regardless of share value
        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, 0f, 0.8f, null);

        Assert.AreEqual(1000f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_RohanZeroShare_NoFactorApplied()
    {
        var result = new ExplainedNumber(1000f, false);
        var party = new WageFeatInputs(isApplicable: true);

        // mountedWageShare=0 → skip even when bonus is present
        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, party, -0.15f, 0f, null);

        Assert.AreEqual(1000f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_RohanPartyNotApplicable_NoFactorApplied()
    {
        var result = new ExplainedNumber(1000f, false);

        // Party feats overall not applicable — Rohan section gated too
        _sut.ApplyWageModifiers(ref result, WageFeatInputs.None, WageFeatInputs.None, -0.15f, 1.0f, null);

        Assert.AreEqual(1000f, result.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void ApplyWageModifiers_GarrisonAndPartyTogether_BothApply()
    {
        // Real-world case: Gondor garrison party with the GondorGarrisonWageFeat,
        // owner-culture also Gondor — but Gondor has no party-wage feat. So this
        // verifies the two sections compose correctly when garrison-only feats fire.
        var result = new ExplainedNumber(1000f, false);
        var garrison = new WageFeatInputs(isApplicable: true, gondorGarrisonBonus: -0.2f);
        var party = new WageFeatInputs(isApplicable: true); // owner Gondor — no party wage feat

        _sut.ApplyWageModifiers(ref result, garrison, party, 0f, 0f, null);

        Assert.AreEqual(800f, result.ResultNumber, 0.01f);
    }

    // --- CalculateRecruitmentCost: composition ---

    [TestMethod]
    public void CalculateRecruitmentCost_NotMounted_UsesBaseCostOnly()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(400);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(400, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedLowLevel_Adds150HorseCost()
    {
        _costService.GetTroopRecruitmentCost(15, false).Returns(200);

        var cost = _sut.CalculateRecruitmentCost(
            level: 15, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(350, cost); // 200 + 150
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedHighLevel_Adds500HorseCost()
    {
        _costService.GetTroopRecruitmentCost(30, false).Returns(1000);

        var cost = _sut.CalculateRecruitmentCost(
            level: 30, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(1500, cost); // 1000 + 500
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedThresholdLevel26_Adds500HorseCost()
    {
        _costService.GetTroopRecruitmentCost(26, false).Returns(600);

        var cost = _sut.CalculateRecruitmentCost(
            level: 26, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(1100, cost); // 600 + 500
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedWithoutItemCost_SkipsHorseCost()
    {
        _costService.GetTroopRecruitmentCost(30, false).Returns(1000);

        var cost = _sut.CalculateRecruitmentCost(
            level: 30, isMounted: true, isMercenary: false, withoutItemCost: true,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(1000, cost); // no horse cost added
    }

    [TestMethod]
    public void CalculateRecruitmentCost_Mercenary_PassedThroughToCostService()
    {
        _costService.GetTroopRecruitmentCost(20, true).Returns(800);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: true, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, cultureText: null);

        Assert.AreEqual(800, cost);
        _costService.Received(1).GetTroopRecruitmentCost(20, true);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedWithIsengardFeat_Applies15PercentReduction()
    {
        _costService.GetTroopRecruitmentCost(30, false).Returns(1000);
        var feats = new MountedCostFeatInputs(isengardMountedCostBonus: -0.15f, rohanMountedCostBonus: 0f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 30, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: feats, cultureText: null);

        // baseline 1000 + horse 500 = 1500, then -0.15 factor → 1275
        Assert.AreEqual(1275, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MountedWithRohanFeat_Applies15PercentReduction()
    {
        _costService.GetTroopRecruitmentCost(30, false).Returns(1000);
        var feats = new MountedCostFeatInputs(isengardMountedCostBonus: 0f, rohanMountedCostBonus: -0.15f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 30, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: feats, cultureText: null);

        Assert.AreEqual(1275, cost); // 1500 * 0.85
    }

    [TestMethod]
    public void CalculateRecruitmentCost_UnmountedWithMountedFeats_FeatsIgnored()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(400);
        var feats = new MountedCostFeatInputs(isengardMountedCostBonus: -0.15f, rohanMountedCostBonus: -0.15f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: feats, cultureText: null);

        Assert.AreEqual(400, cost);
    }

    // --- CalculateRecruitmentCost: buyer-hero perk discounts ---
    // Mirrors the vanilla `if (buyerHero != null)` block in DefaultPartyWageModel.GetTroopRecruitmentCost.
    // The model resolves each `buyerHero.GetPerkValue(perk) ? bonus : 0f` at the boundary; the service
    // applies the troop-type / tier / leader / mercenary gating. One test per gate (skip-guard exhaustion).

    [TestMethod]
    public void CalculateRecruitmentCost_NoBuyer_NoPerkFactorApplied()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(400);

        // Pre-gated bonuses present but HasBuyer=false → nothing applies (matches vanilla null buyerHero).
        var perks = new RecruitmentPerkInputs(
            hasBuyer: false, isInfantry: true, chinkInTheArmorBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(400, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_InfantryBuyerWithChinkInTheArmor_AppliesReduction()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isInfantry: true, chinkInTheArmorBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(900, cost); // 1000 * (1 - 0.10)
    }

    [TestMethod]
    public void CalculateRecruitmentCost_InfantryBuyer_AppliesAllThreeInfantryPerks()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isInfantry: true,
            chinkInTheArmorBonus: -0.1f, showOfStrengthBonus: -0.1f, hardyFrontlineBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(700, cost); // 1000 * (1 - 0.30)
    }

    [TestMethod]
    public void CalculateRecruitmentCost_RangedBuyer_AppliesArcherAndPiercer()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isRanged: true,
            renownedArcherBonus: -0.1f, piercerBonus: -0.05f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(850, cost); // 1000 * (1 - 0.15)
    }

    [TestMethod]
    public void CalculateRecruitmentCost_RangedBuyer_DoesNotApplyInfantryPerks()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        // IsRanged path — the else-if means infantry bonuses are ignored even if non-zero.
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isInfantry: false, isRanged: true,
            chinkInTheArmorBonus: -0.5f, renownedArcherBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(900, cost); // only the archer -0.10 applies, not the -0.50 infantry perk
    }

    [TestMethod]
    public void CalculateRecruitmentCost_InfantryBuyer_DoesNotApplyRangedPerks()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isInfantry: true, isRanged: false,
            chinkInTheArmorBonus: -0.1f, renownedArcherBonus: -0.5f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(900, cost); // only the infantry -0.10 applies, not the -0.50 ranged perk
    }

    [TestMethod]
    public void CalculateRecruitmentCost_HeadHunterTierBelow2_NotApplied()
    {
        _costService.GetTroopRecruitmentCost(1, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, tierAtLeast2: false, headHunterBonus: -0.2f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 1, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(1000, cost); // tier < 2 gate blocks HeadHunter
    }

    [TestMethod]
    public void CalculateRecruitmentCost_HeadHunterTier2OrAbove_Applied()
    {
        _costService.GetTroopRecruitmentCost(10, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, tierAtLeast2: true, headHunterBonus: -0.2f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 10, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(800, cost); // 1000 * (1 - 0.20)
    }

    [TestMethod]
    public void CalculateRecruitmentCost_PartyLeaderWithFrugal_Applied()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isPartyLeader: true, frugalBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(900, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_NotPartyLeader_FrugalIgnored()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isPartyLeader: false, frugalBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(1000, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_MercenaryWithTradePerks_Applied()
    {
        _costService.GetTroopRecruitmentCost(20, true).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isMercenary: true,
            swordForBarterBonus: -0.1f, slickNegotiatorBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: true, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(800, cost); // 1000 * (1 - 0.20)
    }

    [TestMethod]
    public void CalculateRecruitmentCost_NotMercenary_TradePerksIgnored()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(1000);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isMercenary: false,
            swordForBarterBonus: -0.1f, slickNegotiatorBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(1000, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_DeepDiscount_ClampedToMinimumOne()
    {
        _costService.GetTroopRecruitmentCost(1, false).Returns(10);
        // Pathological stacked discount drives the raw result negative; vanilla LimitMin(1f) floors it.
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isInfantry: true,
            chinkInTheArmorBonus: -1.0f, showOfStrengthBonus: -1.0f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 1, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(1, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_BuyerPerksComposeWithMountedFeats()
    {
        _costService.GetTroopRecruitmentCost(30, false).Returns(1000);
        var feats = new MountedCostFeatInputs(isengardMountedCostBonus: -0.15f, rohanMountedCostBonus: 0f);
        var perks = new RecruitmentPerkInputs(
            hasBuyer: true, isPartyLeader: true, frugalBonus: -0.1f);

        var cost = _sut.CalculateRecruitmentCost(
            level: 30, isMounted: true, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: feats, buyerPerks: perks, cultureText: null);

        // base 1000 + horse 500 = 1500; factors (-0.15 Isengard) + (-0.10 Frugal) = -0.25 → 1125
        Assert.AreEqual(1125, cost);
    }

    [TestMethod]
    public void CalculateRecruitmentCost_BuyerWithNoActivePerks_LeavesCostUnchanged()
    {
        _costService.GetTroopRecruitmentCost(20, false).Returns(400);
        // HasBuyer but every bonus is zero (hero has none of the perks). LimitMin(1f) is a no-op here.
        var perks = new RecruitmentPerkInputs(hasBuyer: true, isInfantry: true);

        var cost = _sut.CalculateRecruitmentCost(
            level: 20, isMounted: false, isMercenary: false, withoutItemCost: false,
            mountedCostFeats: MountedCostFeatInputs.None, buyerPerks: perks, cultureText: null);

        Assert.AreEqual(400, cost);
    }

    // --- CalculateHorseCost: tier lookup ---

    [TestMethod]
    [DataRow(1, 150)]
    [DataRow(15, 150)]
    [DataRow(25, 150)]
    [DataRow(26, 500)]
    [DataRow(30, 500)]
    [DataRow(55, 500)]
    public void CalculateHorseCost_ByLevel_ReturnsExpectedCost(int level, int expected)
    {
        Assert.AreEqual(expected, _sut.CalculateHorseCost(level));
    }
}
