using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// Per-culture charge damage (#610). The multiplier lands on a mount's <c>MountChargeDamage</c>
/// after base and the career passives; 1.0 is "no change" for every path the boundary can hand
/// in: a null culture (a riderless mount, a mount whose Character has no culture), an id the map
/// does not list, the toggle off.
/// </summary>
[TestClass]
public class ChargeDamageServiceTests
{
    private CombatMechanicsConfig _config = null!;
    private ICombatMechanicsSettingsProvider _settings = null!;
    private ChargeDamageService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new CombatMechanicsConfig();
        _config.ChargeDamage.CultureMultipliers = new Dictionary<string, float>
        {
            ["vlandia"] = 1.5f,
            ["gondor"] = 1.3f,
        };
        var configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        configProvider.GetConfig().Returns(_config);
        _settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        _settings.CultureChargeDamageEnabled.Returns(true);
        _sut = new ChargeDamageService(configProvider, _settings);
    }

    [TestMethod]
    public void Multiplier_ListedCulture_ReturnsItsFactor()
    {
        Assert.AreEqual(1.5f, _sut.Multiplier("vlandia"), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_IsCaseInsensitive()
    {
        Assert.AreEqual(1.3f, _sut.Multiplier("Gondor"), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_UnlistedCulture_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.Multiplier("erebor"), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_NullOrEmptyCulture_ReturnsOne()
    {
        Assert.AreEqual(1f, _sut.Multiplier(null), 0.0001f);
        Assert.AreEqual(1f, _sut.Multiplier(""), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_Disabled_ReturnsOneForAListedCulture()
    {
        _settings.CultureChargeDamageEnabled.Returns(false);

        Assert.AreEqual(1f, _sut.Multiplier("vlandia"), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_NullMap_ReturnsOne()
    {
        _config.ChargeDamage.CultureMultipliers = null!;
        var configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        configProvider.GetConfig().Returns(_config);

        var sut = new ChargeDamageService(configProvider, _settings);

        Assert.AreEqual(1f, sut.Multiplier("vlandia"), 0.0001f);
    }

    [TestMethod]
    public void Multiplier_NonFiniteFactorInMap_ReturnsOne()
    {
        // The provider rejects these at load; the service guards its own read as well so a
        // hand-built config in a test or a future loader cannot poison an engine float.
        _config.ChargeDamage.CultureMultipliers["vlandia"] = float.NaN;
        var configProvider = Substitute.For<ICombatMechanicsConfigProvider>();
        configProvider.GetConfig().Returns(_config);

        var sut = new ChargeDamageService(configProvider, _settings);

        Assert.AreEqual(1f, sut.Multiplier("vlandia"), 0.0001f);
    }
}
