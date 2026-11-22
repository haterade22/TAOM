using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class BannerColorServiceTests
{
    private IBannerColorConfigProvider _configProvider;
    private BannerColorConfig _config;
    private ICoopSessionProvider _coop;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IBannerColorConfigProvider>();
        _config = new BannerColorConfig();
        _configProvider.GetConfig().Returns(_config);
        _coop = Substitute.For<ICoopSessionProvider>();
    }

    private BannerColorService CreateSut() => new BannerColorService(_configProvider, _coop);

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

    // --- The load repaint on a co-op client -------------------------------------------------
    // Clan.PreAfterLoad repaints every kingdom clan's banner in place from Kingdom.PrimaryBannerColor. A co-op
    // client's join save already holds the server's banners, and BannerInjection's restore runs only on the
    // authority, so only a client inside that load skips the repaint.

    [TestMethod]
    public void ShouldSkipLoadRepaint_CoopClientInsideClanLoad_True()
    {
        _coop.IsCoopClient.Returns(true);

        Assert.IsTrue(CreateSut().ShouldSkipLoadRepaint(insideClanLoad: true));
    }

    [TestMethod]
    public void ShouldSkipLoadRepaint_CoopClientOutsideALoad_False()
    {
        _coop.IsCoopClient.Returns(true);

        Assert.IsFalse(CreateSut().ShouldSkipLoadRepaint(insideClanLoad: false), "a kingdom change later still repaints");
    }

    [TestMethod]
    public void ShouldSkipLoadRepaint_SoloOrServerInsideClanLoad_False()
    {
        _coop.IsCoopClient.Returns(false);

        Assert.IsFalse(CreateSut().ShouldSkipLoadRepaint(insideClanLoad: true), "the authority repaints, then restores");
    }

    [TestMethod]
    public void ShouldSkipLoadRepaint_OutsideALoad_DoesNotProbeTheSession()
    {
        CreateSut().ShouldSkipLoadRepaint(insideClanLoad: false);

        _ = _coop.DidNotReceive().IsCoopClient;
    }
}
