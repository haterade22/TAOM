using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>How much of each side's reserve may be released by each stage, and the softlock override.</summary>
[TestClass]
public class ReserveBudgetTests
{
    private static ReserveShares Shares => StagedSiegeTunables.Default.ReserveShares;

    private static ReserveBudget Budget(int attacker, int defender, StagedSide player = StagedSide.Attacker) =>
        new(attacker, defender, Shares, player);

    [TestMethod]
    public void Allowance_AttackerA_Is20Percent()
    {
        Assert.AreEqual(20, Budget(100, 100).Allowance(StagedSide.Attacker, StageId.A));
    }

    [TestMethod]
    public void Allowance_IsCumulative()
    {
        var b = Budget(100, 100);

        Assert.AreEqual(50, b.Allowance(StagedSide.Attacker, StageId.B));
        Assert.AreEqual(75, b.Allowance(StagedSide.Attacker, StageId.C));
        Assert.AreEqual(100, b.Allowance(StagedSide.Attacker, StageId.D));
    }

    [TestMethod]
    public void Allowance_DefenderPlayerAttacks_LeavesTwentyPercentForTheHall()
    {
        Assert.AreEqual(80, Budget(100, 100, StagedSide.Attacker).Allowance(StagedSide.Defender, StageId.D));
    }

    [TestMethod]
    public void Allowance_DefenderPlayerDefends_WholeReserveAtD()
    {
        Assert.AreEqual(100, Budget(100, 100, StagedSide.Defender).Allowance(StagedSide.Defender, StageId.D));
    }

    [TestMethod]
    public void MayRelease_BelowAllowance_True()
    {
        // Reserve 100, 90 left: 10 spent, allowance at A is 20.
        Assert.IsTrue(Budget(100, 100).MayRelease(StagedSide.Attacker, StageId.A, 90, 50));
    }

    [TestMethod]
    public void MayRelease_AtAllowance_False()
    {
        Assert.IsFalse(Budget(100, 100).MayRelease(StagedSide.Attacker, StageId.A, 80, 50));
    }

    [TestMethod]
    public void MayRelease_ZeroActiveWithReserve_TrueOverBudget()
    {
        var b = Budget(100, 100);

        Assert.IsTrue(b.IsSoftlock(0, 5));
        Assert.IsTrue(b.MayRelease(StagedSide.Attacker, StageId.A, 5, 0));
    }

    [TestMethod]
    public void IsSoftlock_NobodyStandingWithReserve_True()
    {
        // Standing: active agents that are not running away. Fifty fleeing agents are active but none stands.
        var b = Budget(100, 100);

        Assert.IsTrue(b.IsSoftlock(standing: 0, remaining: 5));
        Assert.IsTrue(b.MayRelease(StagedSide.Attacker, StageId.A, remaining: 5, standing: 0));
    }

    [TestMethod]
    public void IsSoftlock_SomeoneStanding_False()
    {
        var b = Budget(100, 100);

        Assert.IsFalse(b.IsSoftlock(standing: 1, remaining: 5));
        Assert.IsFalse(b.MayRelease(StagedSide.Attacker, StageId.A, remaining: 80, standing: 1));
    }

    [TestMethod]
    public void MayRelease_ZeroActiveZeroReserve_False()
    {
        var b = Budget(100, 100);

        Assert.IsFalse(b.IsSoftlock(0, 0));
        Assert.IsFalse(b.MayRelease(StagedSide.Attacker, StageId.A, 0, 0));
    }

    [TestMethod]
    public void MayRelease_RemainingAboveStart_SpentIsZero()
    {
        Assert.IsTrue(Budget(100, 100).MayRelease(StagedSide.Attacker, StageId.A, 150, 50));
    }

    [TestMethod]
    public void New_NegativeReserve_ClampsToZero()
    {
        var b = Budget(-5, -9);

        Assert.AreEqual(0, b.Allowance(StagedSide.Attacker, StageId.D));
        Assert.AreEqual(0, b.Allowance(StagedSide.Defender, StageId.D));
    }

    [TestMethod]
    public void Allowance_IntMaxReserve_DoesNotOverflow()
    {
        var b = Budget(int.MaxValue, int.MaxValue, StagedSide.Defender);

        Assert.AreEqual(int.MaxValue, b.Allowance(StagedSide.Defender, StageId.D));
        Assert.IsTrue(b.Allowance(StagedSide.Attacker, StageId.A) > 0);
    }
}
