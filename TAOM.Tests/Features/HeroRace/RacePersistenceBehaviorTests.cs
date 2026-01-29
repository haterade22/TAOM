using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HeroRace;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class RacePersistenceBehaviorTests
{
    private RacePersistenceBehavior _sut;
    private IRacePersistenceService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IRacePersistenceService>();
        _sut = new RacePersistenceBehavior(_service);
    }

    [TestMethod]
    public void SyncData_DelegatesToService()
    {
        var dataStore = Substitute.For<IDataStore>();

        _sut.SyncData(dataStore);

        _service.Received(1).SyncRaceData(dataStore);
    }

    [TestMethod]
    public void Behavior_IsCampaignBehaviorBase()
    {
        Assert.IsInstanceOfType(_sut, typeof(CampaignBehaviorBase));
    }
}
