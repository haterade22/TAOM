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
        bool parked = false, bool captive = false, bool inMapEvent = false,
        string settlementId = null, bool hasEncounter = false)
    {
        return new PlayerPresenceSnapshot(
            mainPartyExists: true,
            isCaptive: captive,
            isActive: !parked,
            isVisible: !parked,
            settlementId: settlementId,
            isInMapEvent: inMapEvent,
            hasPlayerEncounter: hasEncounter);
    }

    private static CommanderSnapshot CommanderIn(string settlementId)
    {
        return new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true,
            partyIsInMapEvent: false, partyIsInSettlement: true,
            settlementId: settlementId,
            cultureId: "gondor", name: "Lord Test");
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
        var result = _service.Assess(state, HealthyCommander(), Player(), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.NotInAttachableState, result.BlockReason);
    }

    [TestMethod]
    public void Assess_PlayerCaptive_BlockedPlayerCaptive()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(captive: true), onTownLeave: false);

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

        var result = _service.Assess(EnlistmentState.EnlistedAttached, commander, Player(parked: true), onTownLeave: false);

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

        var result = _service.Assess(EnlistmentState.EnlistedAttached, commander, Player(parked: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.CommanderPartyMissing, result.BlockReason);
    }

    [TestMethod]
    public void Assess_CommanderInBattlePlayerNot_BattleJoinRequired()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(inMapEvent: true), Player(parked: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.BattleJoinRequired, result.Status);
    }

    [TestMethod]
    public void Assess_CommanderInBattlePlayerAlsoInEvent_Attached()
    {
        // Same-event verification is the battle service's job; the attachment layer only
        // says "nothing to do here".
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(inMapEvent: true), Player(inMapEvent: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    [TestMethod]
    public void Assess_PlayerInForeignMapEvent_Blocked()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(inMapEvent: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.PlayerInForeignMapEvent, result.BlockReason);
    }

    [TestMethod]
    public void Assess_ParkedHealthy_Attached()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(parked: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    [TestMethod]
    public void Assess_NotParkedHealthy_AttachRequired()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(parked: false), onTownLeave: false);

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

    // ---- Shore leave suspends the exit sweep (#512) ------------------------------------------
    // Before this, OnTownLeave appeared nowhere in the attachment layer, so the hourly reconciler
    // dragged the player out of the town the moment the commander walked out of it. A pass whose
    // lifetime is "until the lord leaves" is worth about two real seconds at wait-menu speed.

    [TestMethod]
    public void Assess_OnLeaveInsideSettlement_CommanderGone_StaysPut()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached,
            HealthyCommander(),                                   // commander is NOT in a settlement
            Player(settlementId: "town_EW1"),
            onTownLeave: true);

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    [TestMethod]
    public void Assess_NoLeaveInsideSettlement_CommanderGone_ExitsAsBefore()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached,
            HealthyCommander(),
            Player(settlementId: "town_EW1"),
            onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.SettlementExitRequired, result.Status);
    }

    [TestMethod]
    public void Assess_OnLeaveInADifferentSettlementFromTheCommander_StaysPut()
    {
        // The pass is about staying where the player chose to stay, not about matching him.
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached,
            CommanderIn("town_EW2"),
            Player(settlementId: "town_EW1"),
            onTownLeave: true);

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }

    /// <summary>
    /// ORDERING GUARD 1. A dead, captured or party-less commander outranks a pass: that check sits
    /// above the settlement rules and must stay there. If a future edit moves the leave branch up,
    /// a player on leave would never be released when his commander died.
    /// </summary>
    [TestMethod]
    public void Assess_OnLeaveButCommanderDead_StillBlocked()
    {
        var dead = new CommanderSnapshot(exists: true, isAlive: false);

        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, dead,
            Player(settlementId: "town_EW1"),
            onTownLeave: true);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.CommanderPartyMissing, result.BlockReason);
    }

    /// <summary>
    /// ORDERING GUARD 2. A soldier on a pass is still enlisted. If the commander starts a battle
    /// the join must still be raised; the leave branch must not swallow it.
    /// </summary>
    [TestMethod]
    public void Assess_OnLeaveButCommanderInBattle_StillRaisesTheJoin()
    {
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached,
            HealthyCommander(inMapEvent: true),
            Player(settlementId: "town_EW1"),
            onTownLeave: true);

        Assert.AreEqual(AttachmentStatus.BattleJoinRequired, result.Status);
    }

    [TestMethod]
    public void Assess_OnLeaveButOutsideAnySettlement_BehavesNormally()
    {
        // A pass held with the player NOT inside anything is a stale flag, not a licence to idle.
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, HealthyCommander(), Player(parked: false),
            onTownLeave: true);

        Assert.AreEqual(AttachmentStatus.AttachRequired, result.Status);
    }

    // ---- The presence hold while a battle encounter is live (#577) ----------------------------
    //
    // A live PlayerEncounter in EnlistedBattle is the battle itself, its loot and aftermath window
    // (MapEventSide.Clear() nulls MainParty.MapEvent BEFORE the encounter closes, so the window
    // reads as "no battle anywhere"), or the join gap between EnsureEncounterAgainst and
    // JoinBattle. Nothing may move the party there: a park clears AttachedTo, which pulls the
    // party off its MapEventSide, and "Send troops" then indexes SelectedTroops[-1].

    [TestMethod]
    public void Assess_EnlistedBattle_LiveEncounter_HoldsPresence()
    {
        // The loot-window shape: active and visible (RestorePresence ran), in no map event, the
        // commander in none either, the encounter still open. This used to be AttachRequired.
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(inMapEvent: false),
            Player(parked: false, hasEncounter: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.BattleEncounterOpen, result.BlockReason);
    }

    [TestMethod]
    public void Assess_EnlistedBattle_LiveEncounter_OutranksSettlementExit()
    {
        // The exit rule sits above the battle branch for the siege-assault reason; it must not sit
        // above the hold, or an exit-and-park runs on a party that is mid-encounter.
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(),
            Player(parked: false, hasEncounter: true, settlementId: "town_A"), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.BattleEncounterOpen, result.BlockReason);
    }

    [TestMethod]
    public void Assess_EnlistedBattle_LiveEncounter_OutranksCommanderLoss()
    {
        // A commander killed in this very battle: the discharge waits for the encounter to close
        // instead of running from inside PlayerEncounter.Finish.
        var dead = new CommanderSnapshot(exists: true, isAlive: false);

        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, dead,
            Player(parked: false, hasEncounter: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.BattleEncounterOpen, result.BlockReason);
    }

    [TestMethod]
    public void Assess_EnlistedBattle_LiveEncounter_DoesNotOutrankCaptivity()
    {
        // Captivity is the one thing that outranks the hold: vanilla owns a captive's party.
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(),
            Player(captive: true, hasEncounter: true), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Blocked, result.Status);
        Assert.AreEqual(AttachmentBlockReason.PlayerCaptive, result.BlockReason);
    }

    [TestMethod]
    public void Assess_EnlistedBattle_NoEncounter_NotParked_StillAttachRequired()
    {
        // Parity: once the encounter is gone the old verdict stands. This is the shape the #551
        // stale-latch break leaves behind, and the reconciler still has to re-park it.
        var result = _service.Assess(
            EnlistmentState.EnlistedBattle, HealthyCommander(),
            Player(parked: false, hasEncounter: false), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.AttachRequired, result.Status);
    }

    [TestMethod]
    public void Assess_EnlistedAttached_LiveEncounter_Unchanged()
    {
        // The hold is keyed on EnlistedBattle, not on the encounter alone: since #510 every
        // settlement placement opens an encounter deliberately, so an open encounter while
        // Attached is ordinary and keeps its verdict.
        var result = _service.Assess(
            EnlistmentState.EnlistedAttached, CommanderIn("town_A"),
            Player(parked: false, hasEncounter: true, settlementId: "town_A"), onTownLeave: false);

        Assert.AreEqual(AttachmentStatus.Attached, result.Status);
    }
}
