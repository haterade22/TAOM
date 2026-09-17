using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;

namespace TAOM.Tests.Features.CultureDoctrine;

[TestClass]
public class BraceDecisionTests
{
    private static WallSituation Situation(bool atPosition = true, bool hasShield = true, bool charge = false, bool underFire = false, bool armsLength = false, bool shieldBand = false)
        => new WallSituation(atPosition, hasShield, charge, underFire, armsLength, shieldBand);

    [TestMethod]
    public void Defend_ChargeInbound_IsSquare_WhetherOrNotAtPosition()
    {
        Assert.AreEqual(WallStance.Square, BraceDecision.Defend(Situation(charge: true), bracedRecently: false));
        Assert.AreEqual(WallStance.Square, BraceDecision.Defend(Situation(atPosition: false, charge: true), bracedRecently: false));
    }

    [TestMethod]
    public void Defend_BracedRecently_HoldsTheSquareAfterTheSignalEnds()
        => Assert.AreEqual(WallStance.Square, BraceDecision.Defend(Situation(), bracedRecently: true));

    [TestMethod]
    public void Defend_Marching_IsLoose()
        => Assert.AreEqual(WallStance.Loose, BraceDecision.Defend(Situation(atPosition: false), bracedRecently: false));

    [TestMethod]
    public void Defend_AtPositionWithShields_IsShieldWall_EvenUnderFire()
    {
        Assert.AreEqual(WallStance.ShieldWall, BraceDecision.Defend(Situation(), bracedRecently: false));
        Assert.AreEqual(WallStance.ShieldWall, BraceDecision.Defend(Situation(underFire: true), bracedRecently: false));
    }

    [TestMethod]
    public void Defend_AtPositionWithoutShields_IsLine_ButLoosensUnderFireAtADistance()
    {
        Assert.AreEqual(WallStance.Line, BraceDecision.Defend(Situation(hasShield: false), bracedRecently: false));
        Assert.AreEqual(WallStance.Loose, BraceDecision.Defend(Situation(hasShield: false, underFire: true), bracedRecently: false));
        Assert.AreEqual(WallStance.Line, BraceDecision.Defend(Situation(hasShield: false, underFire: true, armsLength: true), bracedRecently: false), "too late to loosen with the enemy at arm's length");
    }

    [TestMethod]
    public void Advance_ChargeInbound_IsSquare()
        => Assert.AreEqual(WallStance.Square, BraceDecision.Advance(Situation(atPosition: false, charge: true), bracedRecently: false));

    [TestMethod]
    public void Advance_BracedRecently_HoldsTheSquareAfterTheSignalEnds()
        => Assert.AreEqual(WallStance.Square, BraceDecision.Advance(Situation(atPosition: false), bracedRecently: true));

    [TestMethod]
    public void Advance_MarchingOutOfTheBand_IsLine()
    {
        Assert.AreEqual(WallStance.Line, BraceDecision.Advance(Situation(atPosition: false), bracedRecently: false));
        Assert.AreEqual(WallStance.Line, BraceDecision.Advance(Situation(atPosition: false, underFire: true), bracedRecently: false), "under fire but outside the band: keep marching in a line");
        Assert.AreEqual(WallStance.Line, BraceDecision.Advance(Situation(atPosition: false, shieldBand: true), bracedRecently: false), "inside the band but nobody is shooting");
    }

    [TestMethod]
    public void Advance_InTheBandUnderFire_ClosesShields_OrLoosensWithoutThem()
    {
        Assert.AreEqual(WallStance.ShieldWall, BraceDecision.Advance(Situation(atPosition: false, underFire: true, shieldBand: true), bracedRecently: false));
        Assert.AreEqual(WallStance.Loose, BraceDecision.Advance(Situation(atPosition: false, hasShield: false, underFire: true, shieldBand: true), bracedRecently: false));
    }

    [TestMethod]
    public void BracedRecently_HoldsForTheWindow_AndNeverBeforeASignal()
    {
        Assert.IsFalse(BraceDecision.BracedRecently(now: 100f, lastChargeSignalTime: -1f), "no signal yet");
        Assert.IsTrue(BraceDecision.BracedRecently(now: 100f, lastChargeSignalTime: 100f));
        Assert.IsTrue(BraceDecision.BracedRecently(now: 102.9f, lastChargeSignalTime: 100f));
        Assert.IsFalse(BraceDecision.BracedRecently(now: 103f, lastChargeSignalTime: 100f));
        Assert.IsFalse(BraceDecision.BracedRecently(now: float.NaN, lastChargeSignalTime: 100f), "a NaN clock never braces");
    }
}
