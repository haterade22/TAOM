using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

[TestClass]
public class TrainingWavesConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_TgWaves_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "training_grounds"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private TrainingWavesConfigProvider Provider(string moduleData)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData);
        return new TrainingWavesConfigProvider(paths, _logger);
    }

    private string JsonPath => Path.Combine(_tempDir, "training_grounds", "training_waves.json");

    private static int Weight(WaveRow row, int tier) => row.TierWeights.TryGetValue(tier, out var w) ? w : 0;

    [TestMethod]
    public void GetTable_MissingFile_ReturnsTheDefaultTable_AndLogsInfo()
    {
        var table = Provider(_tempDir).GetTable();

        Assert.AreSame(WaveTable.Default, table);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
        _logger.ReceivedWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void GetTable_MissingFolder_ReturnsTheDefaultTable()
    {
        Assert.AreSame(WaveTable.Default, Provider(Path.Combine(_tempDir, "nowhere")).GetTable());
    }

    [TestMethod]
    public void GetTable_ValidFile_ReadsIt()
    {
        File.WriteAllText(JsonPath, "{\"waves\":[{\"count\":4,\"atOnce\":2,\"tiers\":{\"3\":1},\"weaponStage\":1,\"cap\":400,\"oddsTenths\":13}]}");

        var table = Provider(_tempDir).GetTable();

        Assert.AreEqual(1, table.Waves.Count);
        Assert.AreEqual(400, table.Waves[0].Cap);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void GetTable_MalformedFile_ReturnsTheDefaultTable_AndLogsAWarning()
    {
        File.WriteAllText(JsonPath, "{ nope");

        var table = Provider(_tempDir).GetTable();

        Assert.AreEqual(WaveTable.Default.Waves.Count, table.Waves.Count);
        _logger.ReceivedWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void GetTable_InvalidValue_LogsTheReaderWarnings()
    {
        File.WriteAllText(JsonPath, "{\"rules\":{\"returnSeconds\":99},\"waves\":[{\"count\":4,\"atOnce\":2,\"tiers\":{\"3\":1},\"weaponStage\":1,\"cap\":400,\"oddsTenths\":13}]}");

        var table = Provider(_tempDir).GetTable();

        Assert.AreEqual(5, table.Rules.ReturnSeconds);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("returnSeconds")));
        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetTable_IsReadOnce_AndCached()
    {
        File.WriteAllText(JsonPath, "{\"waves\":[{\"count\":4,\"atOnce\":2,\"tiers\":{\"3\":1},\"weaponStage\":1,\"cap\":400,\"oddsTenths\":13}]}");
        var provider = Provider(_tempDir);

        var first = provider.GetTable();
        File.WriteAllText(JsonPath, "{}");

        Assert.AreSame(first, provider.GetTable());
    }

    [TestMethod]
    public void GetTable_DoesNotReadUntilAsked()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(_tempDir);

        _ = new TrainingWavesConfigProvider(paths, _logger);

        _ = paths.DidNotReceive().ModuleDataPath;
    }

    [TestMethod]
    public void TheShippedFile_ReadsWithZeroWarnings_AndEqualsTheDefaultTable()
    {
        var logger = Substitute.For<IModLogger>();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        Assert.IsTrue(File.Exists(Path.Combine(CultureDataFixture.ModuleDataPath(), "training_grounds", "training_waves.json")), "the shipped file is missing");

        var table = new TrainingWavesConfigProvider(paths, logger).GetTable();

        Assert.AreEqual(0, logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name is "LogWarning" or "LogError"), "the shipped file does not read clean");
        Assert.AreEqual(WaveTable.Default.Rules, table.Rules);
        Assert.AreEqual(WaveTable.Default.Waves.Count, table.Waves.Count);
        for (var i = 0; i < table.Waves.Count; i++)
        {
            var e = WaveTable.Default.Waves[i];
            var a = table.Waves[i];
            Assert.AreEqual(e.Count, a.Count, "count " + (i + 1));
            Assert.AreEqual(e.AtOnce, a.AtOnce, "atOnce " + (i + 1));
            Assert.AreEqual(e.WeaponStage, a.WeaponStage, "stage " + (i + 1));
            Assert.AreEqual(e.Cap, a.Cap, "cap " + (i + 1));
            Assert.AreEqual(e.OddsTenths, a.OddsTenths, "odds " + (i + 1));
            foreach (var tier in new[] { 3, 4, 5 })
                Assert.AreEqual(Weight(e, tier), Weight(a, tier), $"tier {tier} of wave {i + 1}");
        }
    }
}
