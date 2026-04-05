using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.TimeAcceleration;

[TestClass]
public class TimeAccelerationServiceTests
{
    private IMapInputAdapter _input;
    private ITimeControlAdapter _timeControl;
    private ITimeAccelerationSettingsProvider _settings;
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
        _timeControl.TimeControlMode.Returns(CampaignTimeControlMode.StoppableFastForward);

        _settings.FastForwardMultiplier.Returns(4);
        _settings.ExtraFastForwardMultiplier.Returns(8);
        _settings.CtrlSpaceMultiplier.Returns(16);

        _sut = new TimeAccelerationService(_input, _timeControl, _settings);
    }

    // --- Guard: inactive campaign ---

    [TestMethod]
    public void OnTick_CampaignInactive_DoesNothing()
    {
        // Arrange
        _timeControl.IsCampaignActive.Returns(false);
        _input.IsKeyPressed(InputKey.Space).Returns(true);

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
        _input.IsKeyPressed(InputKey.Space).Returns(true);

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
        _input.IsKeyPressed(InputKey.Space).Returns(true);

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
        _input.IsKeyPressed(InputKey.E).Returns(true);

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
        _input.IsKeyPressed(InputKey.Space).Returns(true);
        _input.IsControlDown().Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 4;
    }

    [TestMethod]
    public void OnTick_SpacePressed_DoesNotCallSetTimeSpeed()
    {
        // Arrange — Space alone preserves current time mode, just changes speed multiplier
        _input.IsKeyPressed(InputKey.Space).Returns(true);
        _input.IsControlDown().Returns(false);

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
        _input.IsKeyPressed(InputKey.Space).Returns(true);
        _input.IsControlDown().Returns(false);

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
        _input.IsKeyPressed(InputKey.E).Returns(true);
        _input.IsControlDown().Returns(false);
        _input.IsKeyPressed(InputKey.Space).Returns(false);

        // Act
        _sut.OnTick();

        // Assert
        _timeControl.Received(1).SpeedUpMultiplier = 8;
    }

    [TestMethod]
    public void OnTick_EPressed_CallsSetTimeSpeed2()
    {
        // Arrange
        _input.IsKeyPressed(InputKey.E).Returns(true);
        _input.IsControlDown().Returns(false);
        _input.IsKeyPressed(InputKey.Space).Returns(false);

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
        _input.IsKeyPressed(InputKey.E).Returns(true);
        _input.IsControlDown().Returns(false);
        _input.IsKeyPressed(InputKey.Space).Returns(false);

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
        _timeControl.TimeControlMode.Returns(CampaignTimeControlMode.StoppableFastForward);
        _input.IsControlDown().Returns(true);
        _input.IsKeyPressed(InputKey.Space).Returns(true);

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
        _timeControl.TimeControlMode.Returns(CampaignTimeControlMode.StoppableFastForward);
        _input.IsControlDown().Returns(true);
        _input.IsKeyPressed(InputKey.Space).Returns(true);
        _sut.OnTick();

        // Reset to non-pressed state
        _input.IsControlDown().Returns(false);
        _input.IsKeyPressed(InputKey.Space).Returns(false);
        _input.IsKeyReleased(InputKey.Space).Returns(false);

        // Act — release ctrl
        _sut.OnTick();

        // Assert — restored to saved speed (4) and saved mode
        _timeControl.Received(1).SpeedUpMultiplier = 4f;
        _timeControl.Received(1).TimeControlMode = CampaignTimeControlMode.StoppableFastForward;
    }

    [TestMethod]
    public void OnTick_CtrlSpaceActive_SpaceReleasedRestores()
    {
        // Arrange — activate turbo
        _timeControl.SpeedUpMultiplier.Returns(4f);
        _timeControl.TimeControlMode.Returns(CampaignTimeControlMode.StoppableFastForward);
        _input.IsControlDown().Returns(true);
        _input.IsKeyPressed(InputKey.Space).Returns(true);
        _sut.OnTick();

        // Release space while ctrl still held
        _input.IsControlDown().Returns(true);
        _input.IsKeyPressed(InputKey.Space).Returns(false);
        _input.IsKeyReleased(InputKey.Space).Returns(true);

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
        _input.IsControlDown().Returns(true);
        _input.IsKeyPressed(InputKey.Space).Returns(true);

        // Act
        _sut.OnTick();

        // Assert — turbo multiplier, not fast-forward multiplier
        _timeControl.Received(1).SpeedUpMultiplier = 16;
        _timeControl.DidNotReceive().SpeedUpMultiplier = 4;
    }
}
