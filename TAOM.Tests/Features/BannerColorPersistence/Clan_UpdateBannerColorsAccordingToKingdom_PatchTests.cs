using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class Clan_UpdateBannerColorsAccordingToKingdom_PatchTests
{
    private IBannerColorService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IBannerColorService>();
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(_service);
    }

    [TestMethod]
    public void Prefix_DriftGuardEnabled_ReturnsFalseSkippingOriginal()
    {
        // Arrange
        _service.IsDriftGuardEnabled().Returns(true);

        // Act
        bool result = Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Prefix_DriftGuardDisabled_ReturnsTrueAllowingOriginal()
    {
        // Arrange
        _service.IsDriftGuardEnabled().Returns(false);

        // Act
        bool result = Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Prefix_ServiceIsNull_ReturnsFalseSkippingOriginal()
    {
        // Arrange — null service simulates uninitialized state; ?? true means skip original
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(null);

        // Act
        bool result = Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix();

        // Assert
        Assert.IsFalse(result);
    }
}
