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

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _attachment.RestorePresence().Returns(true);
        _service = new DischargeService(_store, _machine, _attachment, Substitute.For<IEncounterAdapter>(), new EncounterOwnershipPolicy(), Substitute.For<ICommanderLordAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
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
}
