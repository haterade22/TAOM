using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// DischargePipelineInvariantTests — the commission-softlock regression suite. The donor
/// mod's commission exit cleared the service record without restoring party presence,
/// leaving the main party permanently hidden. Here EVERY discharge reason must restore
/// presence BEFORE the record is cleared, unconditionally.
/// </summary>
[TestClass]
public class DischargeServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private IMobilePartyAttachmentAdapter _attachment = null!;
    private DischargeService _service = null!;
    private IEncounterAdapter _encounter = null!;
    private ICommanderLordAdapter _commanderAdapter = null!;
    private IGameMenuAdapter _gameMenu = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _attachment.RestorePresence().Returns(true);
        _encounter = Substitute.For<IEncounterAdapter>();
        _commanderAdapter = Substitute.For<ICommanderLordAdapter>();
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _gameMenu.ExitToLast().Returns(true);
        _gameMenu.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _attachment.MoveIntoSettlement(Arg.Any<string>()).Returns(true);
        _service = new DischargeService(_store, _machine, _attachment, _encounter,
            new EncounterOwnershipPolicy(), _commanderAdapter, _gameMenu, _logger);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
        _store.Record.ContractEndDay = 465.0;
    }

    [TestMethod]
    public void Execute_EveryReason_RestoresPresenceAndClearsRecord()
    {
        foreach (DischargeReason reason in Enum.GetValues(typeof(DischargeReason)))
        {
            Setup(); // fresh mocks per reason
            MakeEnlisted();

            var result = _service.Execute(reason);

            Assert.IsTrue(result, $"Execute({reason}) should succeed");
            _attachment.Received(1).RestorePresence();
            Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State, $"({reason}) end state");
            Assert.IsNull(_store.Record.CommanderHeroId, $"({reason}) record cleared");
        }
    }

    [TestMethod]
    public void Execute_RestoresPresenceBeforeRecordClear()
    {
        // The softlock invariant proper: at the moment presence is restored, the record
        // must still identify the service (restore first, clear after).
        MakeEnlisted();
        string commanderAtRestoreTime = null;
        _attachment.RestorePresence().Returns(_ =>
        {
            commanderAtRestoreTime = _store.Record.CommanderHeroId;
            return true;
        });

        _service.Execute(DischargeReason.Commission);

        Assert.AreEqual("lord_1_1", commanderAtRestoreTime,
            "RestorePresence must run BEFORE the record is cleared");
    }

    [TestMethod]
    public void Execute_FromEveryEnlistedFamilyState_Succeeds()
    {
        foreach (var state in new[]
        {
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedBattle,
            EnlistmentState.EnlistedDetachedOnDuty,
            EnlistmentState.EnlistedPlayerCaptive,
            EnlistmentState.CommanderUnavailable,
        })
        {
            Setup();
            MakeEnlisted(state);

            Assert.IsTrue(_service.Execute(DischargeReason.PlayerRequest), $"from {state}");
            Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State, $"from {state}");
        }
    }

    [TestMethod]
    public void Execute_RestorePresenceFails_PipelineStillCompletes()
    {
        // A dead MainParty reference must not leave the record half-discharged; the
        // failure is logged, the pipeline finishes.
        MakeEnlisted();
        _attachment.RestorePresence().Returns(false);

        var result = _service.Execute(DischargeReason.CommanderDead);

        Assert.IsTrue(result);
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("RestorePresence")));
    }

    [TestMethod]
    public void Execute_RaisesEnlistmentEndedWithReasonAfterClear()
    {
        MakeEnlisted();
        DischargeReason? observed = null;
        EnlistmentState? stateAtEvent = null;
        _service.EnlistmentEnded += reason =>
        {
            observed = reason;
            stateAtEvent = _store.Record.State;
        };

        _service.Execute(DischargeReason.Desertion);

        Assert.AreEqual(DischargeReason.Desertion, observed);
        Assert.AreEqual(EnlistmentState.NotEnlisted, stateAtEvent,
            "consequence subscribers must observe the completed discharge");
    }

    [TestMethod]
    public void Execute_NotEnlisted_ReturnsFalseNoRestoreNoEvent()
    {
        var raised = false;
        _service.EnlistmentEnded += _ => raised = true;

        var result = _service.Execute(DischargeReason.PlayerRequest);

        Assert.IsFalse(result);
        Assert.IsFalse(raised);
        _attachment.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void Execute_PetitionPending_ReturnsFalse()
    {
        _store.Record.State = EnlistmentState.PetitionPending;
        _store.Record.PetitionCommanderId = "lord_1_1";

        Assert.IsFalse(_service.Execute(DischargeReason.PlayerRequest));
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
    }

    [TestMethod]
    public void Execute_ConsequenceSubscriberThrows_RecordStillCleared()
    {
        // A faulty consequence listener must never re-softlock the exit.
        MakeEnlisted();
        _service.EnlistmentEnded += _ => throw new InvalidOperationException("bad subscriber");

        var result = _service.Execute(DischargeReason.PlayerRequest);

        Assert.IsTrue(result);
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("EnlistmentEnded")));
    }

    // ---- INV-D1: the hand-back invariant, for EVERY reason ------------------------------

    [TestMethod]
    public void Execute_EveryReason_LeavesTheServiceWaitMenu()
    {
        // THE pin for this batch. Only the menu-button path used to call ExitToLast, so any
        // AUTOMATIC discharge — commander dies, grace expires, contract ends, identity mismatch —
        // left the player in a menu whose options were all condition-gated to false, with no
        // Escape. The donor names the symptom in its own source: frozen in the wait menu with an
        // orphaned camera.
        foreach (DischargeReason reason in System.Enum.GetValues(typeof(DischargeReason)))
        {
            Setup();
            MakeEnlisted();
            _gameMenu.CurrentMenuId.Returns(EnlistmentMenuService.ServiceWaitMenuId);

            Assert.IsTrue(_service.Execute(reason), $"{reason} should discharge");
            _gameMenu.Received(1).ExitToLast();
        }
    }

    [TestMethod]
    public void Execute_EveryReason_ClearsArmyAttachment()
    {
        foreach (DischargeReason reason in System.Enum.GetValues(typeof(DischargeReason)))
        {
            Setup();
            MakeEnlisted();

            _service.Execute(reason);
            _attachment.Received(1).ClearArmyAttachment();
        }
    }

    [TestMethod]
    public void Execute_NotOnTheWaitMenu_DoesNotCallExitToLast()
    {
        // GameMenu.ExitToLast sets TimeControlMode = Stop UNCONDITIONALLY before delegating to a
        // null-guarded manager, so an ungated call freezes campaign time with nothing on screen.
        MakeEnlisted();
        _gameMenu.CurrentMenuId.Returns("town");

        _service.Execute(DischargeReason.PlayerRequest);

        _gameMenu.DidNotReceive().ExitToLast();
    }

    [TestMethod]
    public void Execute_CommanderInSettlement_ReleasesThePlayerIntoIt()
    {
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            settlementId: "town_A1", settlementMenuId: "town"));

        _service.Execute(DischargeReason.PlayerRequest);

        _attachment.Received(1).MoveIntoSettlement("town_A1");
        _gameMenu.Received(1).EnsureMenuOpen("town");
    }

    [TestMethod]
    public void Execute_SettlementMenuFailsToOpen_LeavesSettlementRatherThanStrandingThePlayer()
    {
        // A player inside a settlement with no settlement menu is the same soft-lock one layer
        // deeper, so the placement must roll itself back.
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            settlementId: "town_A1", settlementMenuId: "town"));
        _attachment.MoveIntoSettlement(Arg.Any<string>()).Returns(true);
        _gameMenu.EnsureMenuOpen("town").Returns(false);
        // The move succeeded, so the player really is inside — that is the precondition for
        // backing them out again.
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true, settlementId: "town_A1"));

        _service.Execute(DischargeReason.PlayerRequest);

        _attachment.Received(1).LeaveSettlement();
    }

    [TestMethod]
    public void Execute_LoadNormalizationReason_NeverMovesThePlayer()
    {
        // The SAVED position is authoritative on a load path; moving them would teleport the
        // player on every load.
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            settlementId: "town_A1", settlementMenuId: "town"));

        _service.Execute(DischargeReason.HeirSuccessionOrPossessionMismatch);

        _attachment.DidNotReceive().MoveIntoSettlement(Arg.Any<string>());
    }

    [TestMethod]
    public void Execute_RaisesEnlistmentEnded_BeforePlacingThePlayer()
    {
        // The content layer cancels the active duty in the subscriber, and placement dispatches
        // OnSettlementEntered, which the duty runtime treats as a COMPLETION trigger. Reversed,
        // a cancelled duty would complete.
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true,
            settlementId: "town_A1", settlementMenuId: "town"));
        var subscriberRanBeforePlacement = false;
        _service.EnlistmentEnded += _ =>
            subscriberRanBeforePlacement = _attachment.ReceivedCalls()
                .All(c => c.GetMethodInfo().Name != nameof(IMobilePartyAttachmentAdapter.MoveIntoSettlement));

        _service.Execute(DischargeReason.PlayerRequest);

        Assert.IsTrue(subscriberRanBeforePlacement);
    }

    // ---- Always-on discharge diagnostics: NEVER gated by the [EnlistDiag] volume toggle -----
    // These two lines were entirely unpinned before the toggle work. They must stay reachable
    // with EnableEnlistmentDiagnostics OFF, because they are the only signal that a discharge
    // broke the save. DischargeService takes no IEnlistmentDiagnosticsSettingsProvider at all,
    // which is what makes that structural rather than a matter of discipline — if someone ever
    // adds one, these tests are the tripwire.

    [TestMethod]
    public void Execute_LeavesEncountersBlocked_LogsUnableToStartEncountersError()
    {
        // A live PlayerEncounter that survives the pipeline is the save-breaker: EncounterManager
        // then refuses every main-party encounter, so the player can never click a lord again.
        // Deliberately NOT the settlement shape — that one is benign and logs a warning instead.
        MakeEnlisted();
        _attachment.GetPresence(Arg.Any<string>()).Returns(new PlayerPresenceSnapshot(
            mainPartyExists: true, isActive: true, isVisible: true,
            hasPlayerEncounter: true, settlementId: null));

        _service.Execute(DischargeReason.PlayerRequest);

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("LEFT THE PLAYER UNABLE TO START ENCOUNTERS")));
    }

    [TestMethod]
    public void Execute_LiveEncounter_FinishesItAndRestoresPresence()
    {
        // Discharge outranks ownership: the encounter is closed AND presence is restored, both
        // unconditionally. Presence restoration is the softlock invariant; the finish is the
        // can't-interact-again invariant. One discharge must satisfy both.
        MakeEnlisted();
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(
            hasEncounter: true,
            conversationInProgress: false,
            hasEncounteredMobileParty: true,
            encounteredPartyId: "lord_party_1",
            encounteredPartyIsCommanderRelated: true,
            playerInMapEvent: false));
        _encounter.Finish(true).Returns(true);

        Assert.IsTrue(_service.Execute(DischargeReason.PlayerRequest));

        _encounter.Received(1).Finish(true);
        _attachment.Received(1).RestorePresence();
    }

    [TestMethod]
    public void Execute_PlayerInsideASettlementTheCommanderIsNotIn_StillLeavesIt()
    {
        // F1, the terminal soft-lock. Placement is decided from where the COMMANDER is, so when
        // he has no settlement (dead, in the field, in a hideout) the whole settlement branch was
        // skipped — and the wait menu was then closed, leaving the player with CurrentSettlement
        // set and no menu. The engine will not move a party in that state, will not auto-exit the
        // main party, and the menu it re-pushes for a fortification has a Leave that no-ops
        // without an encounter. It survives save/reload.
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: false));
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(
            mainPartyExists: true, isActive: true, isVisible: true, settlementId: "town_A1"));

        _service.Execute(DischargeReason.CommanderDead);

        _attachment.Received(1).LeaveSettlement();
    }

    [TestMethod]
    public void Execute_PlayerNotInAnySettlement_DoesNotCallLeaveSettlement()
    {
        MakeEnlisted();
        _commanderAdapter.GetSnapshot(Arg.Any<string>()).Returns(new CommanderSnapshot(
            exists: true, isAlive: false));
        _attachment.GetPresenceFlags().Returns(new PlayerPresenceFlags(mainPartyExists: true));

        _service.Execute(DischargeReason.CommanderDead);

        _attachment.DidNotReceive().LeaveSettlement();
    }
}
