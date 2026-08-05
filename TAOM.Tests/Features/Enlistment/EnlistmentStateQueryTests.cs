using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentStateQueryTests
{
    private EnlistmentStore _store = null!;
    private ICommanderLordAdapter _commander = null!;
    private EnlistmentStateQuery _query = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentStore(Substitute.For<IModLogger>());
        _commander = Substitute.For<ICommanderLordAdapter>();
        _query = new EnlistmentStateQuery(_store, _commander);
    }

    [TestMethod]
    public void NotEnlisted_DefaultsAreSafe()
    {
        Assert.IsFalse(_query.IsEnlisted);
        Assert.AreEqual(EnlistmentState.NotEnlisted, _query.State);
        Assert.IsNull(_query.CommanderHeroId);
        Assert.IsFalse(_query.IsCommanderParty("any_party"));
    }

    [TestMethod]
    public void Enlisted_ExposesRecordFields()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.ContractEndDay = 465.0;

        Assert.IsTrue(_query.IsEnlisted);
        Assert.AreEqual("lord_1_1", _query.CommanderHeroId);
        Assert.AreEqual("main_hero", _query.EnlistedHeroId);
        Assert.AreEqual(465.0, _query.ContractEndDay);
    }

    [TestMethod]
    public void IsCommanderParty_MatchesLiveCommanderPartyId()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true));

        Assert.IsTrue(_query.IsCommanderParty("lord_party_1"));
        Assert.IsFalse(_query.IsCommanderParty("other_party"));
    }

    [TestMethod]
    public void IsCommanderParty_NotEnlisted_NeverQueriesAdapter()
    {
        _query.IsCommanderParty("lord_party_1");

        _commander.DidNotReceive().GetSnapshot(Arg.Any<string>());
    }
}
