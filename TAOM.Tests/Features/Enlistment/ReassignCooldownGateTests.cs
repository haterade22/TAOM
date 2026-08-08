using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The swap cooldown used to sit in the reassignment line's VISIBILITY condition, so for seven
/// days after a swap the option simply was not there. That is the same failure the commander's
/// "you march with the {CURRENT_SECTION}" line was added to fix one level up: a player who sees
/// nothing concludes the feature is broken, not that they are waiting.
///
/// The gate now decides clickability instead, and it is written as a POSITIVE requirement so a
/// poisoned clock cannot unlock it. AssignmentService.CooldownRemaining hands back NaN for a NaN
/// "now" on purpose (its own comment calls NaN "still on cooldown, the strict outcome") — and
/// `remaining &lt;= 0` reads NaN as READY, re-opening exactly the swap the cooldown exists to stop.
/// </summary>
[TestClass]
public class ReassignCooldownGateTests
{
    [DataTestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    public void ReassignIsTakeable_CooldownSpent_AllowsTheSwap(double remaining)
    {
        Assert.IsTrue(EnlistmentAssignmentDialogBehavior.ReassignIsTakeable(remaining));
    }

    [DataTestMethod]
    [DataRow(0.01)]
    [DataRow(6.9)]
    public void ReassignIsTakeable_CooldownStillRunning_BlocksTheSwap(double remaining)
    {
        Assert.IsFalse(EnlistmentAssignmentDialogBehavior.ReassignIsTakeable(remaining));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void ReassignIsTakeable_NonFiniteRemainder_BlocksTheSwap(double remaining)
    {
        Assert.IsFalse(
            EnlistmentAssignmentDialogBehavior.ReassignIsTakeable(remaining),
            "A non-finite cooldown must fail the gate. Negative infinity is the trap: it is 'less than zero' and would read as ready.");
    }
}
