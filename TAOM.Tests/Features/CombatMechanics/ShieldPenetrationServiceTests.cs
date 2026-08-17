using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class ShieldPenetrationServiceTests
{
    // Mirrors the service constants (installed 1.4.6 TaleWorlds.Core.WeaponFlags — spec
    // the combat-mechanics spec "Engine ground truth").
    private const ulong CanPenetrateShieldFlag = 0x20000UL;
    private const ulong MultiplePenetrationFlag = 0x40000000UL;

    // Any bit outside the two penetration bits — the service must never clear it.
    private const ulong UnrelatedFlag = 0x1UL;

    private const string ListedItemId = "taom_test_javelin";
    private const string ListedWeaponClass = "Javelin";

    private ICombatMechanicsConfigProvider _configProvider;
    private ICombatMechanicsSettingsProvider _settings;
    private CombatMechanicsConfig _config;
    private ShieldPenetrationService _sut;

    [TestInitialize]
    public void SetUp()
    {
        _configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();

        // A fully ACTIVE fixture, stated explicitly. The shipped defaults are all-off since
        // 2026-08-17 (vanilla javelin parity), so every CONFIG field the service reads is set here
        // rather than inherited — otherwise a default flip silently rewrites what these tests mean.
        //
        // Note the service never reads config.Enabled: the ctor snapshots only the two grant lists,
        // AddMultiplePenetration and the two correction fields. The on/off gate reaches the service
        // as _settings.ShieldPenetrationEnabled (CombatMechanicsSettingsProvider folds config.Enabled
        // into that as the MCM fallback), which is why the Returns(true) below is load-bearing and
        // must not be removed on the assumption that a config flag covers it.
        _config = new CombatMechanicsConfig();
        _config.ShieldPenetration.ItemIds.Clear();
        _config.ShieldPenetration.ItemIds.Add(ListedItemId);
        _config.ShieldPenetration.WeaponClasses.Clear();
        _config.ShieldPenetration.WeaponClasses.Add(ListedWeaponClass);
        _config.ShieldPenetration.RuntimeShieldDamageCorrectionEnabled = true;
        _configProvider.GetConfig().Returns(_config);
        _settings.ShieldPenetrationEnabled.Returns(true);

        _sut = CreateSut();
    }

    // Config is snapshotted in the service constructor (hot-path precompute); tests that
    // mutate _config must rebuild. Settings are read per call — no rebuild needed for those.
    private ShieldPenetrationService CreateSut()
    {
        return new ShieldPenetrationService(_configProvider, _settings);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_ListedItemId_GainsCanPenetrateShield()
    {
        var result = _sut.ApplyPenetrationFlags(ListedItemId, null, 0UL);

        Assert.AreEqual(CanPenetrateShieldFlag, result & CanPenetrateShieldFlag);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_ListedWeaponClass_GainsCanPenetrateShield()
    {
        var result = _sut.ApplyPenetrationFlags(null, ListedWeaponClass, 0UL);

        Assert.AreEqual(CanPenetrateShieldFlag, result & CanPenetrateShieldFlag);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_AddMultiplePenetrationTrue_OrsInBothFlags()
    {
        _config.ShieldPenetration.AddMultiplePenetration = true;
        _sut = CreateSut();

        var result = _sut.ApplyPenetrationFlags(ListedItemId, null, 0UL);

        Assert.AreEqual(CanPenetrateShieldFlag | MultiplePenetrationFlag, result);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_AddMultiplePenetrationFalse_OrsInOnlyCanPenetrateShield()
    {
        _config.ShieldPenetration.AddMultiplePenetration = false;
        _sut = CreateSut();

        var result = _sut.ApplyPenetrationFlags(ListedItemId, null, 0UL);

        Assert.AreEqual(CanPenetrateShieldFlag, result);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_PreExistingUnrelatedBit_Preserved()
    {
        _config.ShieldPenetration.AddMultiplePenetration = true;
        _sut = CreateSut();

        var result = _sut.ApplyPenetrationFlags(ListedItemId, null, UnrelatedFlag);

        Assert.AreEqual(UnrelatedFlag | CanPenetrateShieldFlag | MultiplePenetrationFlag, result);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_UnlistedItemAndClass_ReturnsUnchanged()
    {
        var result = _sut.ApplyPenetrationFlags("unlisted_item", "Bow", UnrelatedFlag);

        Assert.AreEqual(UnrelatedFlag, result);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_MechanicDisabled_ReturnsUnchanged()
    {
        _settings.ShieldPenetrationEnabled.Returns(false);

        var result = _sut.ApplyPenetrationFlags(ListedItemId, ListedWeaponClass, UnrelatedFlag);

        Assert.AreEqual(UnrelatedFlag, result);
    }

    [TestMethod]
    public void ApplyPenetrationFlags_NullItemIdAndClass_ReturnsUnchanged()
    {
        var result = _sut.ApplyPenetrationFlags(null, null, UnrelatedFlag);

        Assert.AreEqual(UnrelatedFlag, result);
    }

    [TestMethod]
    public void ApplyRuntimeFlagCorrection_GrantedNoStaticFlagEnabled_DividesByDivisor()
    {
        // Default divisor 0.3 → 30f / 0.3f ≈ 100f (float rounding → delta assert).
        var result = _sut.ApplyRuntimeFlagCorrection(ListedItemId, null, false, 30f);

        Assert.AreEqual(100f, result, 0.001f);
    }

    [TestMethod]
    public void ApplyRuntimeFlagCorrection_StaticFlagPresent_ReturnsBaseUnchanged()
    {
        var result = _sut.ApplyRuntimeFlagCorrection(ListedItemId, null, true, 30f);

        Assert.AreEqual(30f, result);
    }

    [TestMethod]
    public void ApplyRuntimeFlagCorrection_NotGranted_ReturnsBaseUnchanged()
    {
        var result = _sut.ApplyRuntimeFlagCorrection("unlisted_item", "Bow", false, 30f);

        Assert.AreEqual(30f, result);
    }

    [TestMethod]
    public void ApplyRuntimeFlagCorrection_CorrectionToggleOff_ReturnsBaseUnchanged()
    {
        _config.ShieldPenetration.RuntimeShieldDamageCorrectionEnabled = false;
        _sut = CreateSut();

        var result = _sut.ApplyRuntimeFlagCorrection(ListedItemId, null, false, 30f);

        Assert.AreEqual(30f, result);
    }

    [TestMethod]
    public void ApplyRuntimeFlagCorrection_MechanicDisabled_ReturnsBaseUnchanged()
    {
        _settings.ShieldPenetrationEnabled.Returns(false);

        var result = _sut.ApplyRuntimeFlagCorrection(ListedItemId, null, false, 30f);

        Assert.AreEqual(30f, result);
    }

    [TestMethod]
    public void ShippedDefaults_MechanicToggledOn_StillGrantsNothingAndLeavesShieldDamageAlone()
    {
        // MCM persists per profile and merges OVER the JSON (CombatMechanicsSettingsProvider), so a
        // player who saved "Shield Penetration" as ON before 2026-08-17 keeps that toggle on after
        // the default flipped. This pins the second line of defence: the shipped lists are EMPTY, so
        // IsGranted matches nothing and the mechanic is inert even with the toggle forced on. A
        // stale MCM value alone cannot bring back the 3.33x shield-damage multiplier.
        _config = new CombatMechanicsConfig();
        _configProvider.GetConfig().Returns(_config);
        _settings.ShieldPenetrationEnabled.Returns(true);
        _sut = CreateSut();

        var flags = _sut.ApplyPenetrationFlags("wm_gundabad_javelin_a01", "Javelin", UnrelatedFlag);
        var shieldDamage = _sut.ApplyRuntimeFlagCorrection("wm_gundabad_javelin_a01", "Javelin", false, 30f);

        Assert.AreEqual(UnrelatedFlag, flags);
        Assert.AreEqual(0UL, flags & CanPenetrateShieldFlag);
        Assert.AreEqual(0UL, flags & MultiplePenetrationFlag);
        Assert.AreEqual(30f, shieldDamage);
    }
}
