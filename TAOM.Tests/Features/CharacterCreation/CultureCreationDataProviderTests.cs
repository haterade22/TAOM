using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CultureCreationDataProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private CultureCreationDataProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CC_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "charactercreation"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CultureCreationDataProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadCultures_ParsesValidJson()
    {
        WriteJson(@"[
            {
                ""culture_id"": ""gondor"",
                ""races"": [""human""],
                ""starting_settlement"": ""town_EW1"",
                ""default_age"": 30.0,
                ""default_weight"": 0.0,
                ""default_build"": 0.7222,
                ""focus_to_add"": 1,
                ""skill_level_to_add"": 10
            }
        ]");

        var result = _sut.LoadCultures();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("gondor", result[0].CultureId);
        Assert.AreEqual("town_EW1", result[0].StartingSettlement);
        Assert.AreEqual(30.0f, result[0].DefaultAge, 0.01f);
        Assert.AreEqual(0.7222f, result[0].DefaultBuild, 0.001f);
        CollectionAssert.AreEqual(new[] { "human" }, result[0].Races);
    }

    [TestMethod]
    public void LoadCultures_MultipleCultures_LoadsAll()
    {
        WriteJson(@"[
            { ""culture_id"": ""gondor"", ""races"": [""human""], ""starting_settlement"": ""town_EW1"" },
            { ""culture_id"": ""mordor"", ""races"": [""uruk"", ""orc""], ""starting_settlement"": ""town_ES1"" },
            { ""culture_id"": ""erebor"", ""races"": [""dwarf""], ""starting_settlement"": ""town_SWAN_EREBOR1"" }
        ]");

        var result = _sut.LoadCultures();

        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("mordor", result[1].CultureId);
        Assert.AreEqual(2, result[1].Races.Length);
    }

    [TestMethod]
    public void LoadCultures_MissingFile_ReturnsEmptyAndLogsWarning()
    {
        var result = new CultureCreationDataProvider(
            Substitute.For<IPathService>(), _logger).LoadCultures();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadCultures_MalformedJson_ReturnsEmptyAndLogsError()
    {
        WriteJson("{ invalid json }}}}");

        var result = _sut.LoadCultures();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadCultures_MissingOptionalFields_UsesDefaults()
    {
        WriteJson(@"[{ ""culture_id"": ""test"" }]");

        var result = _sut.LoadCultures();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("test", result[0].CultureId);
        Assert.AreEqual(30f, result[0].DefaultAge, 0.01f);
        Assert.AreEqual(0.5f, result[0].DefaultWeight, 0.01f);
        Assert.AreEqual(0.5f, result[0].DefaultBuild, 0.01f);
        Assert.AreEqual(1, result[0].FocusToAdd);
        Assert.AreEqual(10, result[0].SkillLevelToAdd);
        Assert.AreEqual(0, result[0].Races.Length);
        Assert.AreEqual("", result[0].StartingSettlement);
    }

    [TestMethod]
    public void LoadCultures_CachesResult_SecondCallReturnsSame()
    {
        WriteJson(@"[{ ""culture_id"": ""gondor"" }]");

        var first = _sut.LoadCultures();
        var second = _sut.LoadCultures();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetCultureData_ReturnsMatchingCulture()
    {
        WriteJson(@"[
            { ""culture_id"": ""gondor"", ""starting_settlement"": ""town_EW1"" },
            { ""culture_id"": ""mordor"", ""starting_settlement"": ""town_ES1"" }
        ]");

        var result = _sut.GetCultureData("mordor");

        Assert.IsNotNull(result);
        Assert.AreEqual("mordor", result.CultureId);
        Assert.AreEqual("town_ES1", result.StartingSettlement);
    }

    [TestMethod]
    public void GetCultureData_CaseInsensitive()
    {
        WriteJson(@"[{ ""culture_id"": ""Gondor"" }]");

        var result = _sut.GetCultureData("gondor");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void GetCultureData_NotFound_ReturnsNull()
    {
        WriteJson(@"[{ ""culture_id"": ""gondor"" }]");

        var result = _sut.GetCultureData("unknown");

        Assert.IsNull(result);
    }

    private void WriteJson(string json)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "charactercreation", "cultures.json"), json);
    }
}
