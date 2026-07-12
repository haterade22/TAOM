using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.IO;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.TroopWeight;

[TestClass]
public class TroopWeightXmlLoaderTests
{
    private TroopWeightXmlLoader _sut;
    private IPathService _pathService;
    private IModLogger _logger;
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _pathService = Substitute.For<IPathService>();
        _tempDir = Path.Combine(Path.GetTempPath(), "TroopWeightTests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "TroopWeights"));
        _pathService.ModuleDataPath.Returns(_tempDir + Path.DirectorySeparatorChar);
        _sut = new TroopWeightXmlLoader(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void GetTroopWeights_ValidXml_ReturnsAllEntries()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""4.0"" />
    <TroopWeight id=""imladris_blademaster"" weight=""2.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(4.0f, result["cave_troll"]);
        Assert.AreEqual(2.0f, result["imladris_blademaster"]);
    }

    [TestMethod]
    public void GetTroopWeights_MissingFile_ReturnsEmptyAndLogsWarning()
    {
        var result = _sut.GetTroopWeights();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetTroopWeights_LazyLoads_OnlyOnFirstCall()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""4.0"" />
</TroopWeights>");

        var first = _sut.GetTroopWeights();
        var second = _sut.GetTroopWeights();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetTroopWeights_DuplicateId_LastEntryWins()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""2.0"" />
    <TroopWeight id=""cave_troll"" weight=""4.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(4.0f, result["cave_troll"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("Duplicate")));
    }

    [TestMethod]
    public void GetTroopWeights_ZeroWeight_SkipsEntryWithWarning()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""0"" />
    <TroopWeight id=""imladris_blademaster"" weight=""2.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(1, result.Count);
        Assert.IsFalse(result.ContainsKey("cave_troll"));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("cave_troll") && s.Contains("positive")));
    }

    [TestMethod]
    public void GetTroopWeights_NegativeWeight_SkipsEntryWithWarning()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""bad_troop"" weight=""-1.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("positive")));
    }

    [TestMethod]
    public void GetTroopWeights_NaNOrInfinityWeight_SkipsEntryWithWarning()
    {
        // IEEE-754 NaN comparisons are always false, so a bare `weight <= 0` guard lets "NaN"/"Infinity"
        // through — which poisons the party's weighted sum and casts to int.MinValue downstream. The loader
        // must reject non-finite weights (deep-review 2026-07-11 GAP#3; "Config Providers MUST Validate").
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""nan_troop"" weight=""NaN"" />
    <TroopWeight id=""inf_troop"" weight=""Infinity"" />
    <TroopWeight id=""valid_troop"" weight=""2.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(1, result.Count, "only the finite, positive weight should load");
        Assert.IsTrue(result.ContainsKey("valid_troop"));
        Assert.IsFalse(result.ContainsKey("nan_troop"));
        Assert.IsFalse(result.ContainsKey("inf_troop"));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("nan_troop") && s.Contains("finite")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("inf_troop") && s.Contains("finite")));
    }

    [TestMethod]
    public void GetTroopWeights_MissingIdAttribute_SkipsEntryWithWarning()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight weight=""2.0"" />
    <TroopWeight id=""valid_troop"" weight=""2.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(1, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("missing")));
    }

    [TestMethod]
    public void GetTroopWeights_InvalidWeightValue_SkipsEntryWithWarning()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""bad_troop"" weight=""abc"" />
    <TroopWeight id=""valid_troop"" weight=""2.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.AreEqual(1, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("Invalid")));
    }

    [TestMethod]
    public void GetTroopWeights_CaseInsensitiveLookup()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""Cave_Troll"" weight=""4.0"" />
</TroopWeights>");

        var result = _sut.GetTroopWeights();

        Assert.IsTrue(result.ContainsKey("cave_troll"));
        Assert.IsTrue(result.ContainsKey("CAVE_TROLL"));
    }

    [TestMethod]
    public void ReloadWeights_ForcesReload()
    {
        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""2.0"" />
</TroopWeights>");

        var first = _sut.GetTroopWeights();
        Assert.AreEqual(2.0f, first["cave_troll"]);

        WriteXml(@"<?xml version=""1.0"" encoding=""utf-8""?>
<TroopWeights>
    <TroopWeight id=""cave_troll"" weight=""4.0"" />
</TroopWeights>");

        _sut.ReloadWeights();
        var second = _sut.GetTroopWeights();
        Assert.AreEqual(4.0f, second["cave_troll"]);
    }

    private void WriteXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "TroopWeights", "troop_weights.xml"), content);
    }
}
