using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CulturalFeats;
using TAOM.Features.TroopProgression;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Tests.Features.CastleRecruitment;

[TestClass]
public class CastleRecruitmentServiceTests
{
    private ICastleRecruitmentSettingsProvider _settings = null!;
    private IWarEffectService _war = null!;
    private CastleRecruitmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICastleRecruitmentSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.IsAiEnabled.Returns(true);
        _settings.NotablesPerCastle.Returns(3);
        _war = Substitute.For<IWarEffectService>();
        _war.GetMultiplier(Arg.Any<string?>(), Arg.Any<WarEffectKind>()).Returns(1f);
        // The real production service: with no culture it runs no engine code, so this class needs no game.
        _sut = new CastleRecruitmentService(
            _settings, new VolunteerProductionService(Substitute.For<ICulturalFeatsService>(), _war));
    }

    // --- IsEnabled / IsAiEnabled gating ---

    [TestMethod]
    public void IsEnabled_MasterOn_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsEnabled);
    }

    [TestMethod]
    public void IsEnabled_MasterOff_ReturnsFalse()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOnAiOn_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsAiEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOffAiOn_ReturnsFalse()
    {
        // Master gate must dominate: AI castle recruitment off when the feature is off.
        _settings.IsEnabled.Returns(false);
        _settings.IsAiEnabled.Returns(true);
        Assert.IsFalse(_sut.IsAiEnabled);
    }

    [TestMethod]
    public void IsAiEnabled_MasterOnAiOff_ReturnsFalse()
    {
        _settings.IsAiEnabled.Returns(false);
        Assert.IsFalse(_sut.IsAiEnabled);
    }

    // --- GetOccupationTargets distribution (round-robin over GangLeader/Headman/Merchant/Artisan) ---

    [TestMethod]
    public void GetOccupationTargets_Three_OneEachOfFirstThree()
    {
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.IsFalse(t.ContainsKey(CastleNotableOccupation.Artisan));
        Assert.AreEqual(3, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_One_OnlyGangLeader()
    {
        _settings.NotablesPerCastle.Returns(1);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Four_OneEach()
    {
        _settings.NotablesPerCastle.Returns(4);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(1, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Artisan]);
        Assert.AreEqual(4, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Five_WrapsToSecondGangLeader()
    {
        _settings.NotablesPerCastle.Returns(5);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(2, t[CastleNotableOccupation.GangLeader]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Headman]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Merchant]);
        Assert.AreEqual(1, t[CastleNotableOccupation.Artisan]);
        Assert.AreEqual(5, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Zero_Empty()
    {
        _settings.NotablesPerCastle.Returns(0);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(0, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_Negative_TreatedAsZero()
    {
        _settings.NotablesPerCastle.Returns(-3);
        var t = _sut.GetOccupationTargets();
        Assert.AreEqual(0, Sum(t));
    }

    [TestMethod]
    public void GetOccupationTargets_NeverContainsRuralNotable()
    {
        // RuralNotable would NRE in vanilla GetBasicVolunteer for a castle notable.
        _settings.NotablesPerCastle.Returns(5);
        var t = _sut.GetOccupationTargets();
        // The enum has no RuralNotable; this asserts the distribution only uses castle-safe slots.
        Assert.IsTrue(t.Count <= 4);
    }

    // --- GetSlotProductionProbability ---

    [TestMethod]
    public void GetSlotProductionProbability_Slot0_PositiveAndBelowOne()
    {
        var p = _sut.GetSlotProductionProbability(0, null, false);
        Assert.IsTrue(p > 0f && p < 1f, $"expected (0,1), got {p}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_MonotonicallyDecreasing()
    {
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(_sut.GetSlotProductionProbability(i, null, false) > _sut.GetSlotProductionProbability(i + 1, null, false),
                $"slot {i} should exceed slot {i + 1}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_NegativeIndex_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.GetSlotProductionProbability(-1, null, false), 0.0001f);
    }

    [TestMethod]
    public void GetSlotProductionProbability_AllSlotsWithinUnitInterval()
    {
        for (int i = 0; i < 6; i++)
        {
            var p = _sut.GetSlotProductionProbability(i, null, false);
            Assert.IsTrue(p >= 0f && p <= 1f, $"slot {i} probability {p} outside [0,1]");
        }
    }

    [TestMethod]
    public void GetSlotProductionProbability_NoKingdom_IsTheFixedCurve()
    {
        for (int i = 0; i < 6; i++)
            Assert.AreEqual(Curve(i), _sut.GetSlotProductionProbability(i, null, false), 1e-6f, $"slot {i}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_KingdomWithAVolunteerEffect_ScalesTheCurve()
    {
        _war.GetMultiplier("k1", WarEffectKind.VolunteerRate).Returns(1.2f);

        for (int i = 0; i < 6; i++)
            Assert.AreEqual(Curve(i) * 1.2f, _sut.GetSlotProductionProbability(i, "k1", false), 1e-6f, $"slot {i}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_PlayerClanCastle_IgnoresTheVolunteerEffect()
    {
        _war.GetMultiplier("k1", WarEffectKind.VolunteerRate).Returns(1.2f);

        Assert.AreEqual(Curve(0), _sut.GetSlotProductionProbability(0, "k1", true), 1e-6f);
    }

    [TestMethod]
    public void GetSlotProductionProbability_NegativeIndex_AsksNoMultiplier()
    {
        _sut.GetSlotProductionProbability(-1, "k1", false);

        _war.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    private static float Curve(int slot) => 0.75f * (float)System.Math.Pow(0.85, slot + 1);

    private static int Sum(System.Collections.Generic.IReadOnlyDictionary<CastleNotableOccupation, int> d)
    {
        int s = 0;
        foreach (var kvp in d)
            s += kvp.Value;
        return s;
    }
}
