using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.WandererAllegiance;

namespace TAOM.Tests.Features.WandererAllegiance;

[TestClass]
public class WandererAllegianceConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private WandererAllegianceConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_WandererAllegiance_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "wanderer_allegiance");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new WandererAllegianceConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "wanderer_allegiance_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidFile_ParsesAllFields()
    {
        WriteConfig(@"{ ""enabled"": false, ""scope"": ""NamedCompanionsOnly"" }");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeNamedCompanionsOnly, config.Scope);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_MissingFile_WarnsAndUsesDefaults()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_LogsErrorAndUsesDefaults()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_KeepsCompiledDefaults()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
    }

    [TestMethod]
    public void GetConfig_JsonNullLiteral_FallsBackToDefaults()
    {
        WriteConfig("null");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
    }

    [TestMethod]
    public void GetConfig_UnknownScope_WarnsRevertsToDefaultAndEmitsSummary()
    {
        // The consumer branches on this string; a typo must revert visibly, never pick a mode.
        WriteConfig(@"{ ""enabled"": true, ""scope"": ""NamedOnly"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("scope='NamedOnly'")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_EmptyScope_WarnsAndRevertsToDefault()
    {
        WriteConfig(@"{ ""scope"": """" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("scope=''")));
    }

    [TestMethod]
    public void GetConfig_JsonNullScope_WarnsAndRevertsToDefault()
    {
        WriteConfig(@"{ ""scope"": null }");

        var config = _sut.GetConfig();

        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("reverting")));
    }

    [TestMethod]
    public void GetConfig_ScopeCaseInsensitive_NormalizesToConstant()
    {
        WriteConfig(@"{ ""scope"": ""namedcompanionsonly"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(WandererAllegianceConfig.ScopeNamedCompanionsOnly, config.Scope);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""enabled"": true }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_ShippedFile_ParsesEnabledAllWanderers()
    {
        // Pins the file actually shipped in Main/_Module/ModuleData, not just a synthetic one.
        var shipped = Path.GetFullPath(Path.Combine(
            System.AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData\wanderer_allegiance\wanderer_allegiance_config.json"));

        Assert.IsTrue(File.Exists(shipped), $"Shipped config missing at {shipped}");
        WriteConfig(File.ReadAllText(shipped));

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(WandererAllegianceConfig.ScopeAllWanderers, config.Scope);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
