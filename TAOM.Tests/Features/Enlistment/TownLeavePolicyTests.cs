using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Field report 1: "you can't enter towns when your lord does, which causes you not to be able to
/// buy anything". The player was already INSIDE the settlement — the settlement menu was simply
/// being redirected away. These pin when the pass is offered and, more importantly, every way it
/// ends.
/// </summary>
[TestClass]
public class TownLeavePolicyTests
{
    [TestMethod]
    public void AttachedInSettlement_MayTakeLeave()
        => Assert.IsTrue(TownLeavePolicy.CanTakeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: true, alreadyOnLeave: false));

    [TestMethod]
    public void OnOpenGround_NoLeaveToTake()
        => Assert.IsFalse(TownLeavePolicy.CanTakeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: false, alreadyOnLeave: false));

    [TestMethod]
    public void AlreadyOnLeave_NotOfferedTwice()
        => Assert.IsFalse(TownLeavePolicy.CanTakeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: true, alreadyOnLeave: true));

    [TestMethod]
    public void DuringABattle_NoShopping()
        => Assert.IsFalse(TownLeavePolicy.CanTakeLeave(
            EnlistmentState.EnlistedBattle, insideSettlement: true, alreadyOnLeave: false));

    [TestMethod]
    public void NotEnlisted_PolicyDoesNotApply()
        // A discharged player owns the town outright; the pass must not be the thing granting it.
        => Assert.IsFalse(TownLeavePolicy.CanTakeLeave(
            EnlistmentState.NotEnlisted, insideSettlement: true, alreadyOnLeave: false));

    // ---- revocation: every exit, not just the obvious one ----

    [TestMethod]
    public void ColumnMarchesOut_PassIsRevoked()
        // THE case. A soldier does not stay behind shopping while his company leaves.
        => Assert.IsTrue(TownLeavePolicy.ShouldRevokeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: false, onLeave: true));

    [TestMethod]
    public void BattleStarts_PassIsRevoked()
        => Assert.IsTrue(TownLeavePolicy.ShouldRevokeLeave(
            EnlistmentState.EnlistedBattle, insideSettlement: true, onLeave: true));

    [TestMethod]
    public void Discharged_PassIsRevoked()
        // Not because the player loses town access — he gains it — but because a stale flag would
        // survive into the next enlistment and silently suspend the redirect from the first day.
        => Assert.IsTrue(TownLeavePolicy.ShouldRevokeLeave(
            EnlistmentState.NotEnlisted, insideSettlement: true, onLeave: true));

    [TestMethod]
    public void StillRestingInTown_PassStands()
        => Assert.IsFalse(TownLeavePolicy.ShouldRevokeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: true, onLeave: true));

    [TestMethod]
    public void NoPassHeld_NothingToRevoke()
        => Assert.IsFalse(TownLeavePolicy.ShouldRevokeLeave(
            EnlistmentState.EnlistedAttached, insideSettlement: false, onLeave: false));
}
