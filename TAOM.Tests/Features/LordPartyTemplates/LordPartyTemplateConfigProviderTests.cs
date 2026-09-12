using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordPartyTemplates;

namespace TAOM.Tests.Features.LordPartyTemplates;

[TestClass]
public class LordPartyTemplateConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private LordPartyTemplateConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_LordPartyTemplates_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "lord_party_templates");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new LordPartyTemplateConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "lord_party_templates.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsNoOverridesAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.Overrides.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsNoOverridesAndLogsError()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.Overrides.Count);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetConfig_ValidJson_ParsesEveryOverride()
    {
        WriteConfig(@"{ ""overrides"": { ""lord_1_34"": ""kingdom_hero_party_gondor_faramir_template"", ""lord_1_17"": ""kingdom_hero_party_mordor_sauron_template"" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(2, config.Overrides.Count);
        Assert.AreEqual("kingdom_hero_party_gondor_faramir_template", config.Overrides["lord_1_34"]);
        Assert.AreEqual("kingdom_hero_party_mordor_sauron_template", config.Overrides["lord_1_17"]);
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullOverrides_ReturnsEmptyAndWarns()
    {
        WriteConfig(@"{ ""overrides"": null }");

        var config = _sut.GetConfig();

        Assert.IsNotNull(config.Overrides);
        Assert.AreEqual(0, config.Overrides.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("overrides")));
    }

    [TestMethod]
    public void GetConfig_BlankHeroId_IsDroppedAndWarns()
    {
        WriteConfig(@"{ ""overrides"": { ""  "": ""kingdom_hero_party_gondor_faramir_template"", ""lord_1_17"": ""kingdom_hero_party_mordor_sauron_template"" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.Overrides.Count);
        Assert.IsTrue(config.Overrides.ContainsKey("lord_1_17"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("hero id")));
    }

    [TestMethod]
    public void GetConfig_BlankTemplateId_IsDroppedAndWarns()
    {
        WriteConfig(@"{ ""overrides"": { ""lord_1_34"": """", ""lord_1_17"": ""kingdom_hero_party_mordor_sauron_template"" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.Overrides.Count);
        Assert.IsFalse(config.Overrides.ContainsKey("lord_1_34"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("lord_1_34")));
    }

    [TestMethod]
    public void GetConfig_PrefixedIds_AreDroppedAndWarn()
    {
        // The engine keys heroes and templates on bare StringIds. A "PartyTemplate." or "Hero."
        // prefix copied from the XML would never match anything, so it is rejected rather than
        // silently never firing.
        WriteConfig(@"{ ""overrides"": { ""Hero.lord_1_34"": ""kingdom_hero_party_gondor_faramir_template"", ""lord_1_17"": ""PartyTemplate.kingdom_hero_party_mordor_sauron_template"" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.Overrides.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Hero.lord_1_34")));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("PartyTemplate.")));
    }

    [TestMethod]
    public void GetConfig_AnyRejectedEntry_LogsSummaryWarning()
    {
        WriteConfig(@"{ ""overrides"": { ""lord_1_34"": """" } }");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("See prior warnings")));
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReadsFileOnce()
    {
        WriteConfig(@"{ ""overrides"": { ""lord_1_34"": ""kingdom_hero_party_gondor_faramir_template"" } }");

        var first = _sut.GetConfig();
        File.Delete(Path.Combine(_featureDir, "lord_party_templates.json"));
        var second = _sut.GetConfig();

        // Lazy<T> caches for the process lifetime; retuning needs an application restart.
        Assert.AreSame(first, second);
    }
}
