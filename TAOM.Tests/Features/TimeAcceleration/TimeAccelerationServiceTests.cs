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
    private ICoopPresenceProvider _coop;
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

        _coop = Substitute.For<ICoopPresenceProvider>();
        _coop.IsCoopActive.Returns(false);

        _sut = new TimeAccelerationService(_input, _timeControl, _settings, _coop);
    }

    // --- Co-op interop (#370) -------------------------------------------------------------
    // Suppressing the MapBar button did not suppress the mechanic: E / Space / Ctrl+Space reach
    // this service directly. SpeedUpMultiplier in particular is a different property from
    // TimeControlMode, and nothing is known to intercept it, so an ungated keypress mutated
    // campaign tick state locally under co-op.

    [TestMethod]
    public void OnTick_CoopActive_IgnoresExtraFastForwardKey()
    {
        // Arrange
        _coop.IsCoopActive.Returns(true);
        _input.IsEKeyPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
        _timeControl.DidNotReceiveWithAnyArgs().SetTimeSpeed(default);
    }

    [TestMethod]
    public void OnTick_CoopActive_IgnoresSpace()
    {
        _coop.IsCoopActive.Returns(true);
        _input.IsSpacePressed.Returns(true);

        _sut.OnTick();

        _timeControl.DidNotReceiveWithAnyArgs().SpeedUpMultiplier = default;
    }

    [TestMethod]
    public void OnTick_CoopActive_IgnoresCtrlSpaceTurbo()
    {
        _coop.IsCoopActive.Returns(true);
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(true);

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
        _input.IsSpacePressed.Returns(true);
        _sut.OnTick();
        _timeControl.ClearReceivedCalls();

        // Act
        _coop.IsCoopActive.Returns(true);
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
        _input.IsSpacePressed.Returns(true);

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
        _input.IsSpacePressed.Returns(true);

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
        _input.IsSpacePressed.Returns(true);

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
        _input.IsEKeyPressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — should process the E key
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    // --- Space key: sets fast-forward multiplier, no SetTimeSpeed ---

    [TestMethod]
    public void OnTick_SpacePressed_SetsFastForwardMultiplier()
    {
        // Arrange
        _input.IsSpacePressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 4;
    }

    [TestMethod]
    public void OnTick_SpacePressed_DoesNotCallSetTimeSpeed()
    {
        // Arrange — Space alone preserves current time mode, just changes speed multiplier
        _input.IsSpacePressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.DidNotReceive().SetTimeSpeed(Arg.Any<int>());
    }

    [TestMethod]
    public void OnTick_SpacePressed_UsesConfiguredMultiplier()
    {
        // Arrange
        _settings.FastForwardMultiplier.Returns(6);
        _input.IsSpacePressed.Returns(true);
        _input.IsControlDown.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 6;
    }

    // --- E key: extra fast-forward ---

    [TestMethod]
    public void OnTick_EPressed_SetsExtraFastForwardMultiplier()
    {
        // Arrange
        _input.IsEKeyPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsSpacePressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 8;
    }

    [TestMethod]
    public void OnTick_EPressed_CallsSetTimeSpeed2()
    {
        // Arrange
        _input.IsEKeyPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsSpacePressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_EPressed_UsesConfiguredMultiplier()
    {
        // Arrange
        _settings.ExtraFastForwardMultiplier.Returns(12);
        _input.IsEKeyPressed.Returns(true);
        _input.IsControlDown.Returns(false);
        _input.IsSpacePressed.Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 12;
    }

    // --- Ctrl+Space: turbo mode ---

    [TestMethod]
    public void OnTick_CtrlSpacePressed_SavesPriorState()
    {
        // Arrange
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — multiplier set to turbo
        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.Received(1).SetTimeSpeed(2);
    }

    [TestMethod]
    public void OnTick_CtrlSpaceReleased_RestoresPriorState()
    {
        // Arrange — activate turbo first
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(true);
        _sut.OnTick();

        // Reset to non-pressed state
        _input.IsControlDown.Returns(false);
        _input.IsSpacePressed.Returns(false);
        _input.IsSpaceReleased.Returns(false);

        // Act — release ctrl
        _sut.OnTick();

        // Assert — restored to saved speed (4) and saved mode
        _timeControl.Received(1).SpeedUpMultiplier = 4f;
        _timeControl.Received(1).TimeControlMode = StoppableFastForward;
    }

    [TestMethod]
    public void OnTick_CtrlSpaceActive_SpaceReleasedRestores()
    {
        // Arrange — activate turbo
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(StoppableFastForward);
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(true);
        _sut.OnTick();

        // Release space while ctrl still held
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(false);
        _input.IsSpaceReleased.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — restored
        _timeControl.Received(1).SpeedUpMultiplier = 4f;
    }

    // --- CtrlSpace takes priority over Space alone ---

    [TestMethod]
    public void OnTick_CtrlAndSpace_ActivatesTurboNotFastForward()
    {
        // Arrange — both ctrl and space pressed
        _input.IsControlDown.Returns(true);
        _input.IsSpacePressed.Returns(true);

        // Act
        _sut.OnTick();

        // Assert — turbo multiplier, not fast-forward multiplier
        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 4;
    }
}
