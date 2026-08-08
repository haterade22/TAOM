using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The live status board. The value equality IS the refresh throttle, so a field missing from
/// Equals is a field whose changes never reach the screen — that is what the per-field DataRows
/// below exist to catch.
/// </summary>
[TestClass]
public class ServiceStatusServiceTests
{
    private EnlistmentStore _store;
    private EnlistmentContentStore _content;
    private ICommanderLordAdapter _commander;
    private IServiceStatusTextWriter _writer;
    private ServiceStatusService _sut;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _content = new EnlistmentContentStore(logger);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _writer = Substitute.For<IServiceStatusTextWriter>();

        Commander();
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";

        _sut = new ServiceStatusService(_store, _content, _commander, _writer);
    }

    private void Commander(
        bool inMapEvent = false, bool besieging = false,
        string settlementId = null, string settlementName = null, bool alive = true)
    {
        _commander.GetSnapshot("lord_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: alive, partyId: "lord_party", partyIsActive: true,
            partyIsInMapEvent: inMapEvent, partyIsBesieging: besieging,
            partyIsInSettlement: !string.IsNullOrEmpty(settlementId),
            settlementId: settlementId, settlementName: settlementName,
            name: "Boromir"));
    }

    // ---- Build ------------------------------------------------------------------------------

    [TestMethod]
    public void Build_NotEnlisted_Null()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;
        Assert.IsNull(_sut.Build());
    }

    [TestMethod]
    public void Build_Marching_CarriesCommanderAndProgress()
    {
        _content.Record.Rank = ServiceRank.Veteran;
        _content.Record.DaysServed = 12;
        _content.Record.Trust = 9;

        var status = _sut.Build();

        Assert.AreEqual(CommanderActivity.Marching, status.Activity);
        Assert.AreEqual("Boromir", status.CommanderName);
        Assert.AreEqual(ServiceRank.Veteran, status.Rank);
        Assert.AreEqual(12, status.DaysServed);
        Assert.AreEqual(9, status.Trust);
    }

    [TestMethod]
    public void Build_InSettlement_CarriesTheSettlementName()
    {
        // The proof line for this batch: the board must be able to say WHERE, by name.
        Commander(settlementId: "town_ES1", settlementName: "Minas Tirith");

        var status = _sut.Build();

        Assert.AreEqual(CommanderActivity.InSettlement, status.Activity);
        Assert.AreEqual("Minas Tirith", status.SettlementName);
    }

    [TestMethod]
    public void Build_BattleOutranksSiegeAndSettlement()
    {
        // A commander assaulting a town is in all three states at once; "in battle" is the one
        // the player needs.
        Commander(inMapEvent: true, besieging: true, settlementId: "town_ES1", settlementName: "Minas Tirith");
        Assert.AreEqual(CommanderActivity.InBattle, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_SiegeOutranksSettlement()
    {
        Commander(besieging: true, settlementId: "town_ES1", settlementName: "Minas Tirith");
        Assert.AreEqual(CommanderActivity.Besieging, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_DeadCommander_Unavailable()
    {
        Commander(alive: false);
        Assert.AreEqual(CommanderActivity.Unavailable, _sut.Build().Activity);
    }

    [TestMethod]
    public void Build_NullSnapshot_DoesNotThrow()
    {
        _commander.GetSnapshot("lord_1").Returns((CommanderSnapshot)null);
        Assert.AreEqual(CommanderActivity.Unavailable, _sut.Build().Activity);
    }

    // ---- the throttle -----------------------------------------------------------------------

    [TestMethod]
    public void RefreshIfChanged_FirstCall_Writes()
    {
        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_NothingChanged_DoesNotWriteAgain()
    {
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();

        Assert.IsFalse(_sut.RefreshIfChanged());
        _writer.DidNotReceiveWithAnyArgs().Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_ColumnEntersASettlement_WritesAgain()
    {
        // The felt change: the text must move when the column stops somewhere.
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();
        Commander(settlementId: "town_ES1", settlementName: "Minas Tirith");

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }

    [TestMethod]
    public void RefreshIfChanged_NotEnlisted_DoesNothing()
    {
        _store.Record.State = EnlistmentState.NotEnlisted;

        Assert.IsFalse(_sut.RefreshIfChanged());
        _writer.DidNotReceiveWithAnyArgs().Write(default);
    }

    [TestMethod]
    public void Invalidate_ForcesTheNextRefreshToWrite()
    {
        // Menu init relies on this: the cached model is almost always identical to what is on
        // screen, so without invalidation the init refresh would no-op and leave the previous
        // menu's text in place.
        _sut.RefreshIfChanged();
        _writer.ClearReceivedCalls();

        _sut.Invalidate();

        Assert.IsTrue(_sut.RefreshIfChanged());
        _writer.ReceivedWithAnyArgs(1).Write(default);
    }
}

/// <summary>
/// One DataRow per field. The model's Equals is the refresh gate, so an omitted field is a
/// permanently invisible status line — a silent failure with no error anywhere.
/// </summary>
[TestClass]
public class ServiceStatusModelEqualityTests
{
    private static ServiceStatusModel Base() => new ServiceStatusModel(
        commanderName: "Boromir", activity: CommanderActivity.Marching, settlementName: null,
        rank: ServiceRank.Soldier, assignment: ServiceAssignment.Infantry,
        daysServed: 5, trust: 3, deferredWages: 0, activeDutyId: null);

    [TestMethod]
    public void Equals_IdenticalModels_True() => Assert.IsTrue(Base().Equals(Base()));

    [TestMethod]
    public void Equals_Null_False() => Assert.IsFalse(Base().Equals(null));

    [DataTestMethod]
    [DataRow("commanderName")]
    [DataRow("activity")]
    [DataRow("settlementName")]
    [DataRow("rank")]
    [DataRow("assignment")]
    [DataRow("daysServed")]
    [DataRow("trust")]
    [DataRow("deferredWages")]
    [DataRow("activeDutyId")]
    public void Equals_IsFalse_WhenAnySingleFieldDiffers(string field)
    {
        var other = new ServiceStatusModel(
            commanderName: field == "commanderName" ? "Faramir" : "Boromir",
            activity: field == "activity" ? CommanderActivity.InBattle : CommanderActivity.Marching,
            settlementName: field == "settlementName" ? "Minas Tirith" : null,
            rank: field == "rank" ? ServiceRank.Veteran : ServiceRank.Soldier,
            assignment: field == "assignment" ? ServiceAssignment.Cavalry : ServiceAssignment.Infantry,
            daysServed: field == "daysServed" ? 6 : 5,
            trust: field == "trust" ? 4 : 3,
            deferredWages: field == "deferredWages" ? 50 : 0,
            activeDutyId: field == "activeDutyId" ? "recon_sweep" : null);

        Assert.IsFalse(Base().Equals(other), $"'{field}' is missing from Equals — its changes would never reach the screen");
    }
}
