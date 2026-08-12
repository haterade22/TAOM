using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content.Domain;
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

    // ---- the rank carve-out (A2) ----
    //
    // Deliberately a SECOND decision rather than a fourth argument to ShouldStripPlayerCommand.
    // That one is the shared CONTEXT gate — BattleFormationPolicy's summary states the two
    // "cannot gate apart" — and formation PLACEMENT must keep happening at every rank. Only the
    // role assignment earns the carve-out.

    [TestMethod]
    public void Sergeant_WhomTheEngineMadeASergeant_KeepsHisFormation()
        // The reward for reaching the top of the ladder: one formation, the one he is standing in.
        => Assert.IsTrue(BattleCommandPolicy.ShouldKeepSergeantCommand(
            ServiceRank.Sergeant, engineAssignedSergeantRole: true));

    [TestMethod]
    public void Sergeant_WhomTheEngineMadeAGeneral_IsStillStripped()
        // THE case that makes this two conditions instead of one. The army merge is best-effort —
        // a kingdomless commander gets none — and without it Army stays null, IsPlayerSergeant() is
        // false, and AssignPlayerRoleInTeamMissionController falls back to making the player GENERAL
        // of the entire side. Gating on rank alone would hand a sergeant the whole army precisely
        // when the merge failed, which is the #424 bug wearing a promotion.
        => Assert.IsFalse(BattleCommandPolicy.ShouldKeepSergeantCommand(
            ServiceRank.Sergeant, engineAssignedSergeantRole: false));

    [TestMethod]
    public void Veteran_EvenAsAnEngineSergeant_IsStripped()
        // Below the top rank the player is a private in the line, whatever the engine offered.
        => Assert.IsFalse(BattleCommandPolicy.ShouldKeepSergeantCommand(
            ServiceRank.Veteran, engineAssignedSergeantRole: true));

    [TestMethod]
    public void Recruit_IsStripped()
        => Assert.IsFalse(BattleCommandPolicy.ShouldKeepSergeantCommand(
            ServiceRank.Recruit, engineAssignedSergeantRole: true));
}
