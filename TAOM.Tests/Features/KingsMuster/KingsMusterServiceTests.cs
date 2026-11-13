using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;
using TAOM.Features.KingsMuster;

namespace TAOM.Tests.Features.KingsMuster;

/// <summary>
/// The King's Muster decision matrix (#874). The service is pure: two facts and a number in, a number out. The ruler discount
/// truncates the way vanilla's own <c>(int)</c> cast does, and "an AI ruler" is decided here (ruler and not the player's hero).
/// </summary>
[TestClass]
public class KingsMusterServiceTests
{
    private IKingsMusterSettingsProvider _settings = null!;
    private IArmyTargetingConfigProvider _config = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IKingsMusterSettingsProvider>();
        _settings.DiscountEnabled.Returns(true);
        _config = Substitute.For<IArmyTargetingConfigProvider>();
        SetFactor(0.5f);
        _coop = Substitute.For<ICoopSessionProvider>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
    }

    private void SetFactor(float factor) =>
        _config.GetConfig().Returns(new ArmyTargetingConfig { RulerCallCostFactor = factor });

    private KingsMusterService Sut() => new(_settings, _config, _coop, _dedicated);

    private static int AiRuler(KingsMusterService sut, int cost) => sut.ApplyRulerDiscount(true, false, cost);

    [TestMethod]
    public void ApplyRulerDiscount_AnAiRuler_PaysTheFactorOfTheCost()
    {
        Assert.AreEqual(50, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_NotARuler_PaysTheFullCost()
    {
        Assert.AreEqual(100, Sut().ApplyRulerDiscount(false, false, 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_ThePlayerAsRuler_PaysTheFullCost()
    {
        Assert.AreEqual(100, Sut().ApplyRulerDiscount(true, true, 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_ThePlayerLeadingAsAVassal_PaysTheFullCost()
    {
        Assert.AreEqual(100, Sut().ApplyRulerDiscount(false, true, 100));
    }

    [DataTestMethod]
    [DataRow(101, 50)]
    [DataRow(1, 0)]
    [DataRow(7, 3)]
    [DataRow(3000, 1500)]
    public void ApplyRulerDiscount_TruncatesLikeVanillasIntCast(int cost, int expected)
    {
        Assert.AreEqual(expected, AiRuler(Sut(), cost));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void ApplyRulerDiscount_ZeroOrNegativeCost_IsLeftAlone(int cost)
    {
        // A same-clan call costs 0 in vanilla and stays 0; a negative cost is not ours to scale.
        Assert.AreEqual(cost, AiRuler(Sut(), cost));
    }

    [TestMethod]
    public void ApplyRulerDiscount_StacksOnTheCultureFeatResult()
    {
        // The model passes the feats' result in, so the discount is applied after the feats (120 = 100 plus a 20 percent feat).
        Assert.AreEqual(60, AiRuler(Sut(), 120));
    }

    [TestMethod]
    public void ApplyRulerDiscount_IntMaxValue_StaysInRangeAndNeverRises()
    {
        var result = AiRuler(Sut(), int.MaxValue);

        Assert.IsTrue(result >= 0 && result <= int.MaxValue / 2 + 1, "got " + result);
    }

    [TestMethod]
    public void ApplyRulerDiscount_SwitchOff_PaysTheFullCost()
    {
        _settings.DiscountEnabled.Returns(false);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_TheSwitch_IsReadLive_NotCapturedAtConstruction()
    {
        var sut = Sut();
        _settings.DiscountEnabled.Returns(false);
        Assert.AreEqual(100, AiRuler(sut, 100));

        _settings.DiscountEnabled.Returns(true);
        Assert.AreEqual(50, AiRuler(sut, 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_ACoopSession_PaysTheFullCost()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_ADeferringClient_PaysTheFullCost()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_ADedicatedServer_PaysTheFullCost()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_TheFloorFactor_PaysAQuarter()
    {
        SetFactor(0.25f);

        Assert.AreEqual(25, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void ApplyRulerDiscount_AFactorOfOne_ChangesNothing()
    {
        SetFactor(1f);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.2499f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.5f)]
    [DataRow(1.5f)]
    public void ApplyRulerDiscount_AFactorOutsideTheLegalRange_IsIgnored_NeverASurchargeNorAFreeCall(float factor)
    {
        // A validated config cannot hold one; the floor keeps the ranking key (strength / (cost + 0.1)) from flattening.
        SetFactor(factor);

        Assert.AreEqual(100, AiRuler(Sut(), 100));
    }

    [TestMethod]
    public void TheFactor_IsReadOnceWhenTheServiceIsBuilt_NotOnEveryCall()
    {
        var sut = Sut();
        _config.ClearReceivedCalls();

        AiRuler(sut, 100);
        AiRuler(sut, 200);

        _config.DidNotReceive().GetConfig();
    }

    // AppliedFactor tells the Realms liege discount what this call already got, so the two do not stack.
    [TestMethod]
    public void AppliedFactor_AnAiRuler_IsTheFactor()
    {
        Assert.AreEqual(0.5f, Sut().AppliedFactor(true, false), 1e-6f);
    }

    [TestMethod]
    public void AppliedFactor_NoDiscountCases_AreOne()
    {
        var sut = Sut();
        Assert.AreEqual(1f, sut.AppliedFactor(false, false), 1e-6f, "not a ruler");
        Assert.AreEqual(1f, sut.AppliedFactor(true, true), 1e-6f, "the player");
        _settings.DiscountEnabled.Returns(false);
        Assert.AreEqual(1f, sut.AppliedFactor(true, false), 1e-6f, "switch off");
    }

    [TestMethod]
    public void AppliedFactor_MatchesApplyRulerDiscount()
    {
        var sut = Sut();
        Assert.AreEqual(AiRuler(sut, 1000), (int)(1000 * sut.AppliedFactor(true, false)));
    }
}
