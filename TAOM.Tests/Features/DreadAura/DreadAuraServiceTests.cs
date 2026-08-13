using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Domain;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// The drain arithmetic IS the feature, so these tests are numeric rather than structural.
///
/// Engine constants the simulation reproduces (v1.4.8, <c>CommonAIComponent</c>):
/// initial morale = 35 + rand[0..29], mean 49.5; recovery ceiling = half of initial;
/// regeneration +0.4/sec while below that ceiling; panic below 0.01.
/// </summary>
[TestClass]
public class DreadAuraServiceTests
{
    private const float MeanInitialMorale = 49.5f;
    private const float RecoveryCeiling = MeanInitialMorale * 0.5f;
    private const float RegenPerSecond = 0.4f;
    private const float PanicThreshold = 0.01f;

    private const float Radius = 12f;
    private const float InnerRadius = 4f;

    private IDreadAuraConfigProvider _configProvider = null!;
    private IDreadAuraSettingsProvider _settings = null!;
    private IDreadRegistry _registry = null!;
    private DreadAuraConfig _config = null!;
    private DreadAuraService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new DreadAuraConfig();

        _configProvider = Substitute.For<IDreadAuraConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);

        _settings = Substitute.For<IDreadAuraSettingsProvider>();
        _settings.IsEnabled.Returns(true);

        _registry = Substitute.For<IDreadRegistry>();
        _registry.ResolveResist(Arg.Any<int?>()).Returns(1f);

        _sut = new DreadAuraService(_configProvider, _settings, _registry);
    }

    private static DreadDrainContext Ctx(
        float resistScaledRate,
        float distance = 0f,
        float morale = MeanInitialMorale,
        float elapsed = 1f,
        int? raceId = null,
        float radius = Radius,
        float innerRadius = InnerRadius)
        => new DreadDrainContext(
            resistScaledRate, distance * distance, radius, innerRadius, raceId, morale, elapsed);

    /// <summary>Drives the real service against the engine's real morale loop and returns the
    /// wall-clock seconds until the target crosses the panic threshold.</summary>
    private float SimulateRoutSeconds(float resistScaledRate, int? raceId = null, float maxSeconds = 600f)
    {
        const float dt = 0.25f;
        var morale = MeanInitialMorale;

        for (var t = 0f; t < maxSeconds; t += dt)
        {
            morale -= _sut.ComputeDrain(Ctx(resistScaledRate, morale: morale, elapsed: dt, raceId: raceId));

            // Order matters and mirrors CommonAIComponent.OnTickParallel: the panic test runs
            // FIRST, and regeneration only afterwards and only for an agent that did not panic.
            // Regenerating first would let +0.4/sec lift a broken agent back over the threshold
            // every frame and nothing would ever rout.
            if (morale < PanicThreshold)
                return t + dt;

            if (morale < RecoveryCeiling)
                morale = Math.Min(morale + RegenPerSecond * dt, RecoveryCeiling);
        }

        return float.PositiveInfinity;
    }

    // ---- Falloff geometry -------------------------------------------------

    [TestMethod]
    public void ComputeDrain_AtInnerRadius_AppliesFullRate()
    {
        var drain = _sut.ComputeDrain(Ctx(2f, distance: InnerRadius));

        Assert.AreEqual(2f, drain, 0.001f);
    }

    [TestMethod]
    public void ComputeDrain_InsideInnerRadius_AppliesFullRate()
    {
        var drain = _sut.ComputeDrain(Ctx(2f, distance: 0f));

        Assert.AreEqual(2f, drain, 0.001f);
    }

    [TestMethod]
    public void ComputeDrain_MidwayBetweenRadii_AppliesHalfRate()
    {
        // Inner 4, outer 12 — the midpoint of the falloff band is 8.
        var drain = _sut.ComputeDrain(Ctx(2f, distance: 8f));

        Assert.AreEqual(1f, drain, 0.001f);
    }

    [TestMethod]
    public void ComputeDrain_AtOuterRadius_AppliesZero()
    {
        var drain = _sut.ComputeDrain(Ctx(2f, distance: Radius));

        Assert.AreEqual(0f, drain, 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_BeyondOuterRadius_ReturnsZero()
    {
        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, distance: 12.5f)), 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_InnerRadiusNotBelowOuter_AppliesFlatRateInsideRadius()
    {
        // An MCM radius below the JSON inner radius must not divide by a negative band width.
        var drain = _sut.ComputeDrain(Ctx(2f, distance: 3f, radius: 4f, innerRadius: 4f));

        Assert.AreEqual(2f, drain, 0.001f);
    }

    // ---- Race resistance --------------------------------------------------

    [TestMethod]
    public void ComputeDrain_ResistantRace_ScalesDrainDown()
    {
        _registry.ResolveResist(7).Returns(0.4f);

        var drain = _sut.ComputeDrain(Ctx(2f, raceId: 7));

        Assert.AreEqual(0.8f, drain, 0.001f);
    }

    [TestMethod]
    public void ComputeDrain_ImmuneRace_ReturnsZero()
    {
        _registry.ResolveResist(9).Returns(0f);

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, raceId: 9)), 0.0001f);
    }

    // ---- The balance contract (golden numbers) ----------------------------

    // Vanilla CharacterObject.GetMoraleResistance() = (IsHero ? 1.5 : 1) * (0.5 * tier + 1).
    // ResistScaledRate is moralePerSecond (5.0) divided by that.
    [DataTestMethod]
    [DataRow(5f / 1.5f, 16f, DisplayName = "Tier-1 levy routs in about 16s")]
    [DataRow(5f / 2.5f, 28f, DisplayName = "Tier-3 line infantry routs in about 28s")]
    [DataRow(5f / 4.0f, 49f, DisplayName = "Tier-6 elite routs in about 49s")]
    public void ComputeDrain_ShippedRate_RoutsAtTheDocumentedTime(float resistScaledRate, float expectedSeconds)
    {
        var actual = SimulateRoutSeconds(resistScaledRate);

        Assert.AreEqual(expectedSeconds, actual, 2f,
            $"Rout time moved to {actual:F1}s. This is the balance contract in docs/features/dread-aura.md — " +
            "update the doc and the table in the same commit, or revert the retune.");
    }

    [TestMethod]
    public void ComputeDrain_ElfLineInfantry_TakesRoughlyThreeTimesLongerThanHuman()
    {
        _registry.ResolveResist(3).Returns(0.4f);

        var elf = SimulateRoutSeconds(5f / 2.5f, raceId: 3);

        Assert.AreEqual(93f, elf, 4f);
    }

    [TestMethod]
    public void ComputeDrain_DwarfVeteran_RoutsAtTheDocumentedTime()
    {
        // Tier-5 resistance 3.5, dwarf resist 0.5 -> 0.71/s. Pins the one row of the balance
        // table in docs/features/dread-aura.md that no test previously derived.
        _registry.ResolveResist(2).Returns(0.5f);

        var dwarf = SimulateRoutSeconds(5f / 3.5f, raceId: 2);

        Assert.AreEqual(113f, dwarf, 5f);
    }

    [TestMethod]
    public void ComputeDrain_ElfHero_DriftsBelowRegenRate_AndNeverRouts()
    {
        // Tier-6 hero resistance 6.0 -> 0.833/s, times elf 0.4 -> 0.333/s, under the engine's
        // 0.4/s regeneration. Immunity by arithmetic, with no special case in the code.
        _registry.ResolveResist(3).Returns(0.4f);

        var elfHero = SimulateRoutSeconds(5f / 6.0f, raceId: 3);

        Assert.AreEqual(float.PositiveInfinity, elfHero,
            "An elven hero must stay above the engine's morale-recovery floor forever.");
    }

    // ---- The morale floor -------------------------------------------------

    [TestMethod]
    public void ComputeDrain_MoraleAtFloor_ReturnsZero()
    {
        _config.Profile.MoraleFloor = 15f;

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, morale: 15f)), 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_MoraleBelowFloor_ReturnsZero()
    {
        _config.Profile.MoraleFloor = 15f;

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, morale: 14f)), 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_MoraleJustAboveFloor_NeverCrossesIt()
    {
        _config.Profile.MoraleFloor = 15f;

        var drain = _sut.ComputeDrain(Ctx(50f, morale: 15.001f, elapsed: 10f));

        Assert.IsTrue(15.001f - drain >= 15f,
            $"Drain {drain} would push the target from 15.001 below the configured floor of 15.");
    }

    [TestMethod]
    public void ComputeDrain_ZeroFloor_CanDrainToPanic()
    {
        Assert.AreEqual(float.PositiveInfinity, SimulateRoutSeconds(0.2f));
        Assert.IsTrue(SimulateRoutSeconds(5f / 2.5f) < 60f);
    }

    // ---- The no-component sentinel ----------------------------------------

    [TestMethod]
    public void ComputeDrain_MinusOneMoraleSentinel_ReturnsZero()
    {
        // agent.GetMorale() returns -1f when the agent has no CommonAIComponent (the player and
        // every non-AI agent). It must FAIL the floor gate, not pass it as "already drained".
        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, morale: -1f)), 0.0001f);
    }

    // ---- NaN and infinity gates (one per float that reaches a decision) ----

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ComputeDrain_NonFiniteRate_ReturnsZero(float rate)
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(rate)), 0.0001f);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void ComputeDrain_NonFiniteDistance_ReturnsZero(float distance)
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, distance: distance)), 0.0001f);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ComputeDrain_NonFiniteMorale_ReturnsZero(float morale)
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, morale: morale)), 0.0001f);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-1f)]
    public void ComputeDrain_NonPositiveOrNonFiniteElapsed_ReturnsZero(float elapsed)
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, elapsed: elapsed)), 0.0001f);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void ComputeDrain_NonPositiveOrNonFiniteRadius_ReturnsZero(float radius)
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, radius: radius)), 0.0001f);

    [TestMethod]
    public void ComputeDrain_NaNRadius_DoesNotAffectEveryAgentInTheMission()
    {
        // `distance > radius` is FALSE for a NaN radius, so a naive gate would admit every agent
        // on the field. The gate must be a positive requirement instead.
        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, distance: 900f, radius: float.NaN)), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void ComputeDrain_NonFiniteRaceResist_ReturnsZero(float resist)
    {
        _registry.ResolveResist(4).Returns(resist);

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f, raceId: 4)), 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_NonFiniteMoraleFloor_ReturnsZero()
    {
        _config.Profile.MoraleFloor = float.NaN;

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f)), 0.0001f);
    }

    // ---- Skip guards, both polarities -------------------------------------

    [TestMethod]
    public void ComputeDrain_Disabled_ReturnsZero()
    {
        _settings.IsEnabled.Returns(false);

        Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(2f)), 0.0001f);
    }

    [TestMethod]
    public void ComputeDrain_Enabled_Proceeds()
    {
        _settings.IsEnabled.Returns(true);

        Assert.IsTrue(_sut.ComputeDrain(Ctx(2f)) > 0f);
    }

    [TestMethod]
    public void ComputeDrain_ZeroRate_ReturnsZero()
        => Assert.AreEqual(0f, _sut.ComputeDrain(Ctx(0f)), 0.0001f);

    [TestMethod]
    public void IsEnabled_FollowsSettingsProvider()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);

        _settings.IsEnabled.Returns(true);
        Assert.IsTrue(_sut.IsEnabled);
    }

    // ---- Pulse scheduling knobs -------------------------------------------

    [TestMethod]
    public void PulseIntervalSeconds_ReadsValidatedConfig()
    {
        _config.PulseIntervalSeconds = 0.5f;

        Assert.AreEqual(0.5f, _sut.PulseIntervalSeconds, 0.0001f);
    }

    [TestMethod]
    public void MaxSourcesPerFrame_ReadsValidatedConfig()
    {
        _config.MaxSourcesPerFrame = 5;

        Assert.AreEqual(5, _sut.MaxSourcesPerFrame);
    }

    // ---- Fear voice -------------------------------------------------------

    [TestMethod]
    public void ShouldPlayFearVoice_RollBelowChance_ReturnsTrue()
    {
        _config.FearVoiceChancePerPulse = 0.5f;

        Assert.IsTrue(_sut.ShouldPlayFearVoice(0.25f));
    }

    [TestMethod]
    public void ShouldPlayFearVoice_RollAboveChance_ReturnsFalse()
    {
        _config.FearVoiceChancePerPulse = 0.5f;

        Assert.IsFalse(_sut.ShouldPlayFearVoice(0.75f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ShouldPlayFearVoice_NonFiniteRoll_ReturnsFalse(float roll)
    {
        _config.FearVoiceChancePerPulse = 1f;

        Assert.IsFalse(_sut.ShouldPlayFearVoice(roll));
    }

    [TestMethod]
    public void ShouldPlayFearVoice_ZeroChance_ReturnsFalse()
    {
        _config.FearVoiceChancePerPulse = 0f;

        Assert.IsFalse(_sut.ShouldPlayFearVoice(0f));
    }

    [TestMethod]
    public void ShouldPlayFearVoice_Disabled_ReturnsFalse()
    {
        _config.FearVoiceChancePerPulse = 1f;
        _settings.IsEnabled.Returns(false);

        Assert.IsFalse(_sut.ShouldPlayFearVoice(0f));
    }
}
