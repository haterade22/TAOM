using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class ServiceBattleServiceTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private EnlistmentStateMachine _machine = null!;
    private IEncounterAdapter _encounter = null!;
    private IMobilePartyAttachmentAdapter _partyAdapter = null!;
    private ServiceAttachmentService _attachment = null!;
    private IGameMenuAdapter _gameMenu = null!;
    private ServiceBattleService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _machine = new EnlistmentStateMachine(_store, _logger);
        _encounter = Substitute.For<IEncounterAdapter>();
        _partyAdapter = Substitute.For<IMobilePartyAttachmentAdapter>();
        _partyAdapter.RestorePresence().Returns(true);
        _partyAdapter.ParkNear(Arg.Any<string>()).Returns(true);
        _attachment = new ServiceAttachmentService(_partyAdapter, _logger);
        _gameMenu = Substitute.For<IGameMenuAdapter>();
        _service = new ServiceBattleService(_store, _machine, _encounter, _attachment, _gameMenu, _logger);

        _encounter.GetPartyBattleSide("lord_party_1").Returns(PartyBattleSide.Defender);
        _encounter.CanMainPartyJoinBattleOf("lord_party_1", PartyBattleSide.Defender).Returns(true);
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(true);
        _encounter.RestartBattle(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _encounter.Finish(Arg.Any<bool>()).Returns(true);
    }

    private void MakeEnlisted(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        _store.Record.State = state;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    [TestMethod]
    public void BattleStarted_NotAttachedState_NoOp()
    {
        MakeEnlisted(EnlistmentState.EnlistedDetachedOnDuty);

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        Assert.AreEqual(EnlistmentState.EnlistedDetachedOnDuty, _store.Record.State);
        _encounter.DidNotReceive().JoinBattle(Arg.Any<PartyBattleSide>());
    }

    [TestMethod]
    public void BattleStarted_TransitionsToBattleBeforeMenuWork()
    {
        // The ordering contract that keeps the menu guard from eating battle menus:
        // state flips to EnlistedBattle (redirect-exempt) BEFORE any encounter/menu push.
        MakeEnlisted();
        EnlistmentState stateAtJoin = default;
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(_ =>
        {
            stateAtJoin = _store.Record.State;
            return true;
        });

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        Assert.AreEqual(EnlistmentState.EnlistedBattle, stateAtJoin);
        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
    }

    [TestMethod]
    public void BattleStarted_RestoresPresenceJoinsCommanderSideAndOpensEncounterMenu()
    {
        MakeEnlisted();

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        _partyAdapter.Received(1).RestorePresence();
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
        _gameMenu.Received(1).SwitchTo("encounter");
    }

    [TestMethod]
    public void BattleStarted_NoCurrentEncounter_RestartsAgainstEventParties()
    {
        MakeEnlisted();
        _encounter.HasCurrent.Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        _encounter.Received(1).RestartBattle("defender_p", "attacker_p");
        _encounter.Received(1).JoinBattle(PartyBattleSide.Defender);
    }

    [TestMethod]
    public void BattleStarted_StaleForeignEncounter_FinishedFirst()
    {
        MakeEnlisted();
        _encounter.HasCurrent.Returns(true);
        _encounter.EncounteredPartyId.Returns("some_other_party");

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        _encounter.Received(1).Finish(false);
    }

    [TestMethod]
    public void BattleStarted_CommanderSideUnresolvable_NoOpStaysAttached()
    {
        MakeEnlisted();
        _encounter.GetPartyBattleSide("lord_party_1").Returns((PartyBattleSide?)null);

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().RestorePresence();
    }

    [TestMethod]
    public void BattleStarted_MainPartyCannotJoin_NoOpStaysAttached()
    {
        MakeEnlisted();
        _encounter.CanMainPartyJoinBattleOf("lord_party_1", PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
    }

    [TestMethod]
    public void BattleStarted_JoinFails_RolledBackToParkedAttached()
    {
        // The donor's wasHiddenServiceMode rollback, formalized: a failed join must never
        // strand a visible, active main party in battle state.
        MakeEnlisted();
        _encounter.JoinBattle(PartyBattleSide.Defender).Returns(false);

        _service.OnCommanderBattleStarted("lord_party_1", "attacker_p", "defender_p");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
        _gameMenu.DidNotReceive().SwitchTo("encounter");
    }

    [TestMethod]
    public void BattleEnded_NoEncounterLeft_ReturnsToAttachedAndParks()
    {
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(false);

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.Received(1).ParkNear("lord_1_1");
    }

    [TestMethod]
    public void BattleEnded_EncounterStillOpen_LeavesBattleStateForLootFlow()
    {
        // Loot/aftermath menus run inside the still-open encounter; flipping to Attached
        // here would let the menu guard eat them. The reconciler completes the return.
        MakeEnlisted(EnlistmentState.EnlistedBattle);
        _encounter.HasCurrent.Returns(true);

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedBattle, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }

    [TestMethod]
    public void BattleEnded_NotInBattleState_NoOp()
    {
        MakeEnlisted();

        _service.OnCommanderBattleEnded();

        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _partyAdapter.DidNotReceive().ParkNear(Arg.Any<string>());
    }
}
