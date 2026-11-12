using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Gauntlet measures every visible child against the parent's whole space (DefaultLayout.MeasureChildren), so a StretchToParent
/// child of a CoverChildren parent with no MaxWidth or MaxHeight grows the parent to everything it is offered. It shipped three
/// times (lessons/localization-ui.md, #866; Fords of War 2026-10-31, where three panel backgrounds covered the whole scene).
/// This gate scans every prefab file under Main/_Module/GUI; prefab inserts built in C# with XmlDocument.LoadXml stay guarded by
/// their own feature tests. Grid parents (GridLayout.UpdateCellSizes gives their children a fixed cell) and parents with a max
/// size on the axis are exempt. The older files below held the fault before this gate: their counts may only fall, and
/// must be lowered here in the same change when they do. A new file, a higher count, a lower count or a stale entry fails.
/// </summary>
[TestClass]
public class GauntletStretchInCoverRatchetTests
{
    private static readonly string[] Grammar = { "Prefab", "Window", "Children", "ItemTemplate" };

    // Ignore case: git tracks the folder as PreFabs, and a checkout can hold it on disk as Prefabs.
    private static readonly Dictionary<string, int> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Main/_Module/GUI/PreFabs/MomentumView/MomentumView.xml"] = 5,
        ["Main/_Module/GUI/PreFabs/FacGen/PreBuildCharacterSelection.xml"] = 1,
    };

    [TestMethod]
    public void NoPrefab_GainsAStretchChildInsideACoverChildrenParent()
    {
        var root = RepoPaths.RepoPath("Main", "_Module", "GUI");
        var repo = RepoPaths.RepoPath();
        var failures = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories))
        {
            XElement document;
            try
            {
                document = XDocument.Load(file, LoadOptions.SetLineInfo).Root!;
            }
            catch (XmlException)
            {
                continue;
            }

            scanned++;
            var relative = file.Substring(repo.Length).TrimStart('\\', '/').Replace('\\', '/');
            seen.Add(relative);
            var lines = Offenders(document).ToList();
            var allowed = Known.TryGetValue(relative, out var known) ? known : 0;
            if (lines.Count > allowed)
                failures.Add($"{relative}: {lines.Count} (allowed {allowed}) at lines {string.Join(", ", lines)}");
            else if (lines.Count < allowed)
                failures.Add($"{relative}: {lines.Count} is below its baseline {allowed}; lower the baseline in Known");
        }

        foreach (var entry in Known.Keys.Where(k => !seen.Contains(k)))
            failures.Add($"{entry}: a Known entry matches no prefab file; remove it");

        Assert.IsTrue(scanned > 50, "the prefab folder was not found");
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void Offenders_CountAStretchChildOfACoverChildrenWidget()
    {
        var lines = Offenders(Parse("<Widget WidthSizePolicy=\"CoverChildren\"><Children><Widget WidthSizePolicy=\"StretchToParent\"/></Children></Widget>"));

        Assert.AreEqual(1, lines.Count);
    }

    [TestMethod]
    [DataRow("GridWidget")]
    [DataRow("NavigatableGridWidget")]
    public void Offenders_SkipAStretchChildOfAGridParent(string grid)
    {
        var lines = Offenders(Parse($"<{grid} WidthSizePolicy=\"CoverChildren\"><Children><Widget WidthSizePolicy=\"StretchToParent\"/></Children></{grid}>"));

        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void Offenders_SkipAParentWithAMaxWidthOrMaxHeightOnThatAxis()
    {
        Assert.AreEqual(0, Offenders(Parse("<Widget WidthSizePolicy=\"CoverChildren\" MaxWidth=\"300\"><Children><Widget WidthSizePolicy=\"StretchToParent\"/></Children></Widget>")).Count);
        Assert.AreEqual(0, Offenders(Parse("<Widget HeightSizePolicy=\"CoverChildren\" MaxHeight=\"300\"><Children><Widget HeightSizePolicy=\"StretchToParent\"/></Children></Widget>")).Count);
        Assert.AreEqual(1, Offenders(Parse("<Widget HeightSizePolicy=\"CoverChildren\" MaxWidth=\"300\"><Children><Widget HeightSizePolicy=\"StretchToParent\"/></Children></Widget>")).Count,
            "a max width does not cap the height axis");
    }

    [TestMethod]
    public void Offenders_CountAWidgetThatStretchesOnBothAxesOnce()
    {
        var lines = Offenders(Parse("<Widget WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"CoverChildren\"><Children><Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\"/></Children></Widget>"));

        Assert.AreEqual(1, lines.Count);
    }

    private static XElement Parse(string xml) => XDocument.Parse(xml, LoadOptions.SetLineInfo).Root!;

    internal static List<int> Offenders(XElement document)
    {
        var lines = new List<int>();
        foreach (var widget in document.DescendantsAndSelf().Where(e => !Grammar.Contains(e.Name.LocalName)))
        {
            var parent = widget.Parent;
            while (parent != null && Grammar.Contains(parent.Name.LocalName))
                parent = parent.Parent;
            if (parent == null || parent.Name.LocalName.EndsWith("GridWidget"))
                continue;

            if (Stretches(widget, parent, "WidthSizePolicy", "MaxWidth") || Stretches(widget, parent, "HeightSizePolicy", "MaxHeight"))
                lines.Add(((IXmlLineInfo)widget).LineNumber);
        }

        return lines;
    }

    private static bool Stretches(XElement widget, XElement parent, string axis, string max) =>
        (string?)widget.Attribute(axis) == "StretchToParent" && (string?)parent.Attribute(axis) == "CoverChildren"
        && parent.Attribute(max) == null;
}
