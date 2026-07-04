using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumVictoryServiceTests
{
    private IMomentumSettingsProvider _settings = null!;
    private IPlayerMomentumService _playerService = null!;
    private IWarOfTheRingService _wotrService = null!;
    private IAllianceAdapter _allianceAdapter = null!;
    private IModLogger _logger = null!;
    private MomentumWarState _state = null!;
    private MomentumVictoryService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMomentumSettingsProvider>();
        _playerService = Substitute.For<IPlayerMomentumService>();
        _wotrService = Substitute.For<IWarOfTheRingService>();
        _allianceAdapter = Substitute.For<IAllianceAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.VictoryThreshold.Returns(500);
        _settings.RequireParticipationForVictory.Returns(true);
        _playerService.HasPlayerMetVictoryRequirement().Returns(true);

        _state = new MomentumWarState();
        _state.MarkWarStarted();
        _state.Free.AddKingdom("empire_w");
        _state.Free.AddKingdom("vlandia");
        _state.Evil.AddKingdom("empire_s");

        _sut = new MomentumVictoryService(_settings, _playerService, _wotrService, _allianceAdapter, _logger);
    }

    /// <summary>+500 internal = +50000 raw.</summary>
    private void SetInternalMomentum(int internalValue)
    {
        if (internalValue >= 0)
            _state.Free.EditMomentum(internalValue * MomentumWarState.MomentumScale);
        else
            _state.Evil.EditMomentum(-internalValue * MomentumWarState.MomentumScale);
    }

    // ---- Threshold wins ----

    [TestMethod]
    public void CheckAndApplyVictory_FreeAtThreshold_FreeVictory()
    {
        SetInternalMomentum(500);

        var outcome = _sut.CheckAndApplyVictory(_state);

        Assert.AreEqual(WarOutcome.FreeVictory, outcome);
        Assert.IsTrue(_state.HasWarEnded);
        Assert.AreEqual(WarOutcome.FreeVictory, _state.Victor);
    }

    [TestMethod]
    public void CheckAndApplyVictory_EvilAtThreshold_EvilVictory()
    {
        SetInternalMomentum(-500);

        var outcome = _sut.CheckAndApplyVictory(_state);

        Assert.AreEqual(WarOutcome.EvilVictory, outcome);
    }

    [TestMethod]
    public void CheckAndApplyVictory_BelowThreshold_None()
    {
        SetInternalMomentum(499);

        Assert.AreEqual(WarOutcome.None, _sut.CheckAndApplyVictory(_state));
        Assert.IsFalse(_state.HasWarEnded);
        _wotrService.DidNotReceive().EndWar(Arg.Any<WarOutcome>());
    }

    // ---- Elimination wins ----

    [TestMethod]
    public void CheckAndApplyVictory_EvilEliminated_FreeVictory()
    {
        _state.Evil.RemoveKingdom("empire_s");

        Assert.AreEqual(WarOutcome.FreeVictory, _sut.CheckAndApplyVictory(_state));
    }

    [TestMethod]
    public void CheckAndApplyVictory_FreeEliminated_EvilVictory()
    {
        _state.Free.RemoveKingdom("empire_w");
        _state.Free.RemoveKingdom("vlandia");

        Assert.AreEqual(WarOutcome.EvilVictory, _sut.CheckAndApplyVictory(_state));
    }

    // ---- Player gate (BOTH sides — LOTRAOM parity) ----

    [TestMethod]
    public void CheckAndApplyVictory_PlayerGateUnmet_BlocksFreeVictory()
    {
        _playerService.HasPlayerMetVictoryRequirement().Returns(false);
        SetInternalMomentum(600);

        Assert.AreEqual(WarOutcome.None, _sut.CheckAndApplyVictory(_state));
        Assert.IsFalse(_state.HasWarEnded);
    }

    [TestMethod]
    public void CheckAndApplyVictory_PlayerGateUnmet_BlocksEvilVictoryToo()
    {
        _playerService.HasPlayerMetVictoryRequirement().Returns(false);
        SetInternalMomentum(-600);

        Assert.AreEqual(WarOutcome.None, _sut.CheckAndApplyVictory(_state));
    }

    [TestMethod]
    public void CheckAndApplyVictory_GateDisabled_WinsWithoutPlayerEvents()
    {
        _settings.RequireParticipationForVictory.Returns(false);
        _playerService.HasPlayerMetVictoryRequirement().Returns(false);
        SetInternalMomentum(500);

        Assert.AreEqual(WarOutcome.FreeVictory, _sut.CheckAndApplyVictory(_state));
    }

    // ---- Lifecycle guards ----

    [TestMethod]
    public void CheckAndApplyVictory_WarNotStarted_None()
    {
        var fresh = new MomentumWarState();
        Assert.AreEqual(WarOutcome.None, _sut.CheckAndApplyVictory(fresh));
    }

    [TestMethod]
    public void CheckAndApplyVictory_AlreadyEnded_NoneAndNoSideEffects()
    {
        SetInternalMomentum(500);
        _sut.CheckAndApplyVictory(_state);
        _wotrService.ClearReceivedCalls();
        _allianceAdapter.ClearReceivedCalls();

        var outcome = _sut.CheckAndApplyVictory(_state);

        Assert.AreEqual(WarOutcome.None, outcome);
        _wotrService.DidNotReceive().EndWar(Arg.Any<WarOutcome>());
        _allianceAdapter.DidNotReceiveWithAnyArgs().MakePeace(default, default);
    }

    // ---- Victory application: ordering + peace-out ----

    [TestMethod]
    public void CheckAndApplyVictory_Victory_EndsWarBeforeMakingPeace()
    {
        // MakePeaceAction is blocked until the phase leaves FullWar — order is load-bearing.
        SetInternalMomentum(500);
        _allianceAdapter.AreAtWar("empire_w", "empire_s").Returns(true);

        _sut.CheckAndApplyVictory(_state);

        Received.InOrder(() =>
        {
            _wotrService.EndWar(WarOutcome.FreeVictory);
            _allianceAdapter.MakePeace("empire_w", "empire_s");
        });
    }

    [TestMethod]
    public void CheckAndApplyVictory_Victory_PeacesOutOnlyCrossSideAtWarPairs()
    {
        SetInternalMomentum(500);
        _allianceAdapter.AreAtWar("empire_w", "empire_s").Returns(true);
        _allianceAdapter.AreAtWar("vlandia", "empire_s").Returns(false);

        _sut.CheckAndApplyVictory(_state);

        _allianceAdapter.Received(1).MakePeace("empire_w", "empire_s");
        _allianceAdapter.DidNotReceive().MakePeace("vlandia", "empire_s");
        // Same-side pairs are never touched.
        _allianceAdapter.DidNotReceive().MakePeace("empire_w", "vlandia");
        _allianceAdapter.DidNotReceive().MakePeace("vlandia", "empire_w");
    }

    [TestMethod]
    public void CheckAndApplyVictory_Victory_FreezesStateBeforeEndWar()
    {
        SetInternalMomentum(500);
        bool stateWasFrozenWhenEndWarRan = false;
        _wotrService.When(s => s.EndWar(Arg.Any<WarOutcome>()))
            .Do(_ => stateWasFrozenWhenEndWarRan = _state.HasWarEnded);

        _sut.CheckAndApplyVictory(_state);

        Assert.IsTrue(stateWasFrozenWhenEndWarRan);
    }
}
