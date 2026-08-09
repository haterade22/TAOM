using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The #424 decision table. Command is stripped exactly when the battle was entered as
/// enlisted service AND the player does not lead the battle side — every other state keeps
/// vanilla roles, including detached-duty fights (the player's own business) and the
/// player somehow leading the side (never strip the actual leader's command).
/// </summary>
[TestClass]
public class BattleCommandPolicyTests
{
    [TestMethod]
    public void EnlistedBattle_NotLeadingSide_Strips()
        => Assert.IsTrue(BattleCommandPolicy.ShouldStripPlayerCommand(
            EnlistmentState.EnlistedBattle, playerLeadsBattleSide: false));

    [TestMethod]
    public void EnlistedBattle_LeadingSide_DoesNotStrip()
        => Assert.IsFalse(BattleCommandPolicy.ShouldStripPlayerCommand(
            EnlistmentState.EnlistedBattle, playerLeadsBattleSide: true));

    [TestMethod]
    public void DetachedDutyBattle_DoesNotStrip()
        => Assert.IsFalse(BattleCommandPolicy.ShouldStripPlayerCommand(
            EnlistmentState.EnlistedDetachedOnDuty, playerLeadsBattleSide: false));

    [TestMethod]
    public void NotEnlisted_DoesNotStrip()
        => Assert.IsFalse(BattleCommandPolicy.ShouldStripPlayerCommand(
            EnlistmentState.NotEnlisted, playerLeadsBattleSide: false));

    [TestMethod]
    public void EnlistedAttached_DoesNotStrip()
        => Assert.IsFalse(BattleCommandPolicy.ShouldStripPlayerCommand(
            EnlistmentState.EnlistedAttached, playerLeadsBattleSide: false));
}
