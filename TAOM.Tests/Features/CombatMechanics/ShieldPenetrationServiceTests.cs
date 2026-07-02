using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

[TestClass]
public class ShieldPenetrationServiceTests
{
    // Mirrors the service constants (installed 1.4.6 TaleWorlds.Core.WeaponFlags — spec
    // docs/reviews/adopt-tor-combat-mechanics-2026-07-02.md "Engine ground truth").
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

        _config = new CombatMechanicsConfig();
        _config.ShieldPenetration.ItemIds.Clear();
        _config.ShieldPenetration.ItemIds.Add(ListedItemId);
        _config.ShieldPenetration.WeaponClasses.Clear();
        _config.ShieldPenetration.WeaponClasses.Add(ListedWeaponClass);
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
}
