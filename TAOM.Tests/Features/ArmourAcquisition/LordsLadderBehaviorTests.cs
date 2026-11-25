using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.ArmourAcquisition.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// A running ladder quest must keep counting the hero's kills while Armour Acquisition is switched off: the
/// armoury menu that takes its materials is hidden then, so its deeds are the only way to finish it.
/// </summary>
[TestClass]
public class LordsLadderBehaviorTests
{
    private IArmourGateService _gate = null!;
    private IArmouryPlayerAdapter _player = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _server = null!;
    private HeroKillTally _tally = null!;
    private LordsLadderBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);
        _gate = Substitute.For<IArmourGateService>();
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.HeroId.Returns("main_hero");
        _coop = Substitute.For<ICoopSessionProvider>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _tally = new HeroKillTally();
        var ladder = new LordsLadderService(new ArmourAcquisitionState(), config, _gate, _player,
            Substitute.For<ICultureMarketplaceConfigProvider>());
        _sut = new LordsLadderBehavior(ladder, _tally, _gate, _player, config, _coop, _server, Substitute.For<IModLogger>());
        _player.ClearReceivedCalls();
    }

    [TestMethod]
    public void HourlyTick_GateOff_StillCreditsKills()
    {
        _gate.IsActive.Returns(false);
        _tally.Add(3);

        _sut.OnHourlyTick();

        _ = _player.Received().HeroId;
    }

    [TestMethod]
    public void HourlyTick_CoopGuest_DoesNotCreditKills()
    {
        _gate.IsActive.Returns(true);
        _coop.ShouldDeferToHost.Returns(true);
        _tally.Add(3);

        _sut.OnHourlyTick();

        _ = _player.DidNotReceive().HeroId;
    }

    [TestMethod]
    public void HourlyTick_DedicatedServer_DoesNotCreditKills()
    {
        _gate.IsActive.Returns(true);
        _server.IsDedicatedServer.Returns(true);
        _tally.Add(3);

        _sut.OnHourlyTick();

        _ = _player.DidNotReceive().HeroId;
    }
}
