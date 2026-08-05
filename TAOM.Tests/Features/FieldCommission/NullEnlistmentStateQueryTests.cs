using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.FieldCommission;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class NullEnlistmentStateQueryTests
{
    private readonly NullEnlistmentStateQuery _sut = new NullEnlistmentStateQuery();

    [TestMethod]
    public void IsEnlisted_AlwaysFalse()
    {
        Assert.IsFalse(_sut.IsEnlisted);
    }

    [TestMethod]
    public void State_AlwaysNotEnlisted()
    {
        Assert.AreEqual(EnlistmentState.NotEnlisted, _sut.State);
    }

    [TestMethod]
    public void CommanderHeroId_AlwaysNull()
    {
        Assert.IsNull(_sut.CommanderHeroId);
    }

    [TestMethod]
    public void ContractEndDay_AlwaysNull()
    {
        Assert.IsNull(_sut.ContractEndDay);
    }

    [TestMethod]
    public void IsCommanderParty_AnyPartyId_AlwaysFalse()
    {
        Assert.IsFalse(_sut.IsCommanderParty("party_1"));
        Assert.IsFalse(_sut.IsCommanderParty(null));
    }
}
