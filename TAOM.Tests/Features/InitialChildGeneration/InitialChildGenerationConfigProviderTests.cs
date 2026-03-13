using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Tests.Features.InitialChildGeneration;

[TestClass]
public class InitialChildGenerationConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private InitialChildGenerationConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ConfigPath.Returns(_tempDir + Path.DirectorySeparatorChar);
        _logger = Substitute.For<IModLogger>();

        _sut = new InitialChildGenerationConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadConfig_ValidJson_ParsesAllFields()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial_child_generation.json"), @"{
  ""defaults"": {
    ""min_age"": 3,
    ""max_age"": 15,
    ""female_ratio"": 0.40,
    ""child_count_multiplier"": 0.75
  },
  ""excluded_cultures"": [""mordor"", ""isengard""],
  ""excluded_clans"": [""clan_evil_1""],
  ""culture_overrides"": [
    {
      ""culture_id"": ""erebor"",
      ""min_age"": 5,
      ""max_age"": 12,
      ""female_ratio"": 0.35,
      ""child_count_multiplier"": 0.5
    }
  ],
  ""clan_overrides"": [
    {
      ""clan_id"": ""clan_gondor_1"",
      ""fixed_child_count"": 4,
      ""min_age"": 10
    }
  ]
}");

        var config = _sut.LoadConfig();

        Assert.AreEqual(3, config.Defaults.MinAge);
        Assert.AreEqual(15, config.Defaults.MaxAge);
        Assert.AreEqual(0.40, config.Defaults.FemaleRatio, 0.001);
        Assert.AreEqual(0.75, config.Defaults.ChildCountMultiplier, 0.001);
        Assert.AreEqual(2, config.ExcludedCultures.Count);
        CollectionAssert.Contains(config.ExcludedCultures, "mordor");
        CollectionAssert.Contains(config.ExcludedCultures, "isengard");
        Assert.AreEqual(1, config.ExcludedClans.Count);
        CollectionAssert.Contains(config.ExcludedClans, "clan_evil_1");
        Assert.AreEqual(1, config.CultureOverrides.Count);
        Assert.AreEqual("erebor", config.CultureOverrides[0].CultureId);
        Assert.AreEqual(5, config.CultureOverrides[0].MinAge);
        Assert.AreEqual(12, config.CultureOverrides[0].MaxAge);
        Assert.AreEqual(0.35, config.CultureOverrides[0].FemaleRatio.Value, 0.001);
        Assert.AreEqual(0.5, config.CultureOverrides[0].ChildCountMultiplier.Value, 0.001);
        Assert.AreEqual(1, config.ClanOverrides.Count);
        Assert.AreEqual("clan_gondor_1", config.ClanOverrides[0].ClanId);
        Assert.AreEqual(4, config.ClanOverrides[0].FixedChildCount);
        Assert.AreEqual(10, config.ClanOverrides[0].MinAge);
        Assert.IsNull(config.ClanOverrides[0].MaxAge);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsDefaultConfig()
    {
        var config = _sut.LoadConfig();

        Assert.AreEqual(2, config.Defaults.MinAge);
        Assert.AreEqual(17, config.Defaults.MaxAge);
        Assert.AreEqual(0.49, config.Defaults.FemaleRatio, 0.001);
        Assert.AreEqual(1.0, config.Defaults.ChildCountMultiplier, 0.001);
        Assert.AreEqual(0, config.ExcludedCultures.Count);
        Assert.AreEqual(0, config.ExcludedClans.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadConfig_MalformedJson_ReturnsDefaultAndLogs()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial_child_generation.json"), "not valid json {{{");

        var config = _sut.LoadConfig();

        Assert.AreEqual(2, config.Defaults.MinAge);
        Assert.AreEqual(17, config.Defaults.MaxAge);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadConfig_EmptyExclusionLists_ReturnsEmptyLists()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial_child_generation.json"), @"{
  ""excluded_cultures"": [],
  ""excluded_clans"": []
}");

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.ExcludedCultures.Count);
        Assert.AreEqual(0, config.ExcludedClans.Count);
        Assert.AreEqual(2, config.Defaults.MinAge);
    }

    [TestMethod]
    public void LoadConfig_CachesResult()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial_child_generation.json"), @"{
  ""excluded_cultures"": [""mordor""]
}");

        var first = _sut.LoadConfig();
        var second = _sut.LoadConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void LoadConfig_PartialConfig_MergesWithDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "initial_child_generation.json"), @"{
  ""excluded_cultures"": [""gundabad""],
  ""defaults"": {
    ""min_age"": 5
  }
}");

        var config = _sut.LoadConfig();

        Assert.AreEqual(5, config.Defaults.MinAge);
        Assert.AreEqual(17, config.Defaults.MaxAge);
        Assert.AreEqual(0.49, config.Defaults.FemaleRatio, 0.001);
        Assert.AreEqual(1, config.ExcludedCultures.Count);
        CollectionAssert.Contains(config.ExcludedCultures, "gundabad");
    }
}
