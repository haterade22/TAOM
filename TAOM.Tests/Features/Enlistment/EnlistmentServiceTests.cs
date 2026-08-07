using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private ICommanderLordAdapter _commander = null!;
    private IMobilePartyAttachmentAdapter _attachment = null!;
    private IEncounterAdapter _encounter = null!;
    private DischargeService _discharge = null!;
    private EnlistmentService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _attachment = Substitute.For<IMobilePartyAttachmentAdapter>();
        _attachment.RestorePresence().Returns(true);
        _attachment.ParkNear(Arg.Any<string>()).Returns(true);
        _discharge = new DischargeService(_store, _machine, _attachment, Substitute.For<IEncounterAdapter>(), Substitute.For<IGameMenuAdapter>(), _logger);
        _encounter = Substitute.For<IEncounterAdapter>();
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
        _service = new EnlistmentService(
            _store, _machine, _discharge, _commander, _attachment, _encounter,
            new EnlistmentConfigProvider(_logger), _logger);

        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true, name: "Lord Test"));
    }

    [TestMethod]
    public void SubmitPetition_FromNotEnlisted_AcceptedAndPending()
    {
        var result = _service.SubmitPetition("lord_1_1");

        Assert.AreEqual(PetitionResult.Accepted, result);
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
        Assert.AreEqual("lord_1_1", _store.Record.PetitionCommanderId);
    }

    [TestMethod]
    public void SubmitPetition_WhileEnlisted_Rejected()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_9_9";

        Assert.AreEqual(PetitionResult.AlreadyEnlisted, _service.SubmitPetition("lord_1_1"));
    }

    [TestMethod]
    public void SubmitPetition_ReplacesExistingPetition()
    {
        _commander.GetSnapshot("lord_2_2").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_2", partyIsActive: true));

        _service.SubmitPetition("lord_1_1");
        var result = _service.SubmitPetition("lord_2_2");

        Assert.AreEqual(PetitionResult.Accepted, result);
        Assert.AreEqual("lord_2_2", _store.Record.PetitionCommanderId);
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
    }

    [DataTestMethod]
    [DataRow(false, true, true)]   // missing
    [DataRow(true, false, true)]   // dead
    [DataRow(true, true, false)]   // no active party
    public void SubmitPetition_UnfitCommander_Rejected(bool exists, bool alive, bool hasParty)
    {
        _commander.GetSnapshot("lord_bad").Returns(new CommanderSnapshot(
            exists: exists, isAlive: alive,
            partyId: hasParty ? "p" : null, partyIsActive: hasParty));

        Assert.AreEqual(PetitionResult.CommanderUnavailable, _service.SubmitPetition("lord_bad"));
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void WithdrawPetition_ClearsPetitionAndReturnsToNotEnlisted()
    {
        _service.SubmitPetition("lord_1_1");

        Assert.IsTrue(_service.WithdrawPetition());
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        Assert.IsNull(_store.Record.PetitionCommanderId);
    }

    [TestMethod]
    public void CompleteOath_MatchingPetition_SwornParkedTimersSet()
    {
        _service.SubmitPetition("lord_1_1");

        var result = _service.CompleteOath("lord_1_1", "main_hero", nowDays: 200.0);

        Assert.AreEqual(OathResult.Sworn, result);
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        Assert.AreEqual("main_hero", _store.Record.EnlistedHeroId);
        Assert.AreEqual("lord_1_1", _store.Record.CommanderHeroId);
        Assert.IsNull(_store.Record.PetitionCommanderId);
        Assert.AreEqual(200.0, _store.Record.EnlistedAtDay);
        Assert.AreEqual(200.0 + 365.0, _store.Record.ContractEndDay);
        _attachment.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void CompleteOath_RaisesEnlistmentStarted()
    {
        _service.SubmitPetition("lord_1_1");
        string started = null;
        _service.EnlistmentStarted += id => started = id;

        _service.CompleteOath("lord_1_1", "main_hero", 200.0);

        Assert.AreEqual("lord_1_1", started);
    }

    [TestMethod]
    public void CompleteOath_NoPetition_Rejected()
    {
        Assert.AreEqual(OathResult.NoMatchingPetition, _service.CompleteOath("lord_1_1", "main_hero", 200.0));
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void CompleteOath_DifferentCommanderThanPetitioned_Rejected()
    {
        _service.SubmitPetition("lord_1_1");

        Assert.AreEqual(OathResult.NoMatchingPetition, _service.CompleteOath("lord_2_2", "main_hero", 200.0));
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
    }

    [TestMethod]
    public void CompleteOath_CommanderVanishedSincePetition_RejectedPetitionKept()
    {
        _service.SubmitPetition("lord_1_1");
        _commander.GetSnapshot("lord_1_1").Returns(CommanderSnapshot.Missing);

        Assert.AreEqual(OathResult.CommanderUnavailable, _service.CompleteOath("lord_1_1", "main_hero", 200.0));
        Assert.AreEqual(EnlistmentState.PetitionPending, _store.Record.State);
    }

    [TestMethod]
    public void CompleteOath_ParkFails_StillSworn()
    {
        // Parking can fail transiently (commander mid-transition); the hourly reconciler
        // re-parks. The oath itself must not be lost.
        _service.SubmitPetition("lord_1_1");
        _attachment.ParkNear(Arg.Any<string>()).Returns(false);

        var result = _service.CompleteOath("lord_1_1", "main_hero", 200.0);

        Assert.AreEqual(OathResult.Sworn, result);
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("ParkNear")));
    }

    [TestMethod]
    public void RequestDischarge_DelegatesToDischargePipeline()
    {
        _service.SubmitPetition("lord_1_1");
        _service.CompleteOath("lord_1_1", "main_hero", 200.0);

        Assert.IsTrue(_service.RequestDischarge(DischargeReason.PlayerRequest));
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _attachment.Received(1).RestorePresence();
    }

    [TestMethod]
    public void CompleteOath_ClosesTheConversationEncounterBeforeParking()
    {
        // Regression (in-game 2026-08-07): the oath is sworn inside a conversation, which runs
        // inside a PlayerEncounter. Parking without closing it left PlayerEncounter.Current live
        // for the whole term — observed on 93 of 93 ticks — and EncounterManager refuses EVERY
        // main-party encounter while it is set, so the player could not click a lord again.
        // The donor closes it at the same point (FinalizeEnlistmentConversation).
        _encounter.HasCurrent.Returns(true);
        _encounter.IsEncounterOwnedBy("lord_party_1").Returns(true);
        _service.SubmitPetition("lord_1_1");

        _service.CompleteOath("lord_1_1", "main_hero", 100.0);

        Received.InOrder(() =>
        {
            _encounter.Finish(false);
            _attachment.ParkNear("lord_1_1");
        });
    }

    [TestMethod]
    public void CompleteOath_NoEncounterOpen_DoesNotCallFinish()
    {
        _encounter.HasCurrent.Returns(false);
        _service.SubmitPetition("lord_1_1");

        _service.CompleteOath("lord_1_1", "main_hero", 100.0);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
    }

    [TestMethod]
    public void CompleteOath_EncounterIsNotTheCommanders_LeavesItAlone()
    {
        // Regression (donor diff, CRITICAL): finishing blind destroys encounters that are the
        // PLAYER'S own business — a settlement they are visiting, a fight they started, someone
        // else's conversation. Only the encounter with the commander is ours to close.
        _encounter.HasCurrent.Returns(true);
        _encounter.IsEncounterOwnedBy(Arg.Any<string>()).Returns(false);
        _service.SubmitPetition("lord_1_1");

        _service.CompleteOath("lord_1_1", "main_hero", 100.0);

        _encounter.DidNotReceive().Finish(Arg.Any<bool>());
        _attachment.Received(1).ParkNear("lord_1_1");
    }


}
