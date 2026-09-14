using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class PlateAlphaPolicyTests
{
    [TestMethod]
    public void TryTakeChange_FirstCall_ReturnsTrueAndStores()
    {
        float lastAlpha = float.NaN, lastColorFactor = float.NaN;

        var changed = PlateAlphaPolicy.TryTakeChange(0.35f, 1f, ref lastAlpha, ref lastColorFactor);

        Assert.IsTrue(changed);
        Assert.AreEqual(0.35f, lastAlpha);
        Assert.AreEqual(1f, lastColorFactor);
    }

    [TestMethod]
    public void TryTakeChange_SameValues_ReturnsFalse()
    {
        float lastAlpha = 0.35f, lastColorFactor = 1f;

        Assert.IsFalse(PlateAlphaPolicy.TryTakeChange(0.35f, 1f, ref lastAlpha, ref lastColorFactor));
        Assert.AreEqual(0.35f, lastAlpha);
        Assert.AreEqual(1f, lastColorFactor);
    }

    [TestMethod]
    public void TryTakeChange_AlphaChanged_ReturnsTrue()
    {
        float lastAlpha = 0.35f, lastColorFactor = 1f;

        Assert.IsTrue(PlateAlphaPolicy.TryTakeChange(0.5f, 1f, ref lastAlpha, ref lastColorFactor));
        Assert.AreEqual(0.5f, lastAlpha);
    }

    [TestMethod]
    public void TryTakeChange_ColorFactorChanged_ReturnsTrue()
    {
        float lastAlpha = 0.8f, lastColorFactor = 1f;

        Assert.IsTrue(PlateAlphaPolicy.TryTakeChange(0.8f, 1.3f, ref lastAlpha, ref lastColorFactor));
        Assert.AreEqual(1.3f, lastColorFactor);
    }

    [TestMethod]
    public void TryTakeChange_NaNAlpha_ReturnsFalseAndKeepsLast()
    {
        float lastAlpha = 0.35f, lastColorFactor = 1f;

        Assert.IsFalse(PlateAlphaPolicy.TryTakeChange(float.NaN, 1f, ref lastAlpha, ref lastColorFactor));
        Assert.AreEqual(0.35f, lastAlpha);
        Assert.AreEqual(1f, lastColorFactor);
    }

    [TestMethod]
    public void TryTakeChange_InfinityColorFactor_ReturnsFalseAndKeepsLast()
    {
        float lastAlpha = 0.35f, lastColorFactor = 1f;

        Assert.IsFalse(PlateAlphaPolicy.TryTakeChange(0.35f, float.PositiveInfinity, ref lastAlpha, ref lastColorFactor));
        Assert.IsFalse(PlateAlphaPolicy.TryTakeChange(0.35f, float.NegativeInfinity, ref lastAlpha, ref lastColorFactor));
        Assert.AreEqual(0.35f, lastAlpha);
        Assert.AreEqual(1f, lastColorFactor);
    }

    [TestMethod]
    public void TextAlphaFor_AtOrAboveVanillaMinimum_ReturnsOne()
    {
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.35f), 0.0001f);
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.5f), 0.0001f);
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.8f), 0.0001f);
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(1f), 0.0001f);
    }

    [TestMethod]
    public void TextAlphaFor_HalfOfMinimum_ReturnsHalf()
        => Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f), 0.0001f);

    [TestMethod]
    public void TextAlphaFor_Zero_ReturnsZero()
        => Assert.AreEqual(0f, PlateAlphaPolicy.TextAlphaFor(0f), 0.0001f);

    [TestMethod]
    public void TextAlphaFor_NonFinite_ReturnsOne()
    {
        // A poisoned plate alpha must never hide the name; full text alpha is the safe answer.
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(float.NaN));
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(float.PositiveInfinity));
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(float.NegativeInfinity));
    }

    [TestMethod]
    public void TextAlphaFor_RestingBelowVanilla_KeepsTextOpaqueAtTheRestingValue()
    {
        // A player's 10% neutral plate settles at 0.10; the name must stay fully opaque there and
        // follow the plate only once the fade takes it lower (Codex F1 / data flow, #596).
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.10f, 0.10f), 0.0001f);
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.20f, 0.20f), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.05f, 0.10f), 0.0001f);
        Assert.AreEqual(0f, PlateAlphaPolicy.TextAlphaFor(0f, 0.10f), 0.0001f);
    }

    [TestMethod]
    public void TextAlphaFor_RestingAtOrAboveVanilla_KeepsVanillaAnchor()
    {
        // The default coloured curve (resting 0.5) is unchanged: text fades only below 0.35.
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.35f, 0.5f), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, 0.5f), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, 0.35f), 0.0001f);
        Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(0.8f, 1f), 0.0001f);
    }

    [TestMethod]
    public void TextAlphaFor_NonFiniteOrNonPositiveResting_UsesVanillaAnchor()
    {
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, float.NaN), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, float.PositiveInfinity), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, 0f), 0.0001f);
        Assert.AreEqual(0.5f, PlateAlphaPolicy.TextAlphaFor(0.175f, -1f), 0.0001f);
    }

    [TestMethod]
    public void TextAlphaFor_NonFinitePlateAlpha_ReturnsOneForAnyResting()
        => Assert.AreEqual(1f, PlateAlphaPolicy.TextAlphaFor(float.NaN, 0.10f));

    [TestMethod]
    public void NeedsWrite_CurrentEqualsTarget_ReturnsFalse()
        => Assert.IsFalse(PlateAlphaPolicy.NeedsWrite(1f, 1f));

    [TestMethod]
    public void NeedsWrite_CurrentWithinTolerance_ReturnsFalse()
        => Assert.IsFalse(PlateAlphaPolicy.NeedsWrite(0.9995f, 1f));

    [TestMethod]
    public void NeedsWrite_CurrentBeyondTolerance_ReturnsTrue()
    {
        Assert.IsTrue(PlateAlphaPolicy.NeedsWrite(0.5f, 1f));
        Assert.IsTrue(PlateAlphaPolicy.NeedsWrite(1f, 0.5f));
    }

    [TestMethod]
    public void NeedsWrite_NonFiniteCurrent_ReturnsTrue()
    {
        // A NaN already in the brush makes Math.Abs(NaN - target) > tolerance false; the poisoned
        // destination is itself the reason to write the finite recovery value.
        Assert.IsTrue(PlateAlphaPolicy.NeedsWrite(float.NaN, 1f));
        Assert.IsTrue(PlateAlphaPolicy.NeedsWrite(float.PositiveInfinity, 1f));
        Assert.IsTrue(PlateAlphaPolicy.NeedsWrite(float.NegativeInfinity, 0f));
    }

    [TestMethod]
    public void VanillaMinimumPlateAlpha_MatchesEngineNeutralTarget()
    {
        // SettlementNameplateWidget._normalNeutralAlphaTarget => 0.35f (v1.4.8 dump line 67).
        Assert.AreEqual(0.35f, PlateAlphaPolicy.VanillaMinimumPlateAlpha);
    }
}
