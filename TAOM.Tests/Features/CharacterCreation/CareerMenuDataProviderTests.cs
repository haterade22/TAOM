using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CareerMenuDataProviderTests
{
    private IPathService _pathService;
    private IModLogger _logger;
    private CareerMenuDataProvider _sut;
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _pathService = Substitute.For<IPathService>();
        _logger = Substitute.For<IModLogger>();
        _tempDir = Path.Combine(Path.GetTempPath(), "CareerMenuTests_" + Path.GetRandomFileName());
        var ccDir = Path.Combine(_tempDir, "charactercreation");
        Directory.CreateDirectory(ccDir);
        _pathService.ModuleDataPath.Returns(_tempDir);
        _sut = new CareerMenuDataProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadCareerMenuOptions_ValidJson_ReturnsAllEntries()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""ranger_of_ithilien"", ""skills"": [""Bow"", ""Scouting""], ""attribute"": ""Cunning"" },
            { ""career_string_id"": ""captain_of_osgiliath"", ""skills"": [""Leadership"", ""OneHanded""], ""attribute"": ""Social"" }
        ]");

        var result = _sut.LoadCareerMenuOptions();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("ranger_of_ithilien", result[0].CareerStringId);
        Assert.AreEqual("captain_of_osgiliath", result[1].CareerStringId);
    }

    [TestMethod]
    public void LoadCareerMenuOptions_ParsesSkillsAndAttribute()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""ranger_of_ithilien"", ""skills"": [""Bow"", ""Scouting""], ""attribute"": ""Cunning"",
              ""focus_to_add"": 2, ""skill_level_to_add"": 15, ""attribute_level_to_add"": 2 }
        ]");

        var result = _sut.LoadCareerMenuOptions();

        Assert.AreEqual(2, result[0].Skills.Length);
        Assert.AreEqual("Bow", result[0].Skills[0]);
        Assert.AreEqual("Scouting", result[0].Skills[1]);
        Assert.AreEqual("Cunning", result[0].Attribute);
        Assert.AreEqual(2, result[0].FocusToAdd);
        Assert.AreEqual(15, result[0].SkillLevelToAdd);
        Assert.AreEqual(2, result[0].AttributeLevelToAdd);
    }

    [TestMethod]
    public void LoadCareerMenuOptions_MissingFile_ReturnsEmptyAndLogs()
    {
        // Don't write any file — it shouldn't exist
        var result = _sut.LoadCareerMenuOptions();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void LoadCareerMenuOptions_InvalidJson_ReturnsEmptyAndLogs()
    {
        WriteCareerMenuJson("{ broken json !!!");

        var result = _sut.LoadCareerMenuOptions();

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void LoadCareerMenuOptions_MissingBonusFields_DefaultToZero()
    {
        // Career is flavor-only as of 2026-05-30: a missing bonus key means "no bonus",
        // not the old 1/10/1. This guards the landmine where deleting (not zeroing) the
        // keys would silently restore the pre-cut budget.
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""knight_of_belfalas"", ""skills"": [""Riding""], ""attribute"": ""Vigor"" }
        ]");

        var result = _sut.LoadCareerMenuOptions();

        Assert.AreEqual(0, result[0].FocusToAdd);
        Assert.AreEqual(0, result[0].SkillLevelToAdd);
        Assert.AreEqual(0, result[0].AttributeLevelToAdd);
    }

    [TestMethod]
    public void LoadCareerMenuOptions_CachesResult()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""ranger_of_ithilien"", ""skills"": [""Bow""], ""attribute"": ""Cunning"" }
        ]");

        var result1 = _sut.LoadCareerMenuOptions();
        var result2 = _sut.LoadCareerMenuOptions();

        Assert.AreSame(result1, result2);
    }

    [TestMethod]
    public void GetOptionForCareer_ExistingCareer_ReturnsMatch()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""ranger_of_ithilien"", ""skills"": [""Bow""], ""attribute"": ""Cunning"" },
            { ""career_string_id"": ""captain_of_osgiliath"", ""skills"": [""Leadership""], ""attribute"": ""Social"" }
        ]");

        var result = _sut.GetOptionForCareer("captain_of_osgiliath");

        Assert.IsNotNull(result);
        Assert.AreEqual("captain_of_osgiliath", result.CareerStringId);
    }

    [TestMethod]
    public void GetOptionForCareer_UnknownCareer_ReturnsNull()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""ranger_of_ithilien"", ""skills"": [""Bow""], ""attribute"": ""Cunning"" }
        ]");

        var result = _sut.GetOptionForCareer("nonexistent_career");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetOptionForCareer_CaseInsensitive()
    {
        WriteCareerMenuJson(@"[
            { ""career_string_id"": ""Ranger_Of_Ithilien"", ""skills"": [""Bow""], ""attribute"": ""Cunning"" }
        ]");

        var result = _sut.GetOptionForCareer("ranger_of_ithilien");

        Assert.IsNotNull(result);
    }

    private void WriteCareerMenuJson(string json)
    {
        var ccDir = Path.Combine(_tempDir, "charactercreation");
        Directory.CreateDirectory(ccDir);
        File.WriteAllText(Path.Combine(ccDir, "career_menu.json"), json);
    }
}
