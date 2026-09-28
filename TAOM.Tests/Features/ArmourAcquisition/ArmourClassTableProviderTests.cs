using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// armour_classes.xml is generated (tools/generate_armour_classes.py), but it ships in the module and a
/// hand edit or a half-written file must not poison the gate: an unknown class, a missing id, a
/// duplicate id or a piece that "upgrades" into itself is refused with a warning, and one summary
/// warning names the count. A missing or unreadable table leaves every piece to the engine-tier
/// fallback and says so.
/// </summary>
[TestClass]
public class ArmourClassTableProviderTests
{
    private string _tempDir = null!;
    private string _dir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_armour_classes_" + Guid.NewGuid().ToString("N"));
        _dir = Path.Combine(_tempDir, "armour_acquisition");
        Directory.CreateDirectory(_dir);
        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private System.Collections.Generic.IReadOnlyDictionary<string, ArmourClassEntry> Load(string? rows)
    {
        if (rows != null)
            File.WriteAllText(Path.Combine(_dir, "armour_classes.xml"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><ArmourClasses>" + rows + "</ArmourClasses>");
        return new ArmourClassTableProvider(_pathService, _logger).GetEntries();
    }

    [TestMethod]
    public void GetEntries_NoFile_IsEmptyAndWarns()
    {
        Assert.AreEqual(0, Load(null).Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("armour_classes.xml")));
    }

    [TestMethod]
    public void GetEntries_MalformedFile_IsEmptyAndLogsError()
    {
        File.WriteAllText(Path.Combine(_dir, "armour_classes.xml"), "<ArmourClasses><Item");

        Assert.AreEqual(0, new ArmourClassTableProvider(_pathService, _logger).GetEntries().Count);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetEntries_ValidRows_CarryClassAndNext()
    {
        var entries = Load("<Item id=\"a\" class=\"medium\" next=\"b\" /><Item id=\"b\" class=\"heavy\" />");

        Assert.AreEqual(ArmourClass.Medium, entries["a"].Class);
        Assert.AreEqual("b", entries["a"].NextItemId);
        Assert.IsNull(entries["b"].NextItemId);
    }

    [TestMethod]
    public void GetEntries_UnknownClass_IsRefused()
    {
        var entries = Load("<Item id=\"a\" class=\"mythril\" />");

        Assert.IsFalse(entries.ContainsKey("a"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("mythril")));
    }

    [TestMethod]
    public void GetEntries_MissingId_IsRefused()
    {
        var entries = Load("<Item class=\"light\" />");

        Assert.AreEqual(0, entries.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no id")));
    }

    [TestMethod]
    public void GetEntries_DuplicateId_KeepsTheFirst()
    {
        var entries = Load("<Item id=\"a\" class=\"light\" /><Item id=\"a\" class=\"elite\" />");

        Assert.AreEqual(ArmourClass.Light, entries["a"].Class);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("twice")));
    }

    [TestMethod]
    public void GetEntries_NextPointingAtItself_DropsTheLink()
    {
        var entries = Load("<Item id=\"a\" class=\"heavy\" next=\"a\" />");

        Assert.IsNull(entries["a"].NextItemId);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("itself")));
    }

    [TestMethod]
    public void GetEntries_IdsAreCaseSensitive_AsTheEngineResolvesThem()
    {
        var entries = Load("<Item id=\"Abc\" class=\"light\" />");

        Assert.IsTrue(entries.ContainsKey("Abc"));
        Assert.IsFalse(entries.ContainsKey("abc"));
    }

    [TestMethod]
    public void GetEntries_AnyRefusedRow_EmitsOneSummaryWarning()
    {
        Load("<Item id=\"a\" class=\"x\" /><Item id=\"b\" class=\"y\" />");

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("2 row(s)")));
    }
}
