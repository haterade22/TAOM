using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.XmlMerge;
using TAOM.Tests.Migration;
using TaleWorlds.ObjectSystem;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// Byte equality between the engine's own <c>MBObjectManager.CreateMergedXmlFile</c> and the fast path, through the
/// real adapter, on fixtures that exercise every branch of the engine's loop (plan 042, Step 6). The engine runs
/// unpatched in this process; ClassInitialize checks that.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class XmlMergeEngineEquivalenceTests
{
    private static XmlMergeFixtures? _fixtures;
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _gameLoaded = GameAssemblies.EnsureLoaded();
        if (!_gameLoaded)
            return;

        _fixtures = XmlMergeFixtures.Create();
        XmlResource.ReadXsdFileAndExtractInformation(_fixtures.Xsd);

        var table = XmlResource.XsdElementDictionary[_fixtures.Xsd];
        CollectionAssert.AreEquivalent(new[] { "/Things", "/Things/Thing", "/Things/Thing/Part" }, table.Keys.ToArray());
        CollectionAssert.AreEqual(new[] { "id" }, table["/Things/Thing"].UniqueAttributes);
        Assert.AreEqual(0, table["/Things/Thing/Part"].UniqueAttributes.Count);

        var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile)));
        Assert.IsTrue(info == null || info.Prefixes.Count == 0, "the engine side must run unpatched");
    }

    [ClassCleanup]
    public static void Cleanup() => _fixtures?.Delete();

    [TestMethod]
    public void NoXslt_ThreeFiles_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), (f.B, f.Xsd), (f.C, f.Xsd)), Xslts("", "", ""), "three files");
    }

    [TestMethod]
    public void XsltBesideSecondFile_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), (f.B, f.Xsd)), Xslts("", f.DropXslt), "xslt beside b");
    }

    [TestMethod]
    public void XsltOnlyEntryInTheMiddle_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), ("", ""), (f.B, f.Xsd)), Xslts("", f.DropXslt, ""), "xslt-only entry");
    }

    [TestMethod]
    public void XsltIsTheLastStep_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), (f.B, f.Xsd), ("", "")), Xslts("", "", f.TouchXslt), "xslt last");
    }

    [TestMethod]
    public void FirstEntryXslt_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), (f.B, f.Xsd)), Xslts(f.DropXslt, ""), "first entry xslt");
    }

    [TestMethod]
    public void SingleEntry_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd)), Xslts(""), "single entry");
    }

    [TestMethod]
    public void EmptyXsdPath_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), (f.B, "")), Xslts("", ""), "empty xsd path");
    }

    [TestMethod]
    public void ReplaceWhileMerging_MatchesTheEngine()
    {
        var f = Fixtures();
        var fast = Same(Entries((f.A, f.Xsd), (f.C, f.Xsd)), Xslts("", ""), "replace while merging");

        StringAssert.Contains(fast, "_replaceWhileMerging=\"true\"", "the engine copies the marker into the merged output");
    }

    [TestMethod]
    public void TwoXsltsInARow_MatchesTheEngine()
    {
        var f = Fixtures();
        Same(Entries((f.A, f.Xsd), ("", ""), ("", ""), (f.B, f.Xsd)), Xslts("", f.DropXslt, f.TouchXslt, ""), "two xslts");
    }

    [TestMethod]
    public void MissingFile_ThrowsTheSameExceptionTypeAsTheEngine()
    {
        var f = Fixtures();
        var entries = Entries((f.A, f.Xsd), (f.Missing, f.Xsd));
        var xslts = Xslts("", "");

        var engine = Catch(() => MBObjectManager.CreateMergedXmlFile(entries, xslts, false));
        var fast = Catch(() => NewService().MergeFast(entries, xslts, false, new XmlMergeCounters()));

        Assert.IsNotNull(engine, "the engine should throw for a missing file");
        Assert.IsNotNull(fast, "the fast path should throw for a missing file");
        Assert.AreEqual(engine!.GetType(), fast!.GetType());
    }

    private static string Same(List<Tuple<string, string>> entries, List<string> xslts, string label)
    {
        string engine = MBObjectManager.CreateMergedXmlFile(entries, xslts, false).OuterXml;
        string fast = NewService().MergeFast(entries, xslts, false, new XmlMergeCounters()).OuterXml;
        XmlMergeAssert.SameDocument(engine, fast, label);
        return fast;
    }

    private static XmlMergeService NewService() =>
        new XmlMergeService(new XmlMergeEngineAdapter(), new XsltTransformCache(), Substitute.For<IModLogger>());

    private static XmlMergeFixtures Fixtures()
    {
        if (!_gameLoaded || _fixtures == null)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        return _fixtures!;
    }

    private static Exception? Catch(Func<XmlDocument> action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static List<Tuple<string, string>> Entries(params (string File, string Xsd)[] entries) =>
        entries.Select(e => Tuple.Create(e.File, e.Xsd)).ToList();

    private static List<string> Xslts(params string[] xslts) => xslts.ToList();
}
