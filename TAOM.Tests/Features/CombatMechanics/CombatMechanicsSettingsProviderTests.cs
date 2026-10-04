using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// The MCM-over-JSON merge for the combat knobs (#610). <c>TaomSettings.Instance</c> is MCM's own
/// static and is null in a test host, so the default constructor pins the no-MCM fallback to the
/// validated JSON; the internal constructor hands the provider a fresh <c>TaomSettings</c> and pins
/// the merged path: MCM values win, are read through the cached reference, and follow live edits.
/// The compiled MCM defaults are pinned against the JSON defaults.
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

    private static CombatMechanicsSettingsProvider WarmProviderOn(TaomSettings mcm)
    {
        var configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        configProvider.GetConfig().Returns(new CombatMechanicsConfig());
        var sut = new CombatMechanicsSettingsProvider(configProvider, mcm);
        foreach (var p in typeof(ICombatMechanicsSettingsProvider).GetProperties())
            _ = p.GetValue(sut);
        return sut;
    }

    // The provider caches the MCM reference and reads THROUGH it: MCM edits its one registered
    // TaomSettings in place, so a setting changed after every getter has been read once must reach
    // its own getter. Every expected value differs from the compiled MCM default, so a getter that
    // snapshots its first read, or reads the JSON instead of the settings, fails its row. The auto
    // ratio of 10 passes its clamp because the live neutral ratio stays 6.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings_SoLiveMcmEditsApply()
    {
        var rows = new (Action<TaomSettings> Edit, Func<CombatMechanicsSettingsProvider, object> Get, object Expected)[]
        {
            (s => s.EnableSkillCrushThrough = false, p => p.SkillCrushThroughEnabled, false),
            (s => s.EnableMonsterCrushThrough = false, p => p.MonsterCrushThroughEnabled, false),
            (s => s.EnableOrcShieldCrush = false, p => p.OrcShieldCrushEnabled, false),
            (s => s.EnableCreatureCleave = false, p => p.CreatureCleaveEnabled, false),
            (s => s.EnableCreatureUnstoppable = false, p => p.CreatureUnstoppableEnabled, false),
            (s => s.EnableChargeKnockdown = false, p => p.ChargeKnockdownEnabled, false),
            (s => s.EnableShieldPenetration = true, p => p.ShieldPenetrationEnabled, true),
            (s => s.EnableRaceCombatModifiers = false, p => p.RaceCombatModifiersEnabled, false),
            (s => s.EnableCultureChargeDamage = false, p => p.CultureChargeDamageEnabled, false),
            (s => s.CrushThroughMaxChance = 0.25f, p => p.CrushThroughMaxChance, 0.25f),
            (s => s.ChargeAutoKnockdownWeightRatio = 10, p => p.ChargeAutoKnockdownWeightRatio, 10f),
            (s => s.ChargeNeutralWeightRatio = 4f, p => p.ChargeNeutralWeightRatio, 4f),
            (s => s.ChargeHorsePenetration = 0.6f, p => p.ChargeHorsePenetration, 0.6f),
            (s => s.ChargeMinPenetrationFactor = 0.5f, p => p.ChargeMinPenetrationFactor, 0.5f),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var mcm = new TaomSettings();
            var sut = WarmProviderOn(mcm);

            rows[i].Edit(mcm);

            Assert.AreEqual(rows[i].Expected, rows[i].Get(sut), "row " + i);
        }
    }

    // The auto ratio's floor follows the LIVE neutral ratio (#610) through the cached settings too:
    // with the auto slider at its default 6, raising neutral to 12 after warm-up lifts auto to 12. A
    // floor that read the JSON neutral (6) or a snapshot of the first read would return 6.
    [TestMethod]
    public void AutoRatioFloor_FollowsALiveNeutralEdit_ThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = WarmProviderOn(mcm);

        mcm.ChargeNeutralWeightRatio = 12f;

        Assert.AreEqual(12f, sut.ChargeAutoKnockdownWeightRatio, 0.0001f);
    }

    [TestMethod]
    public void MasterToggleOff_EveryEnabledGetterReportsDisabled()
    {
        var mcm = new TaomSettings();
        var sut = WarmProviderOn(mcm);

        mcm.EnableCombatMechanics = false;

        AssertEveryEnabledGetterFalse(sut);
    }

    // A pin, green before and after the cache: without MCM every toggle and the max chance come
    // from the validated JSON, and the JSON master toggle folds into every enabled getter.
    [TestMethod]
    public void NoMcm_TogglesAndMaxChance_FallBackToJson()
    {
        _config.Enabled = true;
        _config.ShieldPenetration.Enabled = true;
        _config.CrushThrough.SkillBasedEnabled = false;
        _config.Creatures.CleaveEnabled = false;
        _config.CrushThrough.MaxSkillChance = 0.3f;

        Assert.IsTrue(_sut.ShieldPenetrationEnabled);
        Assert.IsFalse(_sut.SkillCrushThroughEnabled);
        Assert.IsFalse(_sut.CreatureCleaveEnabled);
        Assert.IsTrue(_sut.MonsterCrushThroughEnabled);
        Assert.IsTrue(_sut.RaceCombatModifiersEnabled);
        Assert.AreEqual(0.3f, _sut.CrushThroughMaxChance, 0.0001f);

        _config.Enabled = false;

        AssertEveryEnabledGetterFalse(_sut);
    }

    private static void AssertEveryEnabledGetterFalse(CombatMechanicsSettingsProvider sut)
    {
        Assert.IsFalse(sut.SkillCrushThroughEnabled, nameof(sut.SkillCrushThroughEnabled));
        Assert.IsFalse(sut.MonsterCrushThroughEnabled, nameof(sut.MonsterCrushThroughEnabled));
        Assert.IsFalse(sut.OrcShieldCrushEnabled, nameof(sut.OrcShieldCrushEnabled));
        Assert.IsFalse(sut.CreatureCleaveEnabled, nameof(sut.CreatureCleaveEnabled));
        Assert.IsFalse(sut.CreatureUnstoppableEnabled, nameof(sut.CreatureUnstoppableEnabled));
        Assert.IsFalse(sut.ChargeKnockdownEnabled, nameof(sut.ChargeKnockdownEnabled));
        Assert.IsFalse(sut.ShieldPenetrationEnabled, nameof(sut.ShieldPenetrationEnabled));
        Assert.IsFalse(sut.RaceCombatModifiersEnabled, nameof(sut.RaceCombatModifiersEnabled));
        Assert.IsFalse(sut.CultureChargeDamageEnabled, nameof(sut.CultureChargeDamageEnabled));
    }
}
