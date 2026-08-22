using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.TimeAcceleration;

[TestClass]
public class TimeAccelerationServiceTests
{
    private const int StoppableFastForward = 4;

    private IMapInputAdapter _input;
    private ITimeControlAdapter _timeControl;
    private ITimeAccelerationSettingsProvider _settings;
    private ICoopSessionProvider _coop;
    private TimeAccelerationService _sut;

    [TestInitialize]
    public void Setup()
    {
        _input = Substitute.For<IMapInputAdapter>();
        _timeControl = Substitute.For<ITimeControlAdapter>();
        _settings = Substitute.For<ITimeAccelerationSettingsProvider>();

        _input.IsMapActive.Returns(true);
        _timeControl.IsCampaignActive.Returns(true);
        _timeControl.IsMenuOpen.Returns(false);
        _timeControl.IsTimeControlLocked.Returns(false);
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);

        _settings.FastForwardMultiplier.Returns(4);
        _settings.ExtraFastForwardMultiplier.Returns(8);
        _settings.CtrlSpaceMultiplier.Returns(16);

        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(false);

        _sut = new TimeAccelerationService(_input, _timeControl, _settings, _coop);
    }

    /// <summary>
    /// Simulates a press of the key that fast-forward and turbo BOTH default to. Since they ship
    /// bound to the same physical key, one press raises both adapter flags, and only the Ctrl
    /// modifier separates the two actions.
    /// </summary>
    private void PressSharedDefaultKey()
    {
        _input.IsFastForwardPressed.Returns(true);
        _input.IsTurboPressed.Returns(true);
    }

    // --- Co-op interop (#370) -------------------------------------------------------------
    // Suppressing the MapBar button did not suppress the mechanic: the time-control keys reach
    // this service directly. SpeedUpMultiplier in particular is a different property from
    // TimeControlMode, and nothing is known to intercept it, so an ungated keypress mutated
    // campaign tick state locally under co-op.

    [TestMethod]
    public void OnTick_CoopActive_IgnoresExtraFastForwardKey()
    {
        // Arrange
        _coop.ShouldDeferToHost.Returns(true);
        _input.IsExtraFastForwardPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
        _timeControl.DidNotReceiveWithAnyArgs().SetTimeSpeed(default);
    }

    [TestMethod]
    public void OnTick_CoopActive_IgnoresFastForwardKey()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _input.IsFastForwardPressed.Returns(true);

        _sut.OnTick();

        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
    }

    [TestMethod]
    public void OnTick_CoopActive_IgnoresTurbo()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();

        _sut.OnTick();

        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
        _timeControl.DidNotReceiveWithAnyArgs().SetTimeSpeed(default);
    }

    [TestMethod]
    public void OnTick_CoopBecomesActiveMidTurbo_RestoresSavedSpeedRatherThanLatching()
    {
        // Arrange — turbo engaged while solo, then co-op reads active on a later tick. The restore
        // must still run: a toggle must never latch the engine at a boosted multiplier
        // (harmony-patches.md "Latches & Toggle Gates" — transition first, gate after).
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();
        _sut.OnTick();
        _timeControl.ClearReceivedCalls();

        // Act
        _coop.ShouldDeferToHost.Returns(true);
        _sut.OnTick();

        // Assert — saved values restored, not left boosted.
        _timeControl.Received().SpeedUpMultiplier = 4f;
        _timeControl.Received().TimeControlMode = StoppableFastForward;
    }

    // --- Guard: inactive campaign ---

    [TestMethod]
    public void OnTick_CampaignInactive_DoesNothing()
    {
        // Arrange
        _timeControl.IsCampaignActive.Returns(false);
        _input.IsFastForwardPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
    }

    [TestMethod]
    public void OnTick_MapInactive_DoesNothing()
    {
        // Arrange
        _input.IsMapActive.Returns(false);
        _input.IsFastForwardPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    [TestMethod]
    public void OnTick_MenuOpenAndNotLocked_DoesNothing()
    {
        // Arrange
        _timeControl.IsMenuOpen.Returns(true);
        _timeControl.IsTimeControlLocked.Returns(false);
        _input.IsFastForwardPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    // --- Guard: menu open but locked (allowed) ---

    [TestMethod]
    public void OnTick_MenuOpenButLocked_AllowsSpeedChange()
    {
        // Arrange — time-control lock means it's in an uninterruptible fast-forward state
        _timeControl.IsMenuOpen.Returns(true);
        _timeControl.IsTimeControlLocked.Returns(true);
        _input.IsExtraFastForwardPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — should process the extra fast-forward key
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    // --- Fast-forward key: sets multiplier, no SetTimeSpeed ---

    [TestMethod]
    public void OnTick_FastForwardPressed_SetsFastForwardMultiplier()
    {
        // Arrange
        _input.IsFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 4;
    }

    [TestMethod]
    public void OnTick_FastForwardPressed_DoesNotCallSetTimeSpeed()
    {
        // Arrange — fast-forward alone preserves current time mode, just changes speed multiplier.
        // That is what keeps it riding vanilla's own MapTimeTogglePause handling.
        _input.IsFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    [TestMethod]
    public void OnTick_FastForwardPressed_UsesConfiguredMultiplier()
    {
        // Arrange
        _settings.FastForwardMultiplier.Returns(6);
        _input.IsFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 6;
    }

    // --- Extra fast-forward key (the one that used to be hardcoded to E) ---

    [TestMethod]
    public void OnTick_ExtraFastForwardPressed_SetsExtraFastForwardMultiplier()
    {
        // Arrange
        _input.IsExtraFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsFastForwardPressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 8;
    }

    [TestMethod]
    public void OnTick_ExtraFastForwardPressed_CallsSetTimeSpeed2()
    {
        // Arrange
        _input.IsExtraFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsFastForwardPressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_ExtraFastForwardPressed_UsesConfiguredMultiplier()
    {
        // Arrange
        _settings.ExtraFastForwardMultiplier.Returns(12);
        _input.IsExtraFastForwardPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsFastForwardPressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 12;
    }

    // --- Turbo (Ctrl + the turbo key) ---

    [TestMethod]
    public void OnTick_CtrlTurboPressed_SavesPriorState()
    {
        // Arrange
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();

        // Act
        _sut.OnTick();

        // Assert — multiplier set to turbo
        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_CtrlReleased_RestoresPriorState()
    {
        // Arrange — activate turbo first
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();
        _sut.OnTick();

        // Reset to non-pressed state
        _input.IsControlDown.Returns(false);
        _input.IsFastForwardPressed.Returns(false);
        _input.IsTurboPressed.Returns(false);
        _input.IsTurboReleased.Returns(false);

        // Act — release ctrl
        _sut.OnTick();

        // Assert — restored to saved speed (4) and saved mode
        _timeControl.Received(1).SpeedUpMultiplier = 4f;
        _timeControl.Received(1).TimeControlMode = StoppableFastForward;
    }

    [TestMethod]
    public void OnTick_TurboActive_TurboKeyReleasedRestores()
    {
        // Arrange — activate turbo
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();
        _sut.OnTick();

        // Release the turbo key while ctrl is still held. MapInputAdapter also reports IsTurboReleased
        // for an UNBOUND turbo key, so this same state is what rescues an active turbo when the
        // player clears that binding in Options while still holding Ctrl.
        _input.IsControlDown.Returns(true);
        _input.IsFastForwardPressed.Returns(false);
        _input.IsTurboPressed.Returns(false);
        _input.IsTurboReleased.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — restored
        _timeControl.Received(1).SpeedUpMultiplier = 4f;
    }

    // --- Turbo takes priority over fast-forward ---

    [TestMethod]
    public void OnTick_CtrlAndSharedKey_ActivatesTurboNotFastForward()
    {
        // Arrange — ctrl plus the key both actions default to
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();

        // Act
        _sut.OnTick();

        // Assert — turbo multiplier, not fast-forward multiplier
        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 4;
    }

    // --- Rebinding: the whole point of the change ---

    [TestMethod]
    public void OnTick_SharedDefaultKeyWithoutCtrl_FastForwardsRatherThanTurbos()
    {
        // Fast-forward and turbo ship bound to the same key, so a plain press raises BOTH flags.
        // Only the Ctrl modifier may promote it to turbo; without Ctrl this must stay fast-forward.
        _input.IsControlDown.Returns(false);
        PressSharedDefaultKey();

        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 4;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 16;
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    [TestMethod]
    public void OnTick_TurboReboundOffTheFastForwardKey_CtrlPlusFastForwardDoesNotTurbo()
    {
        // Player moved turbo to its own key. Ctrl plus the fast-forward key must no longer turbo,
        // which is only expressible now that the two are separate bindings.
        _input.IsControlDown.Returns(true);
        _input.IsFastForwardPressed.Returns(true);
        _input.IsTurboPressed.Returns(false);

        _sut.OnTick();

        _timeControl.DidNotReceive().SpeedUpMultiplier = 16;
        _timeControl.Received(1).SpeedUpMultiplier = 4;
    }

    [TestMethod]
    public void OnTick_TurboReboundToItsOwnKey_ActivatesWithoutTheFastForwardKey()
    {
        // The mirror case: turbo fires on its own binding with fast-forward untouched.
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        _input.IsTurboPressed.Returns(true);
        _input.IsFastForwardPressed.Returns(false);

        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_FastForwardOnTheSharedVanillaKey_LeavesTheModeToVanilla()
    {
        // Default binding: TAOM's fast-forward key IS vanilla's MapTimeTogglePause, so vanilla's own
        // handler changes the mode for this same press. Taking it over would force fast-forward on
        // every press and break the vanilla toggle.
        _input.IsFastForwardPressed.Returns(true);
        _input.FastForwardOwnsTimeMode.Returns(false);

        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 4;
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    [TestMethod]
    public void OnTick_FastForwardReboundOffTheVanillaTimeKey_EntersFastForwardMode()
    {
        // Rebound, so nothing else will change the mode. Campaign.TickMapTime applies
        // SpeedUpMultiplier ONLY in the fast-forward modes, so without this the key does nothing.
        _input.IsFastForwardPressed.Returns(true);
        _input.FastForwardOwnsTimeMode.Returns(true);

        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 4;
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_TurboPressedAgainWhileActive_DoesNotOverwriteTheSavedState()
    {
        // Regression: a second observed press used to re-save the ALREADY BOOSTED values, so the
        // restore below would put back 16 instead of 4 and leave the engine turboed with the latch
        // closed and nothing left to undo it.
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        PressSharedDefaultKey();
        _sut.OnTick();

        // Engine is now boosted; a second press edge arrives while turbo is still active.
        _timeControl.SpeedUpMultiplier.Returns(16f);
        _sut.OnTick();
        _timeControl.ClearReceivedCalls();

        // Release Ctrl: the ORIGINAL pre-turbo speed must come back, not the boosted one.
        _input.IsControlDown.Returns(false);
        _input.IsTurboPressed.Returns(false);
        _input.IsFastForwardPressed.Returns(false);
        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 4f;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 16f;
    }

    [TestMethod]
    public void OnTick_ExtraFastForwardReboundOffE_StillTakesPriorityOverFastForward()
    {
        // The extra fast-forward branch sits above fast-forward in the chain regardless of which
        // key it is bound to.
        _input.IsControlDown.Returns(false);
        _input.IsExtraFastForwardPressed.Returns(true);
        _input.IsFastForwardPressed.Returns(true);

        _sut.OnTick();

        _timeControl.Received(1).SpeedUpMultiplier = 8;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 4;
    }
}
