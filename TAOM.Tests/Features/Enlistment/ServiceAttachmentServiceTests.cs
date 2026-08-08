using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class ServiceAttachmentServiceTests
{
    private IMobilePartyAttachmentAdapter _attachment = null!;
    private ServiceAttachmentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _service = new ServiceAttachmentService(_attachment, Substitute.For<IGameMenuAdapter>(), Substitute.For<IModLogger>());
    }

    private static CommanderSnapshot HealthyCommander(bool inMapEvent = false, bool inSettlement = false)
    {
        return new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: inMapEvent, partyIsInSettlement: inSettlement,
            cultureId: "gondor", name: "Lord Test");
    }

    private static PlayerPresenceSnapshot Player(
        bool parked = false, bool captive = false, bool inMapEvent = false)
    {
        return new PlayerPresenceSnapshot(
            mainPartyExists: true,
            isCaptive: captive,
            isActive: !parked,
            isVisible: !parked,
            isInMapEvent: inMapEvent);
    }

    [DataTestMethod]
    [DataRow(EnlistmentState.NotEnlisted)]
    [DataRow(EnlistmentState.PetitionPending)]
    [DataRow(EnlistmentState.Discharging)]
    [DataRow(EnlistmentState.EnlistedDetachedOnDuty)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.CommanderUnavailable)]
    public void Assess_NonAttachableStates_BlockedNotInAttachableState(EnlistmentState state)
    {
        var result = _service.Assess(state, HealthyCommander(), Player());

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.NotInAttachableState, result.BlockReason);
    }

    [TestMethod]
    public void Assess_PlayerCaptive_BlockedPlayerCaptive()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(captive: true));

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.PlayerCaptive, result.BlockReason);
    }

    [DataTestMethod]
    [DataRow(false, true, true)]   // missing hero
    [DataRow(true, false, true)]   // dead
    [DataRow(true, true, false)]   // party gone/inactive
    public void Assess_CommanderUnfit_BlockedCommanderPartyMissing(bool exists, bool alive, bool partyActive)
    {
        var commander = new CommanderSnapshot(
            exists: exists, isAlive: alive,
            partyId: partyActive ? "lord_party_1" : null, partyIsActive: partyActive);

        var result = _service.Assess(EnlistmentState.EnlistedAttached, commander, Player(parked: true));

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.CommanderPartyMissing, result.BlockReason);
    }

    [TestMethod]
    public void Assess_PrisonerCommanderWithLiveParty_BlockedCommanderPartyMissing()
    {
        // The engine usually nulls PartyBelongedTo on capture, but that correlation is not
        // contractual — a prisoner commander must be unfit even with an active party, so
        // the fitness criteria stay identical to IsCommanderFit/ReconcileGrace.
        var commander = new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true,
            partyId: "lord_party_1", partyIsActive: true);

        var result = _service.Assess(EnlistmentState.EnlistedAttached, commander, Player(parked: true));

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.CommanderPartyMissing, result.BlockReason);
    }

    [TestMethod]
    public void Assess_CommanderInBattlePlayerNot_BattleJoinRequired()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(inMapEvent: true), Player(parked: true));

        Assert.AreEqual(AttachmentStatus.BattleJoinRequired, result.Status);
    }

    [TestMethod]
    public void Assess_CommanderInBattlePlayerAlsoInEvent_Attached()
    {
        // Same-event verification is the battle service's job; the attachment layer only
        // says "nothing to do here".
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(inMapEvent: true), Player(inMapEvent: true));

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    [TestMethod]
    public void Assess_PlayerInForeignMapEvent_Blocked()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(inMapEvent: true));

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.PlayerInForeignMapEvent, result.BlockReason);
    }

    [TestMethod]
    public void Assess_ParkedHealthy_Attached()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(parked: true));

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    [TestMethod]
    public void Assess_NotParkedHealthy_AttachRequired()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(parked: false));

        Assert.AreEqual(AttachmentStatus.AttachRequired, result.Status);
    }

    [TestMethod]
    public void EnsureParked_DelegatesToAdapter()
    {
        _attachment.ParkNear("lord_1").Returns(true);

        Assert.IsTrue(_service.EnsureParked("lord_1"));
        _attachment.Received(1).ParkNear("lord_1");
    }

    [TestMethod]
    public void RestorePresence_DelegatesToAdapter()
    {
        _attachment.RestorePresence().Returns(true);

        Assert.IsTrue(_service.RestorePresence());
        _attachment.Received(1).RestorePresence();
    }
}
