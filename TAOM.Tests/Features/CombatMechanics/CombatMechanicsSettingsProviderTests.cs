using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// The MCM-over-JSON merge for the charge knobs (#610). <c>TaomSettings.Instance</c> is MCM's own
/// static and cannot be set in a test, so this pins the two halves a test can reach: the compiled
/// MCM defaults on a fresh <c>TaomSettings</c>, and the no-MCM fallback that returns the validated
/// JSON value.
/// </summary>
[TestClass]
public class CombatMechanicsSettingsProviderTests
{
    private CombatMechanicsConfig _config = null!;
    private CombatMechanicsSettingsProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new CombatMechanicsConfig();
        var provider = Substitute.For<ICombatMechanicsConfigProvider>();
        provider.GetConfig().Returns(_config);
        _sut = new CombatMechanicsSettingsProvider(provider);
    }

    [TestMethod]
    public void McmDefaults_MatchTheCompiledJsonDefaults()
    {
        // MCM persists per property, so a changed default reaches fresh installs only; the JSON
        // and the compiled TaomSettings must agree or the two surfaces drift on first launch.
        var settings = new TaomSettings();
        var json = new ChargeKnockdownConfig();

        Assert.AreEqual((int)json.AutoKnockdownWeightRatio, settings.ChargeAutoKnockdownWeightRatio);
        Assert.AreEqual(json.NeutralWeightRatio, settings.ChargeNeutralWeightRatio, 0.0001f);
        Assert.AreEqual(json.HorseChargePenetration, settings.ChargeHorsePenetration, 0.0001f);
        Assert.AreEqual(json.MinPenetrationFactor, settings.ChargeMinPenetrationFactor, 0.0001f);
        Assert.IsTrue(settings.EnableCultureChargeDamage);
    }

    [TestMethod]
    public void NoMcm_ChargeNeutralWeightRatio_FallsBackToJson()
    {
        _config.ChargeKnockdown.NeutralWeightRatio = 4.5f;

        Assert.AreEqual(4.5f, _sut.ChargeNeutralWeightRatio, 0.0001f);
    }

    [TestMethod]
    public void NoMcm_ChargeHorsePenetration_FallsBackToJson()
    {
        _config.ChargeKnockdown.HorseChargePenetration = 0.55f;

        Assert.AreEqual(0.55f, _sut.ChargeHorsePenetration, 0.0001f);
    }

    [TestMethod]
    public void NoMcm_ChargeMinPenetrationFactor_FallsBackToJson()
    {
        _config.ChargeKnockdown.MinPenetrationFactor = 0.75f;

        Assert.AreEqual(0.75f, _sut.ChargeMinPenetrationFactor, 0.0001f);
    }

    [TestMethod]
    public void NoMcm_ChargeAutoKnockdownWeightRatio_FallsBackToJson()
    {
        _config.ChargeKnockdown.AutoKnockdownWeightRatio = 7f;

        Assert.AreEqual(7f, _sut.ChargeAutoKnockdownWeightRatio, 0.0001f);
    }

    [TestMethod]
    public void NoMcm_CultureChargeDamageEnabled_FallsBackToJson()
    {
        _config.ChargeDamage.Enabled = false;

        Assert.IsFalse(_sut.CultureChargeDamageEnabled);
    }

    [TestMethod]
    public void AutoFloor_DerivesFromTheNeutralRatio()
    {
        // The invariant both surfaces enforce: auto >= neutral, so Branch A never fires on a
        // charge Branch B keeps at parity. Exposed as a pure static so the clamp is testable
        // without MCM.
        Assert.AreEqual(6, CombatMechanicsSettingsProvider.AutoKnockdownFloor(6f));
        Assert.AreEqual(3, CombatMechanicsSettingsProvider.AutoKnockdownFloor(2.5f));
        Assert.AreEqual(2, CombatMechanicsSettingsProvider.AutoKnockdownFloor(1f));
        Assert.AreEqual(2, CombatMechanicsSettingsProvider.AutoKnockdownFloor(float.NaN));
    }
}
