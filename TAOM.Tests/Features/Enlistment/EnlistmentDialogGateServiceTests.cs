using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentDialogGateServiceTests
{
    private EnlistmentStore _store = null!;
    private ICommanderLordAdapter _commander = null!;
    private IPlayerContextAdapter _playerContext = null!;
    private IEnlistmentConfigProvider _config = null!;
    private EnlistmentDialogGateService _gate = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentStore(Substitute.For<IModLogger>());
        _commander = Substitute.For<ICommanderLordAdapter>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();
        _config = Substitute.For<IEnlistmentConfigProvider>();
        _config.GetConfig().Returns(new EnlistmentCoreConfig());
        _gate = new EnlistmentDialogGateService(_store, _commander, _playerContext, _config);

        _commander.IsLord("lord_1_1").Returns(true);
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true, name: "Lord Test"));
        _playerContext.IsUnderMercenaryService().Returns(false);
        _playerContext.GetPlayerKingdomId().Returns((string)null);
    }

    [TestMethod]
    public void CanEnlist_HealthyLordNoConflicts_Ok()
    {
        Assert.AreEqual(EnlistGateResult.Ok, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_AlreadyEnlisted_Rejected()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_9_9";

        Assert.AreEqual(EnlistGateResult.AlreadyEnlisted, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_NotALord_Rejected()
    {
        _commander.IsLord("townsman_1").Returns(false);

        Assert.AreEqual(EnlistGateResult.NotALord, _gate.CanEnlistWith("townsman_1"));
    }

    [TestMethod]
    public void CanEnlist_UnderMercenaryContract_Rejected()
    {
        _playerContext.IsUnderMercenaryService().Returns(true);

        Assert.AreEqual(EnlistGateResult.UnderMercenaryContract, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_CommanderUnfit_Rejected()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: true, partyId: null));

        Assert.AreEqual(EnlistGateResult.CommanderUnavailable, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_CommanderAtWarWithPlayerKingdom_Rejected()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(true);

        Assert.AreEqual(EnlistGateResult.AtWarWithYourKingdom, _gate.CanEnlistWith("lord_1_1"));
    }

    [TestMethod]
    public void CanEnlist_PlayerKingdomAtPeaceWithCommander_Ok()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(false);

        Assert.AreEqual(EnlistGateResult.Ok, _gate.CanEnlistWith("lord_1_1"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void CanEnlist_NullOrEmptyPartner_Rejected(string partnerId)
    {
        Assert.AreEqual(EnlistGateResult.NotALord, _gate.CanEnlistWith(partnerId));
    }

    [TestMethod]
    public void CanRequestDischarge_EnlistedWithThisCommander_True()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        Assert.IsTrue(_gate.CanRequestDischargeFrom("lord_1_1"));
    }

    [TestMethod]
    public void CanRequestDischarge_DifferentLord_False()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";

        Assert.IsFalse(_gate.CanRequestDischargeFrom("lord_2_2"));
    }

    [TestMethod]
    public void CanRequestDischarge_NotEnlisted_False()
    {
        Assert.IsFalse(_gate.CanRequestDischargeFrom("lord_1_1"));
    }
}
