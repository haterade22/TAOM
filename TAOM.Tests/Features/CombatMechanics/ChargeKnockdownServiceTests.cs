using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Domain;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class ChargeKnockdownServiceTests
{
    private ICombatMechanicsConfigProvider _configProvider;
    private ICombatMechanicsSettingsProvider _settings;
    private IRaceCombatModifiersResolver _raceModifiers;
    private CombatMechanicsConfig _config;
    private ChargeKnockdownService _sut;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _raceModifiers = Substitute.For<IRaceCombatModifiersResolver>();

        _config = new CombatMechanicsConfig();
        _configProvider.GetConfig().Returns(_config);
        _settings.ChargeKnockdownEnabled.Returns(true);
        _settings.ChargeAutoKnockdownWeightRatio.Returns(8f);
        _raceModifiers.Resolve(Arg.Any<int?>()).Returns(RaceCombatModifiers.Neutral);

        _sut = CreateSut();
    }

    // Config is cached at construction — tests that mutate _config.ChargeKnockdown build a fresh SUT.
    private ChargeKnockdownService CreateSut() =>
        new ChargeKnockdownService(_configProvider, _settings, _raceModifiers);

    // Defaults model the vanilla-parity charge: Native horse 400 + rider 80 vs man 80 (ratio 6.0 ==
    // neutral), velocity == speed reference (speedFactor 1), damage 50 over the ≈20 threshold.
    private static ChargeKnockdownContext Context(
        bool isHorseCharge = true,
        float chargeVelocity = 4.3f,
        int chargerWeight = 400,
        int riderWeight = 80,
        int victimWeight = 80,
        int? victimRaceId = null,
        float victimMaxHealth = 100f,
        float inflictedDamage = 50f,
        float victimKnockDownResistance = 0.6f,
        float chargerSpeedLimitForCharge = 4.3f,
        bool hasShrugOffFlag = false,
        bool hasKnockBackFlag = true)
    {
        return new ChargeKnockdownContext(
            isHorseCharge,
            chargeVelocity,
            chargerWeight,
            riderWeight,
            victimWeight,
            victimRaceId,
            victimMaxHealth,
            inflictedDamage,
            victimKnockDownResistance,
            chargerSpeedLimitForCharge,
            hasShrugOffFlag,
            hasKnockBackFlag);
    }

    [TestMethod]
    public void DecideChargeKnockdown_Disabled_ReturnsNull()
    {
        _settings.ChargeKnockdownEnabled.Returns(false);

        var result = _sut.DecideChargeKnockdown(Context());

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_NotHorseCharge_ReturnsNull()
    {
        var result = _sut.DecideChargeKnockdown(Context(isHorseCharge: false));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_ShrugOffFlag_ReturnsNull()
    {
        // Engine-faithful: even a Branch A-qualifying mûmakil charge defers when ShrugOff is set.
        var context = Context(chargerWeight: 9999, hasShrugOffFlag: true);

        var result = _sut.DecideChargeKnockdown(context);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_MumakilVersusMan_BranchAIgnoresKnockBackFlag()
    {
        // 9999 + 80 vs 80 → ratio ≈ 126 ≥ 8; speedFactor 0.5 clears the 0.4 gate; Branch A never
        // consults the KnockBack (0.7-dot) flag.
        var context = Context(
            chargeVelocity: 0.5f,
            chargerWeight: 9999,
            riderWeight: 80,
            victimWeight: 80,
            chargerSpeedLimitForCharge: 1f,
            hasKnockBackFlag: false);

        var result = _sut.DecideChargeKnockdown(context);

        Assert.IsTrue(result.HasValue);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_BranchASpeedGateFails_NoKnockBackFlag_ReturnsNull()
    {
        // Same overwhelming mass but speedFactor 0.3 < 0.4 → Branch A skipped; without the
        // KnockBack flag Branch B declines ownership.
        var context = Context(
            chargeVelocity: 0.3f,
            chargerWeight: 9999,
            riderWeight: 80,
            victimWeight: 80,
            chargerSpeedLimitForCharge: 1f,
            hasKnockBackFlag: false);

        var result = _sut.DecideChargeKnockdown(context);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_BelowAutoRatioWithoutKnockBackFlag_ReturnsNull()
    {
        // Ratio 6.0 < 8 → Branch B territory; no KnockBack flag → null even with damage that
        // would trivially clear the Branch B threshold.
        var context = Context(inflictedDamage: 100f, hasKnockBackFlag: false);

        var result = _sut.DecideChargeKnockdown(context);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_VanillaParityCharge_DamageAtThreshold_ReturnsTrue()
    {
        // Ratio 6.0 == neutral → clamp factor 1; velocity == reference → speedFactor 1; pen ==
        // HorseChargePenetration (0.4) exactly. The threshold mirrors the service's own float
        // expression: 0.6f − 0.4f rounds one ulp above 0.2f, so a literal 20f would sit just
        // below the inclusive ≥ boundary.
        float resistance = 0.6f;
        float penetration = 0.4f;
        float maxHealth = 100f;
        float threshold = maxHealth * Math.Max(0f, resistance - penetration);

        var result = _sut.DecideChargeKnockdown(Context(
            victimMaxHealth: maxHealth,
            inflictedDamage: threshold,
            victimKnockDownResistance: resistance));

        Assert.IsTrue(result.HasValue);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_VanillaParityCharge_DamageBelowThreshold_ReturnsOwnedFalse()
    {
        // Branch B's false is an OWNED verdict (deliberately stricter than vanilla for light
        // chargers) — it must not degrade to null.
        float resistance = 0.6f;
        float penetration = 0.4f;
        float maxHealth = 100f;
        float threshold = maxHealth * Math.Max(0f, resistance - penetration);

        var result = _sut.DecideChargeKnockdown(Context(
            victimMaxHealth: maxHealth,
            inflictedDamage: threshold - 1f,
            victimKnockDownResistance: resistance));

        Assert.IsTrue(result.HasValue);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_HeavierVictim_ScaledPenetrationFlipsVerdict()
    {
        // vs man: ratio 6.0 → pen 0.4 → threshold ≈ 20 → damage 25 knocks down.
        // vs troll (160): ratio 3.0 → clamp(3/6) = 0.5 → pen 0.2 → threshold ≈ 40 → stays up.
        var vsMan = Context(victimWeight: 80, inflictedDamage: 25f);
        var vsTroll = Context(victimWeight: 160, inflictedDamage: 25f);

        var manResult = _sut.DecideChargeKnockdown(vsMan);
        var trollResult = _sut.DecideChargeKnockdown(vsTroll);

        Assert.IsTrue(manResult.HasValue);
        Assert.IsTrue(manResult.Value);
        Assert.IsTrue(trollResult.HasValue);
        Assert.IsFalse(trollResult.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_DwarfResistanceRow_FlipsKnockdownToFalse()
    {
        // Dwarf 2.5 multiplier: threshold = 100 × max(0, 0.6 × 2.5 − 0.4) = 110 — damage 25 that
        // floors a neutral-race victim (threshold ≈ 20) leaves the dwarf standing.
        _raceModifiers.Resolve(3).Returns(new RaceCombatModifiers { KnockdownResistanceMultiplier = 2.5f });
        var neutralVictim = Context(victimRaceId: null, inflictedDamage: 25f);
        var dwarfVictim = Context(victimRaceId: 3, inflictedDamage: 25f);

        var neutralResult = _sut.DecideChargeKnockdown(neutralVictim);
        var dwarfResult = _sut.DecideChargeKnockdown(dwarfVictim);

        Assert.IsTrue(neutralResult.HasValue);
        Assert.IsTrue(neutralResult.Value);
        Assert.IsTrue(dwarfResult.HasValue);
        Assert.IsFalse(dwarfResult.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_SpeedLimitSentinel_FallsBackToDefaultReference()
    {
        // Humanoid Monsters carry float.MaxValue for relative_speed_limit_for_charge. Without the
        // fallback speedFactor would be ≈0 (fails Branch A's 0.4 gate, and no KnockBack flag →
        // null); with the 4.3 default it is 1.0 → Branch A → true.
        var context = Context(
            chargeVelocity: 4.3f,
            chargerWeight: 9999,
            riderWeight: 0,
            victimWeight: 80,
            chargerSpeedLimitForCharge: float.MaxValue,
            hasKnockBackFlag: false);

        var result = _sut.DecideChargeKnockdown(context);

        Assert.IsTrue(result.HasValue);
        Assert.IsTrue(result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_IncludeRiderWeightFalse_DropsRiderFromRatio()
    {
        // 560 + 80 rider vs 80 → ratio 8.0 → Branch A true; with the rider excluded → 7.0 → falls
        // to Branch B → null (no KnockBack flag).
        var context = Context(chargerWeight: 560, riderWeight: 80, victimWeight: 80, hasKnockBackFlag: false);
        var withRider = _sut.DecideChargeKnockdown(context);

        _config.ChargeKnockdown.IncludeRiderWeight = false;
        var sutWithoutRider = CreateSut();
        var withoutRider = sutWithoutRider.DecideChargeKnockdown(context);

        Assert.IsTrue(withRider.HasValue);
        Assert.IsTrue(withRider.Value);
        Assert.IsNull(withoutRider);
    }

    [TestMethod]
    public void DecideChargeKnockdown_ZeroVictimWeight_TreatedAsWeightOne()
    {
        // Without the Math.Max floor, 7 / 0 → +Infinity would trip Branch A; the floor makes the
        // ratio 7 < 8 → null. Charger 8 vs floor-1 hits the gate exactly → true.
        var below = Context(chargerWeight: 7, riderWeight: 0, victimWeight: 0, hasKnockBackFlag: false);
        var at = Context(chargerWeight: 8, riderWeight: 0, victimWeight: 0, hasKnockBackFlag: false);

        var belowResult = _sut.DecideChargeKnockdown(below);
        var atResult = _sut.DecideChargeKnockdown(at);

        Assert.IsNull(belowResult);
        Assert.IsTrue(atResult.HasValue);
        Assert.IsTrue(atResult.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_NaNChargeVelocity_ReturnsNull()
    {
        // Corrupt engine input must defer to vanilla, not become an owned false verdict
        // (deep-review 2026-07-02 NaN-polarity audit).
        var result = _sut.DecideChargeKnockdown(Context(chargeVelocity: float.NaN));

        Assert.IsNull(result);
    }

    [TestMethod]
    public void DecideChargeKnockdown_NaNKnockDownResistance_ReturnsNull()
    {
        var result = _sut.DecideChargeKnockdown(Context(victimKnockDownResistance: float.NaN));

        Assert.IsNull(result);
    }
}
