using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BannerColorPersistence;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class BannerColorServiceTests
{
    private IBannerColorConfigProvider _configProvider;
    private BannerColorConfig _config;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IBannerColorConfigProvider>();
        _config = new BannerColorConfig();
        _configProvider.GetConfig().Returns(_config);
    }

    private BannerColorService CreateSut() => new BannerColorService(_configProvider);

    [TestMethod]
    public void IsEnabled_ConfigTrue_ReturnsTrue()
    {
        // Arrange
        _config.EnableColorPersistence = true;
        var sut = CreateSut();

        // Act
        bool result = sut.IsEnabled();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsEnabled_ConfigFalse_ReturnsFalse()
    {
        // Arrange
        _config.EnableColorPersistence = false;
        var sut = CreateSut();

        // Act
        bool result = sut.IsEnabled();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseClanColor_HasCustomColorsAndEnabled_ReturnsTrue()
    {
        // Arrange
        _config.EnableColorPersistence = true;
        var info = new ClanColorInfo("clan_gondor_1", 0xFF123456, 0xFF654321);
        var sut = CreateSut();

        // Act
        bool result = sut.ShouldUseClanColor(info);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldUseClanColor_ColorIsZero_ReturnsFalse()
    {
        // Arrange
        _config.EnableColorPersistence = true;
        var info = new ClanColorInfo("clan_gondor_1", 0, 0xFF654321);
        var sut = CreateSut();

        // Act
        bool result = sut.ShouldUseClanColor(info);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldUseClanColor_ServiceDisabled_ReturnsFalse()
    {
        // Arrange
        _config.EnableColorPersistence = false;
        var info = new ClanColorInfo("clan_gondor_1", 0xFF123456, 0xFF654321);
        var sut = CreateSut();

        // Act
        bool result = sut.ShouldUseClanColor(info);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ApplyClanColors_SetsColor1AndColor2FromInfo()
    {
        // Arrange
        var info = new ClanColorInfo("clan_gondor_1", 0xFF112233, 0xFF445566);
        var sut = CreateSut();
        uint color1 = 0;
        uint color2 = 0;

        // Act
        sut.ApplyClanColors(ref color1, ref color2, info);

        // Assert
        Assert.AreEqual(0xFF112233u, color1);
        Assert.AreEqual(0xFF445566u, color2);
    }

    [TestMethod]
    public void GetUniqueIconColor_SameAsBackground_ReturnsInvertedRgb()
    {
        // Arrange
        var sut = CreateSut();
        uint color = 0xFF112233;

        // Act
        uint result = sut.GetUniqueIconColor(color, color);

        // Assert — RGB inverted, alpha preserved
        Assert.AreEqual(0xFFEEDDCCu, result);
        Assert.AreNotEqual(color, result);
    }

    [TestMethod]
    public void GetUniqueIconColor_SameAsBackground_PreservesAlphaChannel()
    {
        // Arrange
        var sut = CreateSut();
        uint color = 0x80AABBCC;

        // Act
        uint result = sut.GetUniqueIconColor(color, color);

        // Assert — alpha 0x80 preserved, RGB inverted
        Assert.AreEqual(0x80554433u, result);
    }

    [TestMethod]
    public void GetUniqueIconColor_DifferentFromBackground_ReturnsSameColor()
    {
        // Arrange
        var sut = CreateSut();
        uint background = 0xFF112233;
        uint icon = 0xFF445566;

        // Act
        uint result = sut.GetUniqueIconColor(background, icon);

        // Assert
        Assert.AreEqual(icon, result);
    }

    [TestMethod]
    public void IsDriftGuardEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableDriftGuard = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsDriftGuardEnabled());
    }

    [TestMethod]
    public void IsBannerPasteEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableBannerPaste = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsBannerPasteEnabled());
    }

    [TestMethod]
    public void IsUniqueSecondaryColorEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableUniqueSecondaryColor = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsUniqueSecondaryColorEnabled());
    }

    [TestMethod]
    public void IsLayerLimitTranspilerEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableLayerLimitTranspiler = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsLayerLimitTranspilerEnabled());
    }

    [TestMethod]
    public void IsAgentVisualColorsEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableAgentVisualColors = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsAgentVisualColorsEnabled());
    }

    [TestMethod]
    public void IsConversationTableauColorsEnabled_ReflectsConfig()
    {
        // Arrange
        _config.EnableConversationTableauColors = false;
        var sut = CreateSut();

        // Act & Assert
        Assert.IsFalse(sut.IsConversationTableauColorsEnabled());
    }
}
