using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The hollow square's small rules (build-spec 9.1): the side that survives a unit change, and the proof that every soldier got a slot.</summary>
[TestClass]
public class MarshalSquareRulesTests
{
    [TestMethod]
    public void TryKeptSide_TheSideIsAboveTheMinimum_KeepsTheSide()
    {
        Assert.IsTrue(MarshalSquareRules.TryKeptSide(width: 20f, minimumWidth: 12f, out var side));
        Assert.AreEqual(20f, side);
    }

    [TestMethod]
    public void TryKeptSide_MoreUnitsThanTheSideHolds_GrowsToTheMinimum_SoNoSoldierIsLeftUnpositioned()
    {
        Assert.IsTrue(MarshalSquareRules.TryKeptSide(width: 12f, minimumWidth: 15f, out var side));
        Assert.AreEqual(15f, side);
    }

    [TestMethod]
    [DataRow(float.NaN, 12f)]
    [DataRow(12f, float.NaN)]
    [DataRow(float.PositiveInfinity, 12f)]
    [DataRow(12f, float.NegativeInfinity)]
    public void TryKeptSide_ANumberThatIsNotFinite_IsRefused_SoVanillaRuns(float width, float minimum)
    {
        Assert.IsFalse(MarshalSquareRules.TryKeptSide(width, minimum, out _));
    }

    [TestMethod]
    public void TryDesiredWidth_IsTheSideClampedToTheArrangementsOwnBounds()
    {
        Assert.IsTrue(MarshalSquareRules.TryDesiredWidth(20f, 12f, 30f, out var inside));
        Assert.AreEqual(20f, inside);
        Assert.IsTrue(MarshalSquareRules.TryDesiredWidth(5f, 12f, 30f, out var below));
        Assert.AreEqual(12f, below);
        Assert.IsTrue(MarshalSquareRules.TryDesiredWidth(50f, 12f, 30f, out var above));
        Assert.AreEqual(30f, above);
    }

    [TestMethod]
    [DataRow(float.NaN, 12f, 30f)]
    [DataRow(20f, float.NaN, 30f)]
    [DataRow(20f, 12f, float.PositiveInfinity)]
    public void TryDesiredWidth_ANumberThatIsNotFinite_IsRefused(float width, float minimum, float maximum)
    {
        Assert.IsFalse(MarshalSquareRules.TryDesiredWidth(width, minimum, maximum, out _));
    }

    [TestMethod]
    public void TryDesiredWidth_AMinimumAboveTheMaximum_ChoosesTheMinimum_SoTheSquareStillHoldsEverySoldier()
    {
        Assert.IsTrue(MarshalSquareRules.TryDesiredWidth(20f, 25f, 18f, out var side));
        Assert.AreEqual(25f, side);
    }

    [TestMethod]
    public void UnitsAlongSide_IsTheSoldiersThatFitInASide_AndZeroForANumberItCannotUse()
    {
        Assert.AreEqual(13, MarshalSquareRules.UnitsAlongSide(side: 14.5f, unitDiameter: 0.76f, interval: 0.38f), "(14.5 - 0.76) / 1.14 = 12.05, plus the first soldier");
        Assert.AreEqual(1, MarshalSquareRules.UnitsAlongSide(0.76f, 0.76f, 0.38f), "a side one soldier wide");
        Assert.AreEqual(0, MarshalSquareRules.UnitsAlongSide(0.5f, 0.76f, 0.38f), "a side narrower than a soldier");
        Assert.AreEqual(0, MarshalSquareRules.UnitsAlongSide(float.NaN, 0.76f, 0.38f));
        Assert.AreEqual(0, MarshalSquareRules.UnitsAlongSide(14.5f, 0f, 0f), "no pitch to divide by");
    }

    [TestMethod]
    public void ProofPasses_EverySoldierPlaced_Passes()
    {
        Assert.IsTrue(MarshalSquareRules.ProofPasses(placed: 40, units: 40));
    }

    [TestMethod]
    public void ProofPasses_OneSoldierUnplaced_Fails()
    {
        Assert.IsFalse(MarshalSquareRules.ProofPasses(39, 40));
    }

    [TestMethod]
    public void ProofPasses_NoSoldiers_ProvesNothing()
    {
        Assert.IsFalse(MarshalSquareRules.ProofPasses(0, 0));
    }

    [TestMethod]
    public void ProofLineDue_IsDueOnTheFirstSquare_AndAfterAUnitChangeOnlyOnceASecond()
    {
        Assert.IsTrue(MarshalSquareRules.ProofLineDue(now: 10.0, lastLine: double.NegativeInfinity, lastUnits: -1, units: 40));
        Assert.IsFalse(MarshalSquareRules.ProofLineDue(10.5, 10.0, 40, 40), "the count did not change");
        Assert.IsFalse(MarshalSquareRules.ProofLineDue(10.5, 10.0, 40, 39), "changed, but inside the second");
        Assert.IsTrue(MarshalSquareRules.ProofLineDue(11.0, 10.0, 40, 39));
    }
}
