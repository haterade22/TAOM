using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

[TestClass]
public class FormationPresetServiceTests
{
    private ICompanionTacticsSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private FormationPresetService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICompanionTacticsSettingsProvider>();
        _settings.MaxFormationPresets.Returns(10);
        _settings.EnableFormationPresets.Returns(true);
        _settings.FormationPresetsDebug.Returns(false);

        _logger = Substitute.For<IModLogger>();

        _sut = new FormationPresetService(_settings, _logger);
    }

    private static HoNFormationPreset MakePreset(string name = "P1")
    {
        var p = new HoNFormationPreset(name);
        p.HeroFormationAssignments["hero_a"] = 0;
        p.CaptainHeroIds.Add("hero_a");
        p.FormationClasses[0] = 1;
        return p;
    }

    [TestMethod]
    public void Presets_Initial_Empty()
    {
        Assert.AreEqual(0, _sut.Presets.Count);
    }

    [TestMethod]
    public void SavePreset_Valid_ReturnsSaved()
    {
        var p = MakePreset();

        var r = _sut.SavePreset(p);

        Assert.AreEqual(SaveResult.Saved, r);
        Assert.AreEqual(1, _sut.Presets.Count);
    }

    [TestMethod]
    public void SavePreset_NullPreset_ReturnsInvalid()
    {
        var r = _sut.SavePreset(null);

        Assert.AreEqual(SaveResult.Invalid, r);
    }

    [TestMethod]
    public void SavePreset_EmptyName_ReturnsInvalid()
    {
        var p = new HoNFormationPreset("");

        var r = _sut.SavePreset(p);

        Assert.AreEqual(SaveResult.Invalid, r);
    }

    [TestMethod]
    public void SavePreset_DuplicateName_ReturnsNameInUse()
    {
        _sut.SavePreset(MakePreset("Defensive"));

        var r = _sut.SavePreset(MakePreset("Defensive"));

        Assert.AreEqual(SaveResult.NameInUse, r);
        Assert.AreEqual(1, _sut.Presets.Count);
    }

    [TestMethod]
    public void SavePreset_AtLimit_ReturnsLimitReached()
    {
        _settings.MaxFormationPresets.Returns(2);

        Assert.AreEqual(SaveResult.Saved, _sut.SavePreset(MakePreset("a")));
        Assert.AreEqual(SaveResult.Saved, _sut.SavePreset(MakePreset("b")));
        var r = _sut.SavePreset(MakePreset("c"));

        Assert.AreEqual(SaveResult.LimitReached, r);
        Assert.AreEqual(2, _sut.Presets.Count);
    }

    [TestMethod]
    public void SavePreset_ExistingPresetUpdate_NotCountedAgainstLimit()
    {
        _settings.MaxFormationPresets.Returns(2);
        var a = MakePreset("a");
        var b = MakePreset("b");
        _sut.SavePreset(a);
        _sut.SavePreset(b);

        // Update by ID — should NOT trigger limit.
        a.Name = "a-renamed";
        var r = _sut.SavePreset(a);

        Assert.AreEqual(SaveResult.Saved, r);
        Assert.AreEqual(2, _sut.Presets.Count);
    }

    [TestMethod]
    public void GetPresetById_Existing_ReturnsPreset()
    {
        var p = MakePreset("x");
        _sut.SavePreset(p);

        var got = _sut.GetPresetById(p.Id);

        Assert.IsNotNull(got);
        Assert.AreEqual("x", got.Name);
    }

    [TestMethod]
    public void GetPresetById_Missing_ReturnsNull()
    {
        Assert.IsNull(_sut.GetPresetById("nope"));
    }

    [TestMethod]
    public void DeletePreset_Existing_RemovesAndReturnsTrue()
    {
        var p = MakePreset();
        _sut.SavePreset(p);

        var r = _sut.DeletePreset(p.Id);

        Assert.IsTrue(r);
        Assert.AreEqual(0, _sut.Presets.Count);
    }

    [TestMethod]
    public void DeletePreset_Missing_ReturnsFalse()
    {
        Assert.IsFalse(_sut.DeletePreset("nope"));
    }

    [TestMethod]
    public void OnGameLoaded_ReplacesPresets()
    {
        _sut.SavePreset(MakePreset("old"));

        var fresh = new List<HoNFormationPreset> { MakePreset("loaded1"), MakePreset("loaded2") };
        _sut.OnGameLoaded(fresh);

        Assert.AreEqual(2, _sut.Presets.Count);
        Assert.AreEqual("loaded1", _sut.Presets[0].Name);
    }

    [TestMethod]
    public void OnGameLoaded_NullList_ResetsToEmpty()
    {
        _sut.SavePreset(MakePreset("old"));

        _sut.OnGameLoaded(null);

        Assert.AreEqual(0, _sut.Presets.Count);
    }

    [TestMethod]
    public void GetPresetsForSaving_ReturnsCopy()
    {
        var p = MakePreset();
        _sut.SavePreset(p);

        var snap = _sut.GetPresetsForSaving();
        snap.Clear();

        Assert.AreEqual(1, _sut.Presets.Count, "Mutating snapshot must not affect service state");
    }
}
