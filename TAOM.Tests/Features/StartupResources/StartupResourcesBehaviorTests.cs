using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupResourcesBehaviorTests
{
    private IStartupGoldService _goldService;
    private IStartupInfluenceService _influenceService;
    private StartupResourcesBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _goldService = Substitute.For<IStartupGoldService>();
        _influenceService = Substitute.For<IStartupInfluenceService>();
        _sut = new StartupResourcesBehavior(_goldService, _influenceService);
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_Index1_CallsBothServices()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 1);

        _goldService.Received(1).DistributeStartupGold();
        _influenceService.Received(1).DistributeStartupInfluence();
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_Index0_DoesNotCallServices()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 0);

        _goldService.DidNotReceive().DistributeStartupGold();
        _influenceService.DidNotReceive().DistributeStartupInfluence();
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_Index2_DoesNotCallServices()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 2);

        _goldService.DidNotReceive().DistributeStartupGold();
        _influenceService.DidNotReceive().DistributeStartupInfluence();
    }

    [TestMethod]
    public void OnNewGameCreatedPartialFollowUp_CalledTwiceWithIndex1_OnlyDistributesOnce()
    {
        _sut.OnNewGameCreatedPartialFollowUp(null, 1);
        _sut.OnNewGameCreatedPartialFollowUp(null, 1);

        _goldService.Received(1).DistributeStartupGold();
        _influenceService.Received(1).DistributeStartupInfluence();
    }
}
