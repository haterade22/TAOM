using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The harness's module inventory, pinned on a throwaway game folder (no game needed): which modules an explicit
/// TAOM_XMLMERGE_MODULES asks for, and which of the requested modules the folder does not have.
/// </summary>
[TestClass]
public class LiveMergeListBuilderTests
{
    private string _gameDir = "";

    [TestInitialize]
    public void CreateGameFolder()
    {
        _gameDir = Path.Combine(Path.GetTempPath(), "taom-livelists-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_gameDir, "Modules"));
    }

    [TestCleanup]
    public void DeleteGameFolder()
    {
        if (Directory.Exists(_gameDir))
            Directory.Delete(_gameDir, recursive: true);
    }

    [TestMethod]
    public void Select_NoEnvironmentValue_IsTheDefaultOrderAndNotExplicit()
    {
        // Arrange and Act
        var selection = LiveMergeListBuilder.Select(null);

        // Assert
        CollectionAssert.AreEqual(LiveMergeListBuilder.DefaultModuleOrder.Split(';'), selection.Modules.ToList());
        Assert.IsFalse(selection.Explicit);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Select_ABlankEnvironmentValue_IsTheDefaultOrderAndNotExplicit(string value)
    {
        // Arrange and Act
        var selection = LiveMergeListBuilder.Select(value);

        // Assert
        CollectionAssert.AreEqual(LiveMergeListBuilder.DefaultModuleOrder.Split(';'), selection.Modules.ToList());
        Assert.IsFalse(selection.Explicit);
    }

    [DataTestMethod]
    [DataRow("Native; TAOM ;;TAOM_Map;", "Native,TAOM,TAOM_Map")]
    [DataRow("Native; ;TAOM", "Native,TAOM")]
    [DataRow("Native;TAOM; ", "Native,TAOM")]
    public void Select_AnEnvironmentValue_IsExplicitTrimmedAndSplitOnSemicolons(string value, string expected)
    {
        // Arrange and Act: a segment of white space only (a pasted "; ") names no module, as an empty one does; kept, it
        // would be a missing module with no name in the gate's message.
        var selection = LiveMergeListBuilder.Select(value);

        // Assert
        CollectionAssert.AreEqual(expected.Split(','), selection.Modules.ToList());
        Assert.IsTrue(selection.Explicit);
    }

    [TestMethod]
    public void Build_ARequestedModuleHasNoSubModuleXml_IsListedMissingAndTheRestStillBuild()
    {
        // Arrange
        WriteModule("Present", "Things", "things");

        // Act
        var result = LiveMergeListBuilder.Build(_gameDir, "Campaign", new[] { "Present", "Absent" });

        // Assert
        CollectionAssert.AreEqual(new[] { "Absent" }, result.Missing);
        CollectionAssert.AreEqual(new[] { "Present" }, result.Modules);
        Assert.AreEqual(1, result.Lists.Count);
        Assert.AreEqual("Things", result.Lists[0].Id);
    }

    [TestMethod]
    public void Build_EveryRequestedModuleExists_ListsNoneMissing()
    {
        // Arrange
        WriteModule("First", "Things", "things");
        WriteModule("Second", "Things", "things");

        // Act
        var result = LiveMergeListBuilder.Build(_gameDir, "Campaign", new[] { "First", "Second" });

        // Assert
        Assert.AreEqual(0, result.Missing.Count);
        CollectionAssert.AreEqual(new[] { "First", "Second" }, result.Modules);
        Assert.AreEqual(2, result.Lists[0].Entries.Count);
    }

    private void WriteModule(string name, string xmlId, string xmlPath)
    {
        string root = Path.Combine(_gameDir, "Modules", name);
        Directory.CreateDirectory(Path.Combine(root, "ModuleData"));
        File.WriteAllText(Path.Combine(root, "SubModule.xml"),
            "<Module><Xmls><XmlNode><XmlName id=\"" + xmlId + "\" path=\"" + xmlPath + "\" /></XmlNode></Xmls></Module>");
        File.WriteAllText(Path.Combine(root, "ModuleData", xmlPath + ".xml"), "<" + xmlId + " />");
    }
}
