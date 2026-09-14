using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.StartupResources;

namespace TAOM.Tests.Features.StartupResources;

[TestClass]
public class StartupResourcesBehaviorTests
{
    private IStartupGoldService _goldService;
    private IStartupInfluenceService _influenceService;
    private IPlayerStartupGoldService _playerGoldService;
    private IModLogger _logger;
    private StartupResourcesBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _goldService = Substitute.For<IStartupGoldService>();
        _influenceService = Substitute.For<IStartupInfluenceService>();
        _playerGoldService = Substitute.For<IPlayerStartupGoldService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new StartupResourcesBehavior(_goldService, _influenceService, _playerGoldService, _logger);
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

    // v1.5.0: OnCharacterCreationIsOverEvent fires ten times (index 0..9). The re-apply runs in the
    // last phase only; the positive case needs Campaign.Current and Hero.MainHero, so it is live-game.
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void OnCharacterCreationIsOver_BeforeTheLastPhase_DoesNotReapplyPlayerGold(int index)
    {
        _sut.OnCharacterCreationIsOver(index);

        _playerGoldService.DidNotReceive().GrantPlayerStartupGold(Arg.Any<string>(), Arg.Any<string>());
    }
}
