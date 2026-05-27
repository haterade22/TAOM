using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

[TestClass]
public class BanditScalingServiceTests
{
    private IBanditScalingSettingsProvider _settings = null!;
    private BanditScalingService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBanditScalingSettingsProvider>();
        _settings.IsEnabled.Returns(true);
        _settings.DensityCurve.Returns(1.5f);
        _settings.PartySizeCurve.Returns(1.5f);
        _settings.BossFightCurve.Returns(1.5f);
        _settings.MaxHideoutsPerFactionCap.Returns(15);
        _settings.MaxPartiesPerHideoutCap.Returns(5);
        _settings.MinPartiesToInfest.Returns(2);

        _sut = new BanditScalingService(_settings);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressZero_ReturnsOne()
    {
        var result = _sut.GetDensityMultiplier(0f);
        Assert.AreEqual(1f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressOne_ReturnsOnePlusCurve()
    {
        // 1 + 1.5 * 1 = 2.5
        var result = _sut.GetDensityMultiplier(1f);
        Assert.AreEqual(2.5f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressHalf_ReturnsLinear()
    {
        // 1 + 1.5 * 0.5 = 1.75
        var result = _sut.GetDensityMultiplier(0.5f);
        Assert.AreEqual(1.75f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressNaN_TreatsAsZero()
    {
        var result = _sut.GetDensityMultiplier(float.NaN);
        Assert.AreEqual(1f, result, 0.001f, "NaN playerProgress must not propagate to multiplier; should floor to 1.0");
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressInfinity_TreatsAsZero()
    {
        var result = _sut.GetDensityMultiplier(float.PositiveInfinity);
        Assert.AreEqual(1f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressNegative_ClampsToZero()
    {
        var result = _sut.GetDensityMultiplier(-5f);
        Assert.AreEqual(1f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_PlayerProgressAboveOne_ClampsToOne()
    {
        var result = _sut.GetDensityMultiplier(5f);
        Assert.AreEqual(2.5f, result, 0.001f);
    }

    [TestMethod]
    public void GetDensityMultiplier_CurveNaN_TreatsAsZero()
    {
        _settings.DensityCurve.Returns(float.NaN);
        var result = _sut.GetDensityMultiplier(1f);
        Assert.AreEqual(1f, result, 0.001f, "NaN curve must not propagate; should floor multiplier to 1.0");
    }

    [TestMethod]
    public void GetDensityMultiplier_CurveNegative_TreatsAsZero()
    {
        _settings.DensityCurve.Returns(-0.5f);
        var result = _sut.GetDensityMultiplier(1f);
        Assert.AreEqual(1f, result, 0.001f, "Negative curve must not reduce bandits below vanilla floor");
    }

    [TestMethod]
    public void GetPartySizeMultiplier_UsesPartySizeCurveNotDensity()
    {
        _settings.PartySizeCurve.Returns(3.0f);
        _settings.DensityCurve.Returns(0.5f);

        var result = _sut.GetPartySizeMultiplier(1f);
        Assert.AreEqual(4.0f, result, 0.001f, "Should use PartySizeCurve (3.0) not DensityCurve (0.5)");
    }

    [TestMethod]
    public void GetBossFightMultiplier_UsesBossFightCurveNotDensity()
    {
        _settings.BossFightCurve.Returns(2.0f);
        _settings.DensityCurve.Returns(0.5f);

        var result = _sut.GetBossFightMultiplier(1f);
        Assert.AreEqual(3.0f, result, 0.001f);
    }

    [TestMethod]
    public void IsEnabled_DelegatesToSettings()
    {
        _settings.IsEnabled.Returns(false);
        Assert.IsFalse(_sut.IsEnabled);

        _settings.IsEnabled.Returns(true);
        Assert.IsTrue(_sut.IsEnabled);
    }

    [TestMethod]
    public void MaxHideoutsPerFactionCap_DelegatesToSettings()
    {
        _settings.MaxHideoutsPerFactionCap.Returns(42);
        Assert.AreEqual(42, _sut.MaxHideoutsPerFactionCap);
    }

    [TestMethod]
    public void MaxPartiesPerHideoutCap_DelegatesToSettings()
    {
        _settings.MaxPartiesPerHideoutCap.Returns(7);
        Assert.AreEqual(7, _sut.MaxPartiesPerHideoutCap);
    }

    [TestMethod]
    public void MinPartiesToInfest_DelegatesToSettings()
    {
        _settings.MinPartiesToInfest.Returns(3);
        Assert.AreEqual(3, _sut.MinPartiesToInfest);
    }

    [TestMethod]
    public void GetMultiplier_IsMonotonicInPlayerProgress()
    {
        var at0 = _sut.GetDensityMultiplier(0f);
        var at25 = _sut.GetDensityMultiplier(0.25f);
        var at50 = _sut.GetDensityMultiplier(0.5f);
        var at75 = _sut.GetDensityMultiplier(0.75f);
        var at100 = _sut.GetDensityMultiplier(1f);

        Assert.IsTrue(at0 <= at25, $"{at0} should be <= {at25}");
        Assert.IsTrue(at25 <= at50, $"{at25} should be <= {at50}");
        Assert.IsTrue(at50 <= at75, $"{at50} should be <= {at75}");
        Assert.IsTrue(at75 <= at100, $"{at75} should be <= {at100}");
    }

    [TestMethod]
    public void GetMultiplier_AlwaysAtLeastOne_VanillaFloor()
    {
        // Even with zero curve, multiplier must be 1.0 (vanilla floor)
        _settings.DensityCurve.Returns(0f);

        Assert.AreEqual(1f, _sut.GetDensityMultiplier(0f), 0.001f);
        Assert.AreEqual(1f, _sut.GetDensityMultiplier(1f), 0.001f);
    }
}
