using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCamp;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The spring-chance formula: clamp01(base + concealment x 0.3 + scouting / 300), where
/// concealment = 1 - min(1, playerSpottingRange / maxRange). The first argument is the PLAYER
/// party's spotting range (source behaviour; a wider sight radius erodes the concealment term),
/// not any candidate distance. Non-finite or degenerate-range inputs give 0.
/// </summary>
[TestClass]
public class CampAmbushServiceTests
{
    private CampAmbushService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new CampAmbushService();
    }

    [TestMethod]
    public void TriggerChance_MidRangeSpotting_AddsAllThreeTerms()
    {
        // concealment = 1 - 5/10 = 0.5; 0.5 + 0.5 * 0.3 + 30/300 = 0.75
        float chance = _sut.TriggerChance(
            playerSpottingRange: 5f, maxRange: 10f, baseChance: 0.5f, scoutingSkill: 30f);

        Assert.AreEqual(0.75f, chance, 0.0001f);
    }

    [TestMethod]
    public void TriggerChance_SpottingBeyondMaxRange_ConcealmentTermZero()
    {
        float chance = _sut.TriggerChance(20f, 10f, 0.2f, 0f);

        Assert.AreEqual(0.2f, chance, 0.0001f);
    }

    [TestMethod]
    public void TriggerChance_ZeroSpotting_FullConcealmentBonus()
    {
        float chance = _sut.TriggerChance(0f, 10f, 0.2f, 0f);

        Assert.AreEqual(0.5f, chance, 0.0001f);
    }

    [TestMethod]
    public void TriggerChance_NegativeSpotting_TreatedAsZeroDistance()
    {
        float negative = _sut.TriggerChance(-5f, 10f, 0.2f, 0f);
        float zero = _sut.TriggerChance(0f, 10f, 0.2f, 0f);

        Assert.AreEqual(zero, negative, 0.0001f);
    }

    [TestMethod]
    public void TriggerChance_SumAboveOne_ClampsToOne()
    {
        // 0.9 + 0.3 + 300/300 = 2.2 -> 1
        Assert.AreEqual(1f, _sut.TriggerChance(0f, 10f, 0.9f, 300f));
    }

    [TestMethod]
    public void TriggerChance_SumBelowZero_ClampsToZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(20f, 10f, -0.5f, 0f));
    }

    [TestMethod]
    public void TriggerChance_ZeroMaxRange_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(5f, 0f, 0.5f, 100f));
    }

    [TestMethod]
    public void TriggerChance_NegativeMaxRange_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(5f, -10f, 0.5f, 100f));
    }

    [TestMethod]
    public void TriggerChance_NaNSpotting_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(float.NaN, 10f, 0.5f, 30f));
    }

    [TestMethod]
    public void TriggerChance_NaNMaxRange_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(5f, float.NaN, 0.5f, 30f));
    }

    [TestMethod]
    public void TriggerChance_NaNBaseChance_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(5f, 10f, float.NaN, 30f));
    }

    [TestMethod]
    public void TriggerChance_NaNScouting_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(5f, 10f, 0.5f, float.NaN));
    }

    [TestMethod]
    public void TriggerChance_InfiniteInputs_ReturnZero()
    {
        Assert.AreEqual(0f, _sut.TriggerChance(float.PositiveInfinity, 10f, 0.5f, 30f));
        Assert.AreEqual(0f, _sut.TriggerChance(5f, float.PositiveInfinity, 0.5f, 30f));
        Assert.AreEqual(0f, _sut.TriggerChance(5f, 10f, float.NegativeInfinity, 30f));
        Assert.AreEqual(0f, _sut.TriggerChance(5f, 10f, 0.5f, float.PositiveInfinity));
    }

    [TestMethod]
    public void AmbushedMoraleFactor_IsHalf()
    {
        Assert.AreEqual(0.5f, _sut.AmbushedMoraleFactor);
    }
}
