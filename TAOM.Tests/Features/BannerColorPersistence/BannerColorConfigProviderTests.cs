using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class BannerColorConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private BannerColorConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ConfigPath.Returns(_tempDir + Path.DirectorySeparatorChar);
        _logger = Substitute.For<IModLogger>();

        _sut = new BannerColorConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void GetConfig_FileNotFound_ReturnsDefaults()
    {
        // Act
        var config = _sut.GetConfig();

        // Assert
        Assert.IsTrue(config.EnableColorPersistence);
        Assert.IsTrue(config.EnableDriftGuard);
        Assert.IsTrue(config.EnableBannerPaste);
        Assert.IsTrue(config.EnableUniqueSecondaryColor);
        Assert.IsFalse(config.EnableLayerLimitTranspiler); // off by default since v1.4.7 (engine natively unlimited)
    }

    [TestMethod]
    public void GetConfig_ValidJson_EnableColorPersistence_DeserializesCorrectly()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempDir, "banner_color_config.json"),
            "{ \"EnableColorPersistence\": false }");

        // Act
        var config = _sut.GetConfig();

        // Assert
        Assert.IsFalse(config.EnableColorPersistence);
        Assert.IsTrue(config.EnableDriftGuard);
        Assert.IsTrue(config.EnableBannerPaste);
        Assert.IsTrue(config.EnableUniqueSecondaryColor);
        Assert.IsFalse(config.EnableLayerLimitTranspiler); // JSON omits it -> C# default (false since v1.4.7)
    }

    [TestMethod]
    public void GetConfig_InvalidJson_ReturnsDefaults()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempDir, "banner_color_config.json"),
            "{ not valid json {{{");

        // Act
        var config = _sut.GetConfig();

        // Assert
        Assert.IsTrue(config.EnableColorPersistence);
        Assert.IsTrue(config.EnableDriftGuard);
        Assert.IsTrue(config.EnableBannerPaste);
        Assert.IsTrue(config.EnableUniqueSecondaryColor);
        Assert.IsFalse(config.EnableLayerLimitTranspiler); // invalid JSON -> C# default (false since v1.4.7)
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameInstance()
    {
        // Arrange
        File.WriteAllText(
            Path.Combine(_tempDir, "banner_color_config.json"),
            "{ \"EnableColorPersistence\": false }");

        // Act
        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        // Assert
        Assert.AreSame(first, second);
    }
}
