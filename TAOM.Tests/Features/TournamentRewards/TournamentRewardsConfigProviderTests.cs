using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The provider is
/// fail-soft: a bad factor reverts to the default row's with a warning, and a missing or unreadable file gives
/// every culture vanilla's factor of 1.
/// </summary>
[TestClass]
public class TournamentRewardsConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private TournamentRewardsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_TournamentRewards_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "tournament_rewards");
        Directory.CreateDirectory(_featureDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new TournamentRewardsConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string cultures) =>
        File.WriteAllText(Path.Combine(_featureDir, "tournament_rewards.json"), "{ \"cultures\": { " + cultures + " } }");

    private const string Default = @"""default"": { ""renown"": 1.0, ""influence"": 1.0, ""skill_xp"": 1.0 }";

    private static void AssertFactors(TournamentCultureFactors f, float renown, float influence, float xp)
    {
        Assert.AreEqual(renown, f.Renown, 0.0001f, "renown");
        Assert.AreEqual(influence, f.Influence, 0.0001f, "influence");
        Assert.AreEqual(xp, f.SkillXp, 0.0001f, "skill_xp");
    }

    [TestMethod]
    public void For_FileMissing_EveryCultureIsNeutral_AndWarns()
    {
        File.Delete(Path.Combine(_featureDir, "tournament_rewards.json"));

        AssertFactors(_sut.GetCatalog().For("gondor"), 1f, 1f, 1f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void For_MalformedJson_EveryCultureIsNeutral_AndLogsAnError()
    {
        File.WriteAllText(Path.Combine(_featureDir, "tournament_rewards.json"), "{ not json");

        AssertFactors(_sut.GetCatalog().For("gondor"), 1f, 1f, 1f);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void For_CultureWithARow_ReturnsItsFactors()
    {
        WriteConfig(Default + @", ""gondor"": { ""renown"": 1.25, ""influence"": 1.5, ""skill_xp"": 1.1 }");

        AssertFactors(_sut.GetCatalog().For("gondor"), 1.25f, 1.5f, 1.1f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void For_CultureWithoutARow_GetsTheDefaultRow()
    {
        WriteConfig(@"""default"": { ""renown"": 1.1, ""influence"": 1.2, ""skill_xp"": 1.3 }");

        AssertFactors(_sut.GetCatalog().For("rivendell"), 1.1f, 1.2f, 1.3f);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void For_NoCulture_GetsTheDefaultRow(string? cultureId)
    {
        WriteConfig(@"""default"": { ""renown"": 1.1, ""influence"": 1.0, ""skill_xp"": 1.0 }");

        Assert.AreEqual(1.1f, _sut.GetCatalog().For(cultureId).Renown, 0.0001f);
    }

    [TestMethod]
    public void For_RowMissingAField_InheritsTheDefaultRowsValue()
    {
        WriteConfig(@"""default"": { ""renown"": 1.0, ""influence"": 1.0, ""skill_xp"": 1.2 }, ""vlandia"": { ""renown"": 1.5 }");

        AssertFactors(_sut.GetCatalog().For("vlandia"), 1.5f, 1f, 1.2f);
    }

    [TestMethod]
    public void For_KeyCaseAndSpacing_Normalised()
    {
        WriteConfig(Default + @", "" Mordor "": { ""renown"": 1.5 }");

        Assert.AreEqual(1.5f, _sut.GetCatalog().For("mordor").Renown, 0.0001f);
    }

    [DataTestMethod]
    [DataRow("-0.5")]
    [DataRow("10.5")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void For_FactorOutOfRange_RevertsToTheDefaultRow_AndWarns(string value)
    {
        WriteConfig(Default + @", ""gondor"": { ""renown"": " + value + " }");

        Assert.AreEqual(1f, _sut.GetCatalog().For("gondor").Renown, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("gondor")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    [TestMethod]
    public void For_DefaultRowInvalid_RevertsToNeutral_AndWarns()
    {
        WriteConfig(@"""default"": { ""renown"": -3, ""influence"": 1.0, ""skill_xp"": 1.0 }");

        Assert.AreEqual(1f, _sut.GetCatalog().For("gondor").Renown, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("default")));
    }

    [TestMethod]
    public void For_NoDefaultRow_UsesNeutral_AndWarns()
    {
        WriteConfig(@"""gondor"": { ""renown"": 1.25 }");

        AssertFactors(_sut.GetCatalog().For("rhun"), 1f, 1f, 1f);
        Assert.AreEqual(1.25f, _sut.GetCatalog().For("gondor").Renown, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no 'default'")));
    }

    [TestMethod]
    public void For_NoCulturesObject_UsesNeutral_AndWarns()
    {
        File.WriteAllText(Path.Combine(_featureDir, "tournament_rewards.json"), "{ }");

        AssertFactors(_sut.GetCatalog().For("gondor"), 1f, 1f, 1f);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCatalog_ReadsTheFileOnce()
    {
        WriteConfig(Default);

        var first = _sut.GetCatalog();
        File.Delete(Path.Combine(_featureDir, "tournament_rewards.json"));

        Assert.AreSame(first, _sut.GetCatalog());
    }
}
