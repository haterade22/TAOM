using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild;

namespace TAOM.Tests.Features.EditorCacheRebuild;

[TestClass]
public class CacheRebuildConfigProviderTests
{
    private const int FakeProcessorCount = 8;

    private string _tempDir = null!;
    private string _configsDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CacheRebuildConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CacheRebuild_" + Path.GetRandomFileName());
        _configsDir = Path.Combine(_tempDir, "configs");
        Directory.CreateDirectory(_configsDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = NewSut(FakeProcessorCount);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private CacheRebuildConfigProvider NewSut(int maxParallelism)
    {
        var ctor = typeof(CacheRebuildConfigProvider).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            new[] { typeof(IPathService), typeof(IModLogger), typeof(int) },
            modifiers: null)!;
        return (CacheRebuildConfigProvider)ctor.Invoke(new object[] { _pathService, _logger, maxParallelism });
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_configsDir, "cache_rebuild_config.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultAndLogsWarning()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(4, config.Parallelism);
        Assert.AreEqual(20, config.CheckpointEvery);
        Assert.IsTrue(config.EnableIncremental);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultAndLogsError()
    {
        WriteConfig("not valid json {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.Parallelism);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""enabled"": true,
  ""forceVanilla"": false,
  ""parallelism"": 6,
  ""checkpointEvery"": 50,
  ""enablePathReuse"": false,
  ""enablePersistentPathCache"": false,
  ""enableIncremental"": false,
  ""incrementalMaxChanged"": 10,
  ""incrementalSpatialRadius"": 2.5,
  ""enableDebugQualityCheck"": true,
  ""enableUiOverlay"": false,
  ""smokeTestPairs"": 20,
  ""smokeTestDistanceTolerance"": 0.001,
  ""phase1SkipReversePathfind"": true,
  ""logVerbosity"": ""debug""
}");

        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.IsFalse(config.ForceVanilla);
        Assert.AreEqual(6, config.Parallelism);
        Assert.AreEqual(50, config.CheckpointEvery);
        Assert.IsFalse(config.EnablePathReuse);
        Assert.IsFalse(config.EnablePersistentPathCache);
        Assert.IsFalse(config.EnableIncremental);
        Assert.AreEqual(10, config.IncrementalMaxChanged);
        Assert.AreEqual(2.5f, config.IncrementalSpatialRadius, 0.0001f);
        Assert.IsTrue(config.EnableDebugQualityCheck);
        Assert.IsFalse(config.EnableUiOverlay);
        Assert.AreEqual(20, config.SmokeTestPairs);
        Assert.AreEqual(0.001f, config.SmokeTestDistanceTolerance, 0.00001f);
        Assert.IsTrue(config.Phase1SkipReversePathfind);
        Assert.AreEqual("debug", config.LogVerbosity);
    }

    [TestMethod]
    public void GetConfig_ParallelismZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""parallelism"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.Parallelism);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("parallelism=0")));
    }

    [TestMethod]
    public void GetConfig_ParallelismAboveCap_RevertsToDefault()
    {
        WriteConfig(@"{ ""parallelism"": 999 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(4, config.Parallelism);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("parallelism=999")));
    }

    [TestMethod]
    public void GetConfig_ParallelismAtCap_Accepted()
    {
        WriteConfig($@"{{ ""parallelism"": {FakeProcessorCount} }}");

        var config = _sut.GetConfig();

        Assert.AreEqual(FakeProcessorCount, config.Parallelism);
    }

    [TestMethod]
    public void GetConfig_CheckpointEveryBelowMin_RevertsToDefault()
    {
        WriteConfig(@"{ ""checkpointEvery"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(20, config.CheckpointEvery);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("checkpointEvery=0")));
    }

    [TestMethod]
    public void GetConfig_CheckpointEveryAboveMax_RevertsToDefault()
    {
        WriteConfig(@"{ ""checkpointEvery"": 5000 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(20, config.CheckpointEvery);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("checkpointEvery=5000")));
    }

    [TestMethod]
    public void GetConfig_IncrementalMaxChangedNegative_RevertsToDefault()
    {
        WriteConfig(@"{ ""incrementalMaxChanged"": -5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(30, config.IncrementalMaxChanged);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("incrementalMaxChanged=-5")));
    }

    [TestMethod]
    public void GetConfig_IncrementalMaxChangedAboveMax_RevertsToDefault()
    {
        WriteConfig(@"{ ""incrementalMaxChanged"": 500 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(30, config.IncrementalMaxChanged);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("incrementalMaxChanged=500")));
    }

    [TestMethod]
    public void GetConfig_SpatialRadiusBelowMin_RevertsToDefault()
    {
        WriteConfig(@"{ ""incrementalSpatialRadius"": 0.01 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(5.0f, config.IncrementalSpatialRadius, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("incrementalSpatialRadius")));
    }

    [TestMethod]
    public void GetConfig_SpatialRadiusAboveMax_RevertsToDefault()
    {
        WriteConfig(@"{ ""incrementalSpatialRadius"": 1000.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(5.0f, config.IncrementalSpatialRadius, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("incrementalSpatialRadius")));
    }

    [TestMethod]
    public void GetConfig_SmokeTestPairsZero_RevertsToDefault()
    {
        WriteConfig(@"{ ""smokeTestPairs"": 0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(10, config.SmokeTestPairs);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("smokeTestPairs=0")));
    }

    [TestMethod]
    public void GetConfig_SmokeTestPairsAboveMax_RevertsToDefault()
    {
        WriteConfig(@"{ ""smokeTestPairs"": 1000 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(10, config.SmokeTestPairs);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("smokeTestPairs=1000")));
    }

    [TestMethod]
    public void GetConfig_SmokeTestToleranceTooTight_RevertsToDefault()
    {
        WriteConfig(@"{ ""smokeTestDistanceTolerance"": 1e-12 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1e-4f, config.SmokeTestDistanceTolerance, 1e-8f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("smokeTestDistanceTolerance")));
    }

    [TestMethod]
    public void GetConfig_SmokeTestToleranceTooLoose_RevertsToDefault()
    {
        WriteConfig(@"{ ""smokeTestDistanceTolerance"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1e-4f, config.SmokeTestDistanceTolerance, 1e-8f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("smokeTestDistanceTolerance")));
    }

    [TestMethod]
    public void GetConfig_LogVerbosityInvalid_RevertsToDefault()
    {
        WriteConfig(@"{ ""logVerbosity"": ""verbose"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual("info", config.LogVerbosity);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("logVerbosity='verbose'")));
    }

    [TestMethod]
    public void GetConfig_LogVerbosityUppercase_Normalized()
    {
        WriteConfig(@"{ ""logVerbosity"": ""DEBUG"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual("debug", config.LogVerbosity);
    }

    [TestMethod]
    public void GetConfig_AnyInvalidField_LogsSummaryWarning()
    {
        WriteConfig(@"{ ""parallelism"": 999, ""checkpointEvery"": 5000 }");

        _sut.GetConfig();

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetConfig_AllValid_LogsInfoSummary()
    {
        WriteConfig(@"{ ""parallelism"": 6 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded cache_rebuild_config.json")));
    }
}
