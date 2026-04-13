using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Tests for tier resolution on ISpecialResourceService.
/// </summary>
[TestClass]
public class SpecialResourceTierServiceTests
{
    private ISpecialResourceConfigProvider _config;
    private ISpecialResourceStorageService _storage;
    private IModLogger _logger;
    private SpecialResourceService _service;

    private static readonly IReadOnlyList<ResourceTier> ThreeTiers = new[]
    {
        new ResourceTier(level: 1, name: "Apprentice Miner",    threshold: 100f, description: "Desc 1"),
        new ResourceTier(level: 2, name: "Journeyman Smith",    threshold: 250f, description: "Desc 2"),
        new ResourceTier(level: 3, name: "Master of Treasury",  threshold: 400f, description: "Desc 3"),
    };

    private static SpecialResource MakeGemsResource(IReadOnlyList<ResourceTier> tiers = null) =>
        new(
            id: "gems",
            kingdomIds: new[] { "erebor" },
            cultureIds: new[] { "erebor" },
            displayName: "Gems",
            iconSpriteName: "taom_gems_icon",
            cap: 600f,
            startingAmount: 40f,
            dailyPerTown: 1.0f,
            perBattleVictoryBase: 5f,
            perRaid: 3f,
            perSiegeVictory: 10f,
            perPrisoner: 0f,
            tierThresholds: tiers);

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _logger = Substitute.For<IModLogger>();
        _service = new SpecialResourceService(_config, _storage, _logger);
    }

    // ── GetCurrentTier ──

    [TestMethod]
    public void GetCurrentTier_NoTiersDefined_ReturnsNull()
    {
        // Arrange
        var resource = MakeGemsResource(tiers: null);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(300f);

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentTier_EmptyTiersList_ReturnsNull()
    {
        // Arrange
        var resource = MakeGemsResource(tiers: new ResourceTier[0]);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(300f);

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentTier_BelowAllThresholds_ReturnsNull()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(50f); // below tier 1 (100)

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentTier_ExactlyAtTierOneThreshold_ReturnsTierOne()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(100f);

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Level);
        Assert.AreEqual("Apprentice Miner", result.Name);
    }

    [TestMethod]
    public void GetCurrentTier_BetweenTierOneAndTwo_ReturnsTierOne()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(180f); // >= 100, < 250

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(1, result.Level);
    }

    [TestMethod]
    public void GetCurrentTier_ExactlyAtTierTwoThreshold_ReturnsTierTwo()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(250f);

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(2, result.Level);
        Assert.AreEqual("Journeyman Smith", result.Name);
    }

    [TestMethod]
    public void GetCurrentTier_ExactlyAtTierThreeThreshold_ReturnsTierThree()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(400f);

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(3, result.Level);
        Assert.AreEqual("Master of Treasury", result.Name);
    }

    [TestMethod]
    public void GetCurrentTier_AboveMaxThreshold_ReturnsHighestTier()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(599f); // above max tier threshold

        // Act
        var result = _service.GetCurrentTier("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(3, result.Level);
    }

    [TestMethod]
    public void GetCurrentTier_NoResourceResolved_ReturnsNull()
    {
        // Arrange
        _config.GetByKingdomId(Arg.Any<string>()).Returns((SpecialResource)null);
        _config.GetByCultureId(Arg.Any<string>()).Returns((SpecialResource)null);

        // Act
        var result = _service.GetCurrentTier("hero1", "gondor", "gondor");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetCurrentTier_UsesStoredAmount_ForResolution()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByCultureId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(350f); // between tier 2 (250) and tier 3 (400)

        // Act — resolve via culture fallback (no kingdom)
        var result = _service.GetCurrentTier("hero1", null, "erebor");

        // Assert
        Assert.AreEqual(2, result.Level);
    }

    // ── GetCurrentTierLevel ──

    [TestMethod]
    public void GetCurrentTierLevel_BelowAllThresholds_ReturnsZero()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(0f);

        // Act
        var result = _service.GetCurrentTierLevel("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetCurrentTierLevel_AtTierTwo_ReturnsTwo()
    {
        // Arrange
        var resource = MakeGemsResource(ThreeTiers);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(260f);

        // Act
        var result = _service.GetCurrentTierLevel("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public void GetCurrentTierLevel_NoResourceResolved_ReturnsZero()
    {
        // Arrange
        _config.GetByKingdomId(Arg.Any<string>()).Returns((SpecialResource)null);
        _config.GetByCultureId(Arg.Any<string>()).Returns((SpecialResource)null);

        // Act
        var result = _service.GetCurrentTierLevel("hero1", "gondor", "gondor");

        // Assert
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetCurrentTierLevel_NoTiersDefined_ReturnsZero()
    {
        // Arrange
        var resource = MakeGemsResource(tiers: null);
        _config.GetByKingdomId("erebor").Returns(resource);
        _storage.Get("hero1", "gems").Returns(500f);

        // Act
        var result = _service.GetCurrentTierLevel("hero1", "erebor", null);

        // Assert
        Assert.AreEqual(0, result);
    }
}
