using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CastleRecruitment;

namespace TAOM.Tests.Features.CastleRecruitment;

[TestClass]
public class CastleRecruitmentServiceTests
{
    private ICastleRecruitmentSettingsProvider _settings = null!;
    private CastleRecruitmentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICastleRecruitmentSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.IsAiEnabled.Returns(true);
        _settings.NotablesPerCastle.Returns(3);
        _sut = new CastleRecruitmentService(_settings);
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
        var p = _sut.GetSlotProductionProbability(0);
        Assert.IsTrue(p > 0f && p < 1f, $"expected (0,1), got {p}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_MonotonicallyDecreasing()
    {
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(_sut.GetSlotProductionProbability(i) > _sut.GetSlotProductionProbability(i + 1),
                $"slot {i} should exceed slot {i + 1}");
    }

    [TestMethod]
    public void GetSlotProductionProbability_NegativeIndex_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.GetSlotProductionProbability(-1), 0.0001f);
    }

    [TestMethod]
    public void GetSlotProductionProbability_AllSlotsWithinUnitInterval()
    {
        for (int i = 0; i < 6; i++)
        {
            var p = _sut.GetSlotProductionProbability(i);
            Assert.IsTrue(p >= 0f && p <= 1f, $"slot {i} probability {p} outside [0,1]");
        }
    }

    private static int Sum(System.Collections.Generic.IReadOnlyDictionary<CastleNotableOccupation, int> d)
    {
        int s = 0;
        foreach (var kvp in d)
            s += kvp.Value;
        return s;
    }
}
