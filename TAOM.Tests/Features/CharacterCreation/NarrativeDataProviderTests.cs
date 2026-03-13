using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class NarrativeDataProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private NarrativeDataProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_NAR_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "charactercreation"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new NarrativeDataProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadMenuOptions_ParsesValidJson()
    {
        WriteJson("parents", @"[
            {
                ""string_id"": ""taom_parent_gondor_1"",
                ""culture_id"": ""gondor"",
                ""text"": ""Noble Houses of Gondor"",
                ""description"": ""Your family belonged to one of Gondor's noble houses."",
                ""skills"": [""TwoHanded"", ""Riding""],
                ""attribute"": ""Vigor"",
                ""focus_to_add"": 1,
                ""skill_level_to_add"": 10,
                ""attribute_level_to_add"": 1
            }
        ]");

        var result = _sut.LoadMenuOptions("parents");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("taom_parent_gondor_1", result[0].StringId);
        Assert.AreEqual("gondor", result[0].CultureId);
        Assert.AreEqual("Noble Houses of Gondor", result[0].Text);
        Assert.AreEqual("Vigor", result[0].Attribute);
        CollectionAssert.AreEqual(new[] { "TwoHanded", "Riding" }, result[0].Skills);
    }

    [TestMethod]
    public void LoadMenuOptions_MultipleOptions_LoadsAll()
    {
        WriteJson("parents", @"[
            { ""string_id"": ""opt1"", ""culture_id"": ""gondor"", ""text"": ""Option 1"" },
            { ""string_id"": ""opt2"", ""culture_id"": ""gondor"", ""text"": ""Option 2"" },
            { ""string_id"": ""opt3"", ""culture_id"": ""mordor"", ""text"": ""Option 3"" }
        ]");

        var result = _sut.LoadMenuOptions("parents");

        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void LoadMenuOptions_MissingFile_ReturnsEmptyAndLogsWarning()
    {
        var result = new NarrativeDataProvider(
            Substitute.For<IPathService>(), _logger).LoadMenuOptions("parents");

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadMenuOptions_MalformedJson_ReturnsEmptyAndLogsError()
    {
        WriteJson("parents", "{ invalid json }}}}");

        var result = _sut.LoadMenuOptions("parents");

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadMenuOptions_MissingOptionalFields_UsesDefaults()
    {
        WriteJson("parents", @"[{ ""string_id"": ""opt1"", ""culture_id"": ""test"" }]");

        var result = _sut.LoadMenuOptions("parents");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("", result[0].Text);
        Assert.AreEqual("", result[0].Description);
        Assert.AreEqual("", result[0].Attribute);
        Assert.AreEqual(0, result[0].Skills.Length);
        Assert.AreEqual(1, result[0].FocusToAdd);
        Assert.AreEqual(10, result[0].SkillLevelToAdd);
        Assert.AreEqual(1, result[0].AttributeLevelToAdd);
    }

    [TestMethod]
    public void LoadMenuOptions_CachesResult()
    {
        WriteJson("parents", @"[{ ""string_id"": ""opt1"", ""culture_id"": ""gondor"" }]");

        var first = _sut.LoadMenuOptions("parents");
        var second = _sut.LoadMenuOptions("parents");

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void LoadMenuOptions_DifferentMenus_CachedSeparately()
    {
        WriteJson("parents", @"[{ ""string_id"": ""p1"", ""culture_id"": ""gondor"" }]");
        WriteJson("childhood", @"[
            { ""string_id"": ""c1"", ""text"": ""Option A"" },
            { ""string_id"": ""c2"", ""text"": ""Option B"" }
        ]");

        var parents = _sut.LoadMenuOptions("parents");
        var childhood = _sut.LoadMenuOptions("childhood");

        Assert.AreEqual(1, parents.Count);
        Assert.AreEqual(2, childhood.Count);
        Assert.AreEqual("p1", parents[0].StringId);
        Assert.AreEqual("c1", childhood[0].StringId);
    }

    [TestMethod]
    public void GetMenuOptionsForCulture_FiltersCorrectly()
    {
        WriteJson("parents", @"[
            { ""string_id"": ""opt1"", ""culture_id"": ""gondor"", ""text"": ""Gondor 1"" },
            { ""string_id"": ""opt2"", ""culture_id"": ""mordor"", ""text"": ""Mordor 1"" },
            { ""string_id"": ""opt3"", ""culture_id"": ""gondor"", ""text"": ""Gondor 2"" }
        ]");

        var result = _sut.GetMenuOptionsForCulture("parents", "gondor");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("Gondor 1", result[0].Text);
        Assert.AreEqual("Gondor 2", result[1].Text);
    }

    [TestMethod]
    public void GetMenuOptionsForCulture_CaseInsensitive()
    {
        WriteJson("youth", @"[{ ""string_id"": ""opt1"", ""culture_id"": ""Gondor"", ""text"": ""Test"" }]");

        var result = _sut.GetMenuOptionsForCulture("youth", "gondor");

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void GetMenuOptionsForCulture_NotFound_ReturnsEmpty()
    {
        WriteJson("parents", @"[{ ""string_id"": ""opt1"", ""culture_id"": ""gondor"" }]");

        var result = _sut.GetMenuOptionsForCulture("parents", "unknown");

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void LoadMenuOptions_ParsesTitleType()
    {
        WriteJson("youth", @"[
            {
                ""string_id"": ""taom_youth_gondor_1"",
                ""culture_id"": ""gondor"",
                ""text"": ""Trained with the Swan Knights."",
                ""title_type"": ""retainer"",
                ""skills"": [""Riding"", ""Polearm""],
                ""attribute"": ""Endurance""
            }
        ]");

        var result = _sut.LoadMenuOptions("youth");

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("retainer", result[0].TitleType);
    }

    [TestMethod]
    public void LoadMenuOptions_MissingTitleType_DefaultsToEmpty()
    {
        WriteJson("parents", @"[{ ""string_id"": ""opt1"", ""culture_id"": ""gondor"" }]");

        var result = _sut.LoadMenuOptions("parents");

        Assert.AreEqual("", result[0].TitleType);
    }

    [TestMethod]
    public void LoadMenuOptions_UniversalOptions_HaveEmptyCultureId()
    {
        WriteJson("childhood", @"[
            { ""string_id"": ""c1"", ""text"": ""Leadership"" },
            { ""string_id"": ""c2"", ""culture_id"": """", ""text"": ""Fighting"" }
        ]");

        var result = _sut.LoadMenuOptions("childhood");

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("", result[0].CultureId);
        Assert.AreEqual("", result[1].CultureId);
    }

    private void WriteJson(string menuName, string json)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "charactercreation", $"{menuName}_menu.json"), json);
    }
}
