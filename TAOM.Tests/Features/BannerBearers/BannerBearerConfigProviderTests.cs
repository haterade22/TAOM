using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerBearers;

namespace TAOM.Tests.Features.BannerBearers;

[TestClass]
public class BannerBearerConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private BannerBearerConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_BannerBearers_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "banner_bearers");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new BannerBearerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "banner_bearers_config.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(4, config.MinimumFormationTroopCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ this is not json ");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""Enabled"": false,
  ""MinimumFormationTroopCount"": 6,
  ""MaxBearersPerFormation"": 3,
  ""InfantryBannerPerSoldiers"": 30,
  ""RangedBannerPerSoldiers"": 31,
  ""CavalryBannerPerSoldiers"": 32,
  ""HorseArcherBannerPerSoldiers"": 33,
  ""OtherBannerPerSoldiers"": 34,
  ""DefaultBannerItemId"": ""iron_banner_t2""
}");

        var config = _sut.GetConfig();

        Assert.IsFalse(config.Enabled);
        Assert.AreEqual(6, config.MinimumFormationTroopCount);
        Assert.AreEqual(3, config.MaxBearersPerFormation);
        Assert.AreEqual(30, config.InfantryBannerPerSoldiers);
        Assert.AreEqual(31, config.RangedBannerPerSoldiers);
        Assert.AreEqual(32, config.CavalryBannerPerSoldiers);
        Assert.AreEqual(33, config.HorseArcherBannerPerSoldiers);
        Assert.AreEqual(34, config.OtherBannerPerSoldiers);
        Assert.AreEqual("iron_banner_t2", config.DefaultBannerItemId);
    }

    [TestMethod]
    public void GetConfig_ExcludedRacesInJson_ReplacesDefaultsNotAppends()
    {
        // ObjectCreationHandling.Replace: without it Json.NET appends onto the
        // ctor-populated defaults and the list would still contain cave_troll.
        WriteConfig(@"{ ""ExcludedRaces"": [ ""goblin"" ] }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.ExcludedRaces.Count);
        Assert.AreEqual("goblin", config.ExcludedRaces[0]);
    }

    [TestMethod]
    public void GetConfig_CultureBannersInJson_ReplacesDefaultsNotAppends()
    {
        WriteConfig(@"{ ""CultureBanners"": { ""gondor"": ""iron_banner_t2"" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1, config.CultureBanners.Count);
        Assert.AreEqual("iron_banner_t2", config.CultureBanners["gondor"]);
    }

    [TestMethod]
    public void GetConfig_MinimumTroopCountBelowEngineMinimum_RevertsToDefaultAndWarns()
    {
        // The engine's own floor is 2; anything lower is meaningless.
        WriteConfig(@"{ ""MinimumFormationTroopCount"": 1 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.MinimumFormationTroopCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("MinimumFormationTroopCount")));
    }

    [TestMethod]
    public void GetConfig_MaxBearersZero_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""MaxBearersPerFormation"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.MaxBearersPerFormation);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("MaxBearersPerFormation")));
    }

    [TestMethod]
    public void GetConfig_MaxBearersAboveEngineCap_RevertsToDefaultAndWarns()
    {
        // Engine arrangement tables hold 6 positions.
        WriteConfig(@"{ ""MaxBearersPerFormation"": 7 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.MaxBearersPerFormation);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("MaxBearersPerFormation")));
    }

    [TestMethod]
    public void GetConfig_NegativeBannerPerSoldiers_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""InfantryBannerPerSoldiers"": -3 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(20, config.InfantryBannerPerSoldiers);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("InfantryBannerPerSoldiers")));
    }

    [TestMethod]
    public void GetConfig_ZeroBannerPerSoldiers_IsAcceptedAsDisabled()
    {
        // 0 is a legitimate "no banners for this class" switch, not an invalid value.
        WriteConfig(@"{ ""RangedBannerPerSoldiers"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.RangedBannerPerSoldiers);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("RangedBannerPerSoldiers")));
    }

    [TestMethod]
    public void GetConfig_AnyRejectedField_LogsSummaryWarning()
    {
        WriteConfig(@"{ ""MaxBearersPerFormation"": 99 }");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("See prior warnings")));
    }

    [TestMethod]
    public void GetConfig_AllValid_LogsInfoNotSummaryWarning()
    {
        WriteConfig(@"{ ""MaxBearersPerFormation"": 5 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("See prior warnings")));
    }

    [TestMethod]
    public void GetConfig_NullCollections_RevertToDefaults()
    {
        WriteConfig(@"{ ""ExcludedRaces"": null, ""CultureBanners"": null }");

        var config = _sut.GetConfig();

        Assert.IsNotNull(config.ExcludedRaces);
        Assert.IsNotNull(config.CultureBanners);
        CollectionAssert.Contains(config.ExcludedRaces, "cave_troll");
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReadsFileOnce()
    {
        WriteConfig(@"{ ""MaxBearersPerFormation"": 5 }");

        var first = _sut.GetConfig();
        File.Delete(Path.Combine(_featureDir, "banner_bearers_config.json"));
        var second = _sut.GetConfig();

        // Lazy<T> caches for the process lifetime — retuning needs an app restart.
        Assert.AreSame(first, second);
    }
}
