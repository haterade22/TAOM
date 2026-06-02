using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class AbilityActivationControllerTests
{
    private const string HeroId = "hero_1";
    private const float ChargingThrottleSeconds = 2f;

    private ICareerAbilityService _abilityService;
    private IAbilityInputAdapter _input;
    private IMissionTimeProvider _time;
    private AbilityActivationController _sut;

    [TestInitialize]
    public void Setup()
    {
        _abilityService = Substitute.For<ICareerAbilityService>();
        _input = Substitute.For<IAbilityInputAdapter>();
        _time = Substitute.For<IMissionTimeProvider>();
        _sut = new AbilityActivationController(_abilityService, _input, _time);
    }

    [TestMethod]
    public void Tick_NoCareer_ReturnsEmptyResultAndDoesNotTickAbility()
    {
        var result = _sut.Tick(0.016f, HeroId, hasCareer: false);

        Assert.IsFalse(result.JustBecameReady);
        Assert.IsFalse(result.Activated);
        Assert.IsFalse(result.Charging);
        _abilityService.DidNotReceive().Tick(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_HasCareer_TicksAbilityWithDt()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);

        _sut.Tick(0.016f, HeroId, hasCareer: true);

        _abilityService.Received(1).Tick(HeroId, 0.016f);
    }

    [TestMethod]
    public void Tick_ReadyFirstTime_FlagsJustBecameReady()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(true);

        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.JustBecameReady);
        Assert.IsFalse(result.Activated);
        Assert.IsFalse(result.Charging);
    }

    [TestMethod]
    public void Tick_AlreadyNotifiedReady_DoesNotReFireJustBecameReady()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(true);

        var first = _sut.Tick(0.016f, HeroId, hasCareer: true);
        var second = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(first.JustBecameReady);
        Assert.IsFalse(second.JustBecameReady);
    }

    [TestMethod]
    public void Tick_VPressedWhileReady_FlagsActivatedAndCallsService()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(true);
        _input.IsActivationKeyPressed().Returns(true);

        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.Activated);
        _abilityService.Received(1).ActivateAbility(HeroId);
    }

    [TestMethod]
    public void Tick_ReadyAndVPressedSameFrame_FlagsBothJustBecameReadyAndActivated()
    {
        // Deep-review #102 MED regression gate — pre-refactor the legacy 302-line behavior
        // emitted BOTH the green "ready" toast AND the yellow "activated" toast on the same
        // tick. The single-outcome enum dropped the ready toast. The flags struct preserves
        // both signals so the host can emit both InformationManager messages.
        _abilityService.IsAbilityReady(HeroId).Returns(true);
        _input.IsActivationKeyPressed().Returns(true);

        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.JustBecameReady, "JustBecameReady must remain true even when Activated also fires");
        Assert.IsTrue(result.Activated);
        Assert.IsFalse(result.Charging);
        _abilityService.Received(1).ActivateAbility(HeroId);
    }

    [TestMethod]
    public void Tick_VPressedWhileReady_ReArmsReadyNotificationForNextCycle()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(true);
        _input.IsActivationKeyPressed().Returns(true);
        _sut.Tick(0.016f, HeroId, hasCareer: true); // Activated; resets _abilityReadyNotified

        _input.IsActivationKeyPressed().Returns(false);
        var nextResult = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(nextResult.JustBecameReady);
    }

    [TestMethod]
    public void Tick_VPressedWhileCharging_FlagsCharging()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);
        _input.IsActivationKeyPressed().Returns(true);
        _time.CurrentTime.Returns(0f); // _lastChargingMessageTime starts at -2f, so 0 - (-2) = 2 >= throttle

        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.Charging);
        Assert.IsFalse(result.Activated);
    }

    [TestMethod]
    public void Tick_VPressedTwiceWithinThrottle_SecondDoesNotReFireCharging()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);
        _input.IsActivationKeyPressed().Returns(true);

        _time.CurrentTime.Returns(0f);
        var first = _sut.Tick(0.016f, HeroId, hasCareer: true);

        _time.CurrentTime.Returns(1.5f); // 1.5 < 2.0 throttle window
        var second = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(first.Charging);
        Assert.IsFalse(second.Charging);
    }

    [TestMethod]
    public void Tick_VPressedAfterThrottleElapsed_FlagsChargingAgain()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);
        _input.IsActivationKeyPressed().Returns(true);

        _time.CurrentTime.Returns(0f);
        _sut.Tick(0.016f, HeroId, hasCareer: true);

        _time.CurrentTime.Returns(2.1f); // 2.1 - 0 = 2.1 >= 2.0 throttle
        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.Charging);
    }

    [TestMethod]
    public void Tick_VNotPressed_StateMachineDoesNotEmitCharging()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);
        _input.IsActivationKeyPressed().Returns(false);

        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsFalse(result.Charging);
    }

    [TestMethod]
    public void Reset_ClearsReadyNotificationFlag()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(true);
        _sut.Tick(0.016f, HeroId, hasCareer: true); // _abilityReadyNotified := true

        _sut.Reset();

        var after = _sut.Tick(0.016f, HeroId, hasCareer: true);
        Assert.IsTrue(after.JustBecameReady);
    }

    [TestMethod]
    public void Reset_ClearsThrottleSentinel()
    {
        _abilityService.IsAbilityReady(HeroId).Returns(false);
        _input.IsActivationKeyPressed().Returns(true);
        _time.CurrentTime.Returns(100f);
        _sut.Tick(0.016f, HeroId, hasCareer: true); // _lastChargingMessageTime := 100

        _sut.Reset();

        _time.CurrentTime.Returns(0f); // mission restart; time resets
        var result = _sut.Tick(0.016f, HeroId, hasCareer: true);

        Assert.IsTrue(result.Charging);
    }
}
