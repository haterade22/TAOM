using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.WarChronicle.Effects;

/// <summary>
/// The extra escape roll of a war-boosted kingdom's captured lords (docs/features/war-chronicle.md,
/// Part A). Its chance mirrors vanilla's PrisonerReleaseCampaignBehavior.DailyHeroTick (v1.5.4
/// :201-250): base 4% a day, the mobile-captor factor, half when the player holds the lord, the
/// CanHeroBeReleased veto, and the captor's anti-escape perks, the captive's Fleet Footed and the
/// captor leader's Valor as one factor read by the adapter. TAOM adds two exclusions of its own (the
/// player's clan, a captor in a battle or siege). The extra chance is that chance times (multiplier - 1).
/// </summary>
[TestClass]
public class WarEscapeServiceTests
{
    private readonly WarEscapeService _sut = new WarEscapeService();

    private static PrisonerEscapeSnapshot Eligible() => new PrisonerEscapeSnapshot
    {
        HeroId = "lord_1",
        KingdomId = "empire_w",
        IsAlive = true,
        IsPrisoner = true,
        CanBeReleased = true,
        EscapeFactor = 1f,
    };

    private static PrisonerEscapeSnapshot InTheField(int healthy)
    {
        var snapshot = Eligible();
        snapshot.CaptorIsMobile = true;
        snapshot.CaptorInSettlement = false;
        snapshot.CaptorHealthyMembers = healthy;
        return snapshot;
    }

    [TestMethod]
    public void ShouldEscape_EligibleLordAndRollZero_Escapes()
    {
        Assert.IsTrue(_sut.ShouldEscape(Eligible(), 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_NullSnapshot_DoesNotEscape()
    {
        Assert.IsFalse(_sut.ShouldEscape(null, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_NotAlive_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.IsAlive = false;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_NotAPrisoner_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.IsPrisoner = false;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_TheMainHero_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.IsMainHero = true;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_APlayerClanLord_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.IsPlayerClan = true;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_CaptorInAMapEventOrSiege_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.CaptorInMapEventOrSiege = true;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_VanillaVetoesTheRelease_DoesNotEscape()
    {
        var snapshot = Eligible();
        snapshot.CanBeReleased = false;

        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_MultiplierOne_DoesNotEscape()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 1f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_MultiplierBelowOne_DoesNotEscape()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 0.5f, 0f));
    }

    [TestMethod]
    public void ShouldEscape_SettlementCaptor_UsesTheBaseChanceTimesTheBoost()
    {
        // p = 0.04, multiplier 1.5, so the extra chance is 0.02.
        Assert.IsTrue(_sut.ShouldEscape(Eligible(), 1.5f, 0.0199f));
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 1.5f, 0.0201f));
    }

    [TestMethod]
    public void ShouldEscape_RollEqualToTheChance_DoesNotEscape()
    {
        var extra = _sut.ExtraChance(Eligible(), 1.5f);

        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 1.5f, extra));
    }

    [TestMethod]
    public void ShouldEscape_MobileCaptorInTheFieldWith81Healthy_FactorIsTwo()
    {
        // 5 - 81^0.25 = 2, so p = 0.08 and the extra chance at multiplier 2 is 0.08.
        Assert.IsTrue(_sut.ShouldEscape(InTheField(81), 2f, 0.0799f));
        Assert.IsFalse(_sut.ShouldEscape(InTheField(81), 2f, 0.0801f));
    }

    [TestMethod]
    public void ShouldEscape_MobileCaptorWithOneHealthy_FactorIsFour()
    {
        // 5 - 1^0.25 = 4, so p = 0.16 and the extra chance at multiplier 1.5 is 0.08.
        Assert.IsTrue(_sut.ShouldEscape(InTheField(1), 1.5f, 0.0799f));
        Assert.IsFalse(_sut.ShouldEscape(InTheField(1), 1.5f, 0.0801f));
    }

    [TestMethod]
    public void ExtraChance_MobileCaptorWithMoreThan81Healthy_IsCappedAt81()
    {
        Assert.AreEqual(_sut.ExtraChance(InTheField(81), 2f), _sut.ExtraChance(InTheField(5000), 2f), 1e-7f);
    }

    [TestMethod]
    public void ExtraChance_MobileCaptorWithNegativeHealthy_IsTreatedAsZero()
    {
        Assert.AreEqual(_sut.ExtraChance(InTheField(0), 2f), _sut.ExtraChance(InTheField(-4), 2f), 1e-7f);
    }

    [TestMethod]
    public void ExtraChance_MobileCaptorInsideASettlement_FactorIsOne()
    {
        var snapshot = InTheField(1);
        snapshot.CaptorInSettlement = true;

        Assert.AreEqual(0.02f, _sut.ExtraChance(snapshot, 1.5f), 1e-7f);
    }

    [TestMethod]
    public void ShouldEscape_PlayerHeldLord_HalvesTheChance()
    {
        var snapshot = Eligible();
        snapshot.PlayerHeld = true;

        // p = 0.02, multiplier 2: extra 0.02.
        Assert.IsTrue(_sut.ShouldEscape(snapshot, 2f, 0.0199f));
        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0.0201f));
    }

    [TestMethod]
    public void ExtraChance_PlayerHeldMobileCaptor_MultipliesBothFactors()
    {
        var snapshot = InTheField(81);
        snapshot.PlayerHeld = true;

        // 0.04 * 2 * 0.5 = 0.04, times (2 - 1).
        Assert.AreEqual(0.04f, _sut.ExtraChance(snapshot, 2f), 1e-7f);
    }

    [TestMethod]
    public void ShouldEscape_RollAboveTheChance_DoesNotEscape()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 3f, 0.99f));
    }

    [TestMethod]
    public void ShouldEscape_RollBelowTheChance_Escapes()
    {
        Assert.IsTrue(_sut.ShouldEscape(Eligible(), 3f, 0.001f));
    }

    [TestMethod]
    public void ShouldEscape_NaNMultiplier_FailsClosed()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), float.NaN, 0f));
    }

    [TestMethod]
    public void ShouldEscape_InfiniteMultiplier_FailsClosed()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), float.PositiveInfinity, 0f));
    }

    [TestMethod]
    public void ShouldEscape_NaNRoll_FailsClosed()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 2f, float.NaN));
    }

    [TestMethod]
    public void ShouldEscape_InfiniteRoll_FailsClosed()
    {
        Assert.IsFalse(_sut.ShouldEscape(Eligible(), 2f, float.NegativeInfinity));
    }

    [TestMethod]
    public void IsEligible_EligibleLordAndBoost_IsTrue()
    {
        Assert.IsTrue(_sut.IsEligible(Eligible(), 1.2f));
    }

    [TestMethod]
    public void IsEligible_NaNMultiplier_IsFalse()
    {
        Assert.IsFalse(_sut.IsEligible(Eligible(), float.NaN));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-0.3f)]
    public void ExtraChance_PerkFactorAtOrBelowZero_IsZero(float factor)
    {
        // Keen Sight plus Mounted Patrols on the player's party sum to -1: vanilla's chance is 0, so is ours.
        var snapshot = Eligible();
        snapshot.EscapeFactor = factor;

        Assert.AreEqual(0f, _sut.ExtraChance(snapshot, 2f));
        Assert.IsFalse(_sut.ShouldEscape(snapshot, 2f, 0f));
    }

    [DataTestMethod]
    [DataRow(0.5f, 0.01f)]   // an anti-escape perk at -0.5: 0.04 x 0.5 x (1.5 - 1)
    [DataRow(1.3f, 0.026f)]  // the captive's Fleet Footed at +0.3: 0.04 x 1.3 x (1.5 - 1)
    public void ExtraChance_FoldsThePerkFactor(float factor, float expected)
    {
        var snapshot = Eligible();
        snapshot.EscapeFactor = factor;

        Assert.AreEqual(expected, _sut.ExtraChance(snapshot, 1.5f), 1e-7f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void ExtraChance_NonFinitePerkFactor_IsZero(float factor)
    {
        var snapshot = Eligible();
        snapshot.EscapeFactor = factor;

        Assert.AreEqual(0f, _sut.ExtraChance(snapshot, 2f));
    }

    [TestMethod]
    public void ExtraChance_UnsetPerkFactor_FailsClosed()
    {
        var snapshot = Eligible();
        snapshot.EscapeFactor = new PrisonerEscapeSnapshot().EscapeFactor;

        Assert.AreEqual(0f, _sut.ExtraChance(snapshot, 2f));
    }

    [TestMethod]
    public void ExtraChance_IneligibleLord_IsZero()
    {
        var snapshot = Eligible();
        snapshot.IsMainHero = true;

        Assert.AreEqual(0f, _sut.ExtraChance(snapshot, 2f));
    }
}
