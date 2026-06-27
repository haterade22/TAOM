using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Config;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleCommandersProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private CustomBattleCommandersProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CustomBattleCommanders_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "custom_battle");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CustomBattleCommandersProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "custom_battle_commanders.json"), json);

    [TestMethod]
    public void GetCuratedCommanderIds_ValidJson_ReturnsIdsInConfigOrder()
    {
        // Includes a 3-segment id and a list longer than 3 to prove no reorder / no cap.
        WriteConfig(@"{ ""factions"": { ""vlandia"": [""lord_4_1"",""lord_4_3_1"",""lord_4_7"",""lord_4_3_2"",""lord_4_16""] } }");

        var ids = _sut.GetCuratedCommanderIds("vlandia");

        Assert.AreEqual(5, ids.Count);
        Assert.AreEqual("lord_4_1", ids[0]);
        Assert.AreEqual("lord_4_3_1", ids[1]);
        Assert.AreEqual("lord_4_7", ids[2]);
        Assert.AreEqual("lord_4_3_2", ids[3]);
        Assert.AreEqual("lord_4_16", ids[4]);
    }

    [TestMethod]
    public void HasCuratedEntry_ListedFaction_True()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.IsTrue(_sut.HasCuratedEntry("mordor"));
    }

    [TestMethod]
    public void HasCuratedEntry_UnlistedFaction_False()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.IsFalse(_sut.HasCuratedEntry("gondor"));
    }

    [TestMethod]
    public void HasCuratedEntry_NullFaction_False()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.IsFalse(_sut.HasCuratedEntry(null));
    }

    [TestMethod]
    public void GetCuratedCommanderIds_UnlistedFaction_ReturnsEmpty()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.AreEqual(0, _sut.GetCuratedCommanderIds("gondor").Count);
    }

    [TestMethod]
    public void Load_CaseInsensitiveFactionKey_Resolves()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.IsTrue(_sut.HasCuratedEntry("Mordor"));
        Assert.AreEqual(1, _sut.GetCuratedCommanderIds("MORDOR").Count);
    }

    [TestMethod]
    public void GetCuratedCommanderIds_MissingFile_ReturnsEmptyAndWarns()
    {
        // No file written
        Assert.IsFalse(_sut.HasCuratedEntry("mordor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetCuratedCommanderIds_MalformedJson_ReturnsEmptyAndLogsError()
    {
        WriteConfig("not valid json {{{");

        Assert.IsFalse(_sut.HasCuratedEntry("mordor"));
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void Load_NoFactionsMap_ReturnsEmptyAndWarns()
    {
        WriteConfig(@"{ ""somethingElse"": 1 }");

        Assert.IsFalse(_sut.HasCuratedEntry("mordor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no 'factions' map")));
    }

    [TestMethod]
    public void Load_EmptyOrWhitespaceId_SkippedAndWarns()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17"","" "",""""] } }");

        var ids = _sut.GetCuratedCommanderIds("mordor");

        Assert.AreEqual(1, ids.Count);
        Assert.AreEqual("lord_1_17", ids[0]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty/whitespace commander id")));
    }

    [TestMethod]
    public void Load_DuplicateIdsInFaction_DedupedFirstOrderWins_Warns()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17"",""lord_1_15"",""lord_1_17""] } }");

        var ids = _sut.GetCuratedCommanderIds("mordor");

        Assert.AreEqual(2, ids.Count);
        Assert.AreEqual("lord_1_17", ids[0]);
        Assert.AreEqual("lord_1_15", ids[1]);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("duplicate commander id")));
    }

    [TestMethod]
    public void Load_EmptyOrWhitespaceFactionKey_SkippedAndWarns()
    {
        WriteConfig(@"{ ""factions"": { "" "": [""lord_1_17""] } }");

        // Trigger the lazy load via a non-whitespace key (HasCuratedEntry(" ") would short-circuit
        // on its own guard before the load runs).
        _sut.HasCuratedEntry("mordor");

        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty/whitespace faction key")));
    }

    [TestMethod]
    public void Load_UnknownFactionKey_KeptButWarns()
    {
        WriteConfig(@"{ ""factions"": { ""mordorr"": [""lord_1_17""] } }");

        Assert.IsTrue(_sut.HasCuratedEntry("mordorr")); // kept (inert unless a faction with that id exists)
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not a known culture id")));
    }

    [TestMethod]
    public void Load_FactionWithAllInvalidIds_NotRegistered_HasCuratedEntryFalse()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": ["""","" ""] } }");

        Assert.IsFalse(_sut.HasCuratedEntry("mordor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no valid commander ids")));
    }

    [TestMethod]
    public void Load_ValidConfig_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17"",""lord_1_15""] } }");

        _sut.GetCuratedCommanderIds("mordor");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded") && s.Contains("curated")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCuratedCommanderIds_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""factions"": { ""mordor"": [""lord_1_17""] } }");

        Assert.AreSame(_sut.GetCuratedCommanderIds("mordor"), _sut.GetCuratedCommanderIds("mordor"));
    }
}
