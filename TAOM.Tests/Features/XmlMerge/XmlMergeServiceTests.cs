using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.XmlMerge;

namespace TAOM.Tests.Features.XmlMerge;

/// <summary>
/// The XmlMerge service against a hand-written fake of the engine (plan 042): every decision reason, the exact order of
/// engine calls for each branch of the engine's CreateMergedXmlFile loop, the once-only reason lines, the DEBUG detail
/// line every fallback adds, the session disable and its negative case, the per-game summary and its reset, the
/// configuration header both ways, and the log level of every line. No engine type is touched, so this runs on hosted
/// CI.
/// </summary>
[TestClass]
public class XmlMergeServiceTests
{
    private const string Xsd = "Things.xsd";

    private string _folder = "";
    private string _drop = "";
    private string _touch = "";
    private RecordingXmlMergeEngine _engine = null!;
    private IModLogger _logger = null!;
    private XmlMergeService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "taom-xmlmerge-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _drop = Path.Combine(_folder, "drop.xslt");
        File.WriteAllText(_drop, XmlMergeFixtures.Drop);
        _touch = Path.Combine(_folder, "touch.xslt");
        File.WriteAllText(_touch, XmlMergeFixtures.Touch);

        _engine = new RecordingXmlMergeEngine();
        _engine.Files["a.xml"] = "<Things><Thing id=\"a\" /><Thing id=\"b\" label=\"first\" /></Things>";
        _engine.Files["b.xml"] = "<Things><Thing id=\"b\" label=\"second\" /></Things>";
        _engine.Files["c.xml"] = "<Things><Thing id=\"c\" /></Things>";
        _logger = Substitute.For<IModLogger>();
        _service = new XmlMergeService(_engine, new XsltTransformCache(), _logger);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    // ---- Decide ----------------------------------------------------------------------------

    [TestMethod]
    public void Decide_SkipValidation_ReturnsSkipValidation()
    {
        Assert.AreEqual("skip-validation", _service.Decide(skipValidation: true, entryCount: 3));
    }

    [TestMethod]
    public void Decide_BindingProblem_ReturnsBindings()
    {
        _engine.BindingProblem = "MBObjectManager.MergeElements not found";

        Assert.AreEqual("bindings", _service.Decide(skipValidation: false, entryCount: 3));
    }

    [TestMethod]
    public void Decide_EmptyList_ReturnsEmptyList()
    {
        Assert.AreEqual("empty-list", _service.Decide(skipValidation: false, entryCount: 0));
    }

    [TestMethod]
    public void Decide_ForeignPatch_ReturnsForeignPatch()
    {
        _engine.Foreign.Add("com.other prefix MBObjectManager.CreateMergedXmlFile");

        Assert.AreEqual("foreign-patch", _service.Decide(skipValidation: false, entryCount: 3));
    }

    [TestMethod]
    public void Decide_AfterSessionDisable_ReturnsDisabled()
    {
        DisableForTheSession();

        Assert.AreEqual("disabled", _service.Decide(skipValidation: false, entryCount: 3));
    }

    [TestMethod]
    public void Decide_AllClear_ReturnsNull()
    {
        Assert.IsNull(_service.Decide(skipValidation: false, entryCount: 3));
    }

    // ---- The loop --------------------------------------------------------------------------

    [TestMethod]
    public void MergeFast_ThreeFilesNoXslt_ConvertsTheAccumulatorOnceEachWay()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", Xsd), ("c.xml", Xsd)), Xslts("", "", ""));

        CollectionAssert.AreEqual(
            new[] { "load a.xml", "load b.xml", "toX", "toX", "merge", "load c.xml", "toX", "merge", "toXml" },
            _engine.Calls);
        Assert.AreEqual(4, result.SelectNodes("/Things/Thing")!.Count);
    }

    [TestMethod]
    public void MergeFast_XsltBesideSecondFile_TransformsBeforeLoadingThatFile()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", _drop));

        // Applied before b.xml merges, the drop removes a.xml's b and keeps b.xml's; applied after, it would remove both.
        CollectionAssert.AreEqual(new[] { "load a.xml", "load b.xml", "toX", "toX", "merge", "toXml" }, _engine.Calls);
        var bs = result.SelectNodes("/Things/Thing[@id='b']")!;
        Assert.AreEqual(1, bs.Count);
        Assert.AreEqual("second", ((XmlElement)bs[0]!).GetAttribute("label"));
    }

    [TestMethod]
    public void MergeFast_FirstEntryXslt_IsNeverApplied()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts(_drop, ""));

        Assert.AreEqual(2, result.SelectNodes("/Things/Thing[@id='b']")!.Count, "index 0's XSLT must not run");
    }

    [TestMethod]
    public void MergeFast_SingleEntry_ReturnsTheLoadedDocumentUnconverted()
    {
        var result = Merge(Entries(("a.xml", Xsd)), Xslts(_drop));

        CollectionAssert.AreEqual(new[] { "load a.xml" }, _engine.Calls);
        Assert.IsTrue(ReferenceEquals(_engine.Loaded[0], result));
    }

    [TestMethod]
    public void MergeFast_LastStepIsXslt_ReturnsTheTransformOutputUnconverted()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", Xsd), ("", "")), Xslts("", "", _drop));

        CollectionAssert.AreEqual(new[] { "load a.xml", "load b.xml", "toX", "toX", "merge", "toXml" }, _engine.Calls);
        Assert.AreEqual(0, result.SelectNodes("/Things/Thing[@id='b']")!.Count, "the result is the transform's output");
    }

    [TestMethod]
    public void MergeFast_EmptyXsdPath_AppendsWithoutMergeElements()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", "")), Xslts("", ""));

        CollectionAssert.AreEqual(new[] { "load a.xml", "load b.xml", "toX", "toX", "toXml" }, _engine.Calls);
        Assert.AreEqual(3, result.SelectNodes("/Things/Thing")!.Count);
    }

    [TestMethod]
    public void MergeFast_EmptyFilePath_AppliesItsXsltAndLoadsNothing()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("", "")), Xslts("", _drop));

        CollectionAssert.AreEqual(new[] { "load a.xml" }, _engine.Calls);
        Assert.AreEqual(0, result.SelectNodes("/Things/Thing[@id='b']")!.Count);
    }

    [TestMethod]
    public void MergeFast_TwoXsltsInARow_DoNotConvertBetweenThem()
    {
        var result = Merge(Entries(("a.xml", Xsd), ("b.xml", Xsd), ("", ""), ("", "")), Xslts("", "", _drop, _touch));

        CollectionAssert.AreEqual(new[] { "load a.xml", "load b.xml", "toX", "toX", "merge", "toXml" }, _engine.Calls);
        Assert.AreEqual(0, result.SelectNodes("/Things/Thing[@id='b']")!.Count);
        Assert.AreEqual(1, result.SelectNodes("/Things/Thing[@touched='yes']")!.Count);
    }

    // ---- Outcomes and lines ----------------------------------------------------------------

    [TestMethod]
    public void TryMergeFast_AllClear_LogsOneFastLineAndReturnsTheDocument()
    {
        var call = new XmlMergeCall();

        bool handled = _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, call, out var result);

        Assert.IsTrue(handled);
        Assert.IsTrue(call.Handled);
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result!.SelectNodes("/Things/Thing")!.Count);
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] type=Things files=2 xslt=0 ms=", StringComparison.Ordinal) &&
            s.Contains(" path=fast load_ms=") &&
            s.EndsWith(" xslt_compiles=0 xslt_cache_hits=0", StringComparison.Ordinal)));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
        _logger.DidNotReceiveWithAnyArgs().LogDebug(default!);
    }

    [TestMethod]
    public void TryMergeFast_SkipValidation_ReturnsFalseAndLogsNothingYet()
    {
        var call = new XmlMergeCall();

        bool handled = _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), true, call, out var result);

        Assert.IsFalse(handled);
        Assert.IsNull(result);
        Assert.AreEqual("skip-validation", call.VanillaReason);
        Assert.AreEqual(0, _engine.Calls.Count);
        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "the engine-path line comes from the finalizer");
    }

    [TestMethod]
    public void OnOriginalFinished_EnginePath_LogsTheVanillaLineWithTheReason()
    {
        var call = new XmlMergeCall();
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", _drop), true, call, out _);

        _service.OnOriginalFinished(call, null);

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] type=Things files=2 xslt=1 ms=", StringComparison.Ordinal) &&
            s.EndsWith(" path=vanilla reason=skip-validation result=ok", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryMergeFast_ForeignPatch_LogsTheStandAsideLineOnlyOnce()
    {
        _engine.Foreign.Add("com.other prefix MBObjectManager.CreateMergedXmlFile");

        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), false, new XmlMergeCall(), out _);
        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), false, new XmlMergeCall(), out _);

        _logger.Received(1).LogInfo(
            "[XmlMerge] fast path stands aside: com.other prefix MBObjectManager.CreateMergedXmlFile; " +
            "merges run the engine's own code while those patches are present");
        Assert.AreEqual(0, _engine.Calls.Count);
    }

    [TestMethod]
    public void TryMergeFast_AdapterThrows_ReturnsFalseAndLogsTheFailureOncePerType()
    {
        _engine.ThrowOnLoad = new InvalidOperationException("boom");
        var first = new XmlMergeCall();

        bool handled = _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, first, out var result);
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);

        Assert.IsFalse(handled);
        Assert.IsNull(result);
        Assert.AreEqual("fast-path-error", first.VanillaReason);
        Assert.AreEqual("InvalidOperationException", first.FastErrorType);
        _logger.Received(1).LogWarning(
            "[XmlMerge] fast path failed on type=Things: InvalidOperationException: boom; this merge re-runs the engine's own code");
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void OnOriginalFinished_FastFailedAndEngineSucceeded_DisablesForTheSession()
    {
        DisableForTheSession();

        Assert.AreEqual("disabled", _service.Decide(false, 2));
        _logger.Received(1).LogWarning(
            "[XmlMerge] fast path disabled for this session: it threw InvalidOperationException on type=Things where the engine's own merge succeeded");
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.EndsWith(" path=vanilla reason=fast-path-error result=ok", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void OnOriginalFinished_FastFailedAndEngineThrew_StaysEnabled()
    {
        _engine.ThrowOnLoad = new InvalidOperationException("boom");
        var call = new XmlMergeCall();
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, call, out _);

        _service.OnOriginalFinished(call, new FileNotFoundException("missing"));

        Assert.IsNull(_service.Decide(false, 2));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("disabled for this session")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.EndsWith(" path=vanilla reason=fast-path-error result=FileNotFoundException", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LogWindowSummary_AfterMerges_LogsTotalsAndMaximaThenResets()
    {
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", _drop), false, new XmlMergeCall(), out _);
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd), ("c.xml", Xsd)), Xslts("", _drop, ""), false, new XmlMergeCall(), out _);

        _service.LogWindowSummary("Campaign");
        _service.LogWindowSummary(null);

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=Campaign merges=2 fast=2 vanilla=0 files=5 ms=", StringComparison.Ordinal) &&
            s.Contains(" max_type=Things ") &&
            s.EndsWith(" xslt_compiles=1 xslt_cache_hits=1 fast_path=on", StringComparison.Ordinal)));
        _logger.Received(1).LogInfo(
            "[XmlMerge] summary game=none merges=0 fast=0 vanilla=0 files=0 ms=0 max_ms=0 max_type=none " +
            "xslt_compiles=0 xslt_cache_hits=0 fast_path=on");
    }

    [TestMethod]
    public void LogWindowSummary_EnginePathMerge_CountsInTotalsButNotInCacheCounters()
    {
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", _drop), false, new XmlMergeCall(), out _);
        var vanilla = new XmlMergeCall();
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd), ("c.xml", Xsd)), Xslts("", "", ""), true, vanilla, out _);
        _service.OnOriginalFinished(vanilla, null);

        _service.LogWindowSummary("CustomGame");

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=CustomGame merges=2 fast=1 vanilla=1 files=5 ms=", StringComparison.Ordinal) &&
            s.EndsWith(" xslt_compiles=1 xslt_cache_hits=0 fast_path=on", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LogConfigurationHeader_BindingsResolved_LogsReady()
    {
        _service.LogConfigurationHeader();

        _logger.Received(1).LogInfo(XmlMergeLines.Ready());
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void LogConfigurationHeader_BindingProblem_LogsOff()
    {
        _engine.BindingProblem = "MBObjectManager.MergeElements(XElement, XElement, string) not found";

        _service.LogConfigurationHeader();

        _logger.Received(1).LogWarning(
            "[XmlMerge] fast path off: MBObjectManager.MergeElements(XElement, XElement, string) not found; " +
            "every module XML merge runs the engine's own code");
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void TypeOf_FirstNonEmptyXsdPath_IsTheFileNameWithoutExtension()
    {
        var entries = Entries(("", ""), ("Modules/TAOM/ModuleData/lords.xml", "C:/Game/XmlSchemas/NPCCharacters.xsd"));

        Assert.AreEqual("NPCCharacters", XmlMergeService.TypeOf(entries));
    }

    [TestMethod]
    public void TypeOf_NoXsdPath_IsUnknown()
    {
        Assert.AreEqual("unknown", XmlMergeService.TypeOf(Entries(("a.xml", ""), ("", ""))));
    }

    [TestMethod]
    public void TypeOf_NullEntryBeforeTheXsd_IsSkipped()
    {
        var entries = new List<Tuple<string, string>> { null!, Tuple.Create("a.xml", "XmlSchemas/Items.xsd") };

        Assert.AreEqual("Items", XmlMergeService.TypeOf(entries));
    }

    [TestMethod]
    public void TypeOf_InvalidPathCharacters_IsUnknownWithoutThrowing()
    {
        Assert.AreEqual("unknown", XmlMergeService.TypeOf(Entries(("a.xml", "Things|1.xsd"))));
    }

    // ---- Review follow-ups (deep review 2026-10-02): branches no earlier test reached ----------

    [TestMethod]
    public void OnOriginalFinished_AfterAFastMerge_LogsNothingAndCountsOnce()
    {
        var call = new XmlMergeCall();
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, call, out _);

        // Harmony runs the void finalizer after every call, including one whose original the prefix skipped.
        _service.OnOriginalFinished(call, null);
        _service.LogWindowSummary("Campaign");

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains(" path=vanilla ")));
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=Campaign merges=1 fast=1 vanilla=0 files=2 ms=", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryMergeFast_LoggerThrowsOnTheFastLine_LetsTheEngineMergeAndCountsItOnceAsVanilla()
    {
        _logger.When(l => l.LogInfo(Arg.Is<string>(s => s.Contains(" path=fast "))))
            .Do(_ => throw new IOException("disk full"));
        var call = new XmlMergeCall();

        bool handled = _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, call, out var result);
        _service.OnOriginalFinished(call, null);
        _service.LogWindowSummary("Campaign");

        Assert.IsFalse(handled);
        Assert.IsNull(result);
        Assert.IsFalse(call.Handled);
        Assert.AreEqual("fast-path-error", call.VanillaReason);
        Assert.AreEqual("IOException", call.FastErrorType);
        _logger.Received(1).LogWarning(
            "[XmlMerge] fast path failed on type=Things: IOException: disk full; this merge re-runs the engine's own code");
        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=Campaign merges=1 fast=0 vanilla=1 ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TryMergeFast_NullList_ReturnsFalseWithTheEmptyListReason()
    {
        var call = new XmlMergeCall();

        bool handled = _service.TryMergeFast(null!, Xslts(""), false, call, out var result);

        Assert.IsFalse(handled);
        Assert.IsNull(result);
        Assert.AreEqual("empty-list", call.VanillaReason);
        Assert.AreEqual("unknown", call.Type);
        Assert.AreEqual(0, _engine.Calls.Count);
    }

    [TestMethod]
    public void TryMergeFast_SkipValidationWithAForeignPatch_ReportsSkipValidationAndNoStandAside()
    {
        _engine.Foreign.Add("com.other prefix MBObjectManager.CreateMergedXmlFile");
        var call = new XmlMergeCall();

        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), true, call, out _);

        Assert.AreEqual("skip-validation", call.VanillaReason);
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void TryMergeFast_ADifferentPatchSet_LogsAnotherStandAsideLine()
    {
        _engine.Foreign.Add("com.other prefix MBObjectManager.CreateMergedXmlFile");
        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), false, new XmlMergeCall(), out _);
        _engine.Foreign = new List<string> { "com.other prefix MBObjectManager.CreateMergedXmlFile", "com.third postfix MBObjectManager.ToXDocument" };
        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), false, new XmlMergeCall(), out _);
        _service.TryMergeFast(Entries(("a.xml", Xsd)), Xslts(""), false, new XmlMergeCall(), out _);

        _logger.Received(2).LogInfo(Arg.Is<string>(s => s.StartsWith("[XmlMerge] fast path stands aside: ", StringComparison.Ordinal)));
        _logger.Received(1).LogInfo(
            "[XmlMerge] fast path stands aside: com.other prefix MBObjectManager.CreateMergedXmlFile, " +
            "com.third postfix MBObjectManager.ToXDocument; merges run the engine's own code while those patches are present");
    }

    [TestMethod]
    public void TryMergeFast_ADifferentExceptionType_LogsAnotherFailureLine()
    {
        _engine.ThrowOnLoad = new InvalidOperationException("boom");
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);
        _engine.ThrowOnLoad = new XmlException("bad");
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);

        _logger.Received(2).LogWarning(Arg.Is<string>(s => s.StartsWith("[XmlMerge] fast path failed on type=Things: ", StringComparison.Ordinal)));
        _logger.Received(1).LogWarning(
            "[XmlMerge] fast path failed on type=Things: XmlException: bad; this merge re-runs the engine's own code");
    }

    [TestMethod]
    public void LogWindowSummary_BindingProblem_ReportsTheFastPathOff()
    {
        _engine.BindingProblem = "MBObjectManager.MergeElements not found";

        _service.LogWindowSummary("Campaign");

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=Campaign ", StringComparison.Ordinal) &&
            s.EndsWith(" fast_path=off", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void LogWindowSummary_AfterSessionDisable_ReportsTheFastPathDisabled()
    {
        DisableForTheSession();

        _service.LogWindowSummary("Campaign");

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.StartsWith("[XmlMerge] summary game=Campaign merges=1 fast=0 vanilla=1 ", StringComparison.Ordinal) &&
            s.EndsWith(" fast_path=disabled", StringComparison.Ordinal)));
    }

    // ---- The DEBUG detail line (the maintainer's review follow-up, 2026-10-03) ----------------

    [TestMethod]
    public void TryMergeFast_AdapterThrows_LogsTheFullExceptionAtDebugAfterTheWarning()
    {
        var boom = new InvalidOperationException("boom");
        _engine.ThrowOnLoad = boom;

        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);

        Assert.IsNotNull(boom.StackTrace, "the fake engine's throw gives the exception the stack this line has to carry");
        Received.InOrder(() =>
        {
            _logger.LogWarning(
                "[XmlMerge] fast path failed on type=Things: InvalidOperationException: boom; this merge re-runs the engine's own code");
            _logger.LogDebug("[XmlMerge] fast path failure detail on type=Things: " + boom);
        });
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void TryMergeFast_TheSameExceptionTypeTwice_LogsOneWarningAndADebugLineForEachFallback()
    {
        var first = new InvalidOperationException("first");
        var second = new InvalidOperationException("second");

        _engine.ThrowOnLoad = first;
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);
        _engine.ThrowOnLoad = second;
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);

        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogDebug("[XmlMerge] fast path failure detail on type=Things: " + first);
        _logger.Received(1).LogDebug("[XmlMerge] fast path failure detail on type=Things: " + second);
    }

    [TestMethod]
    public void TryMergeFast_LoggerThrowsOnTheFastLine_LogsTheLoggersExceptionAtDebug()
    {
        var diskFull = new IOException("disk full");
        _logger.When(l => l.LogInfo(Arg.Is<string>(s => s.Contains(" path=fast "))))
            .Do(_ => throw diskFull);

        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, new XmlMergeCall(), out _);

        // The outer catch settles this fallback through the same method as the inner one.
        _logger.Received(1).LogDebug("[XmlMerge] fast path failure detail on type=Things: " + diskFull);
    }

    // ---- helpers ---------------------------------------------------------------------------

    private void DisableForTheSession()
    {
        _engine.ThrowOnLoad = new InvalidOperationException("boom");
        var call = new XmlMergeCall();
        _service.TryMergeFast(Entries(("a.xml", Xsd), ("b.xml", Xsd)), Xslts("", ""), false, call, out _);
        _service.OnOriginalFinished(call, null);
        _engine.ThrowOnLoad = null;
    }

    private XmlDocument Merge(List<Tuple<string, string>> entries, List<string> xslts) =>
        _service.MergeFast(entries, xslts, false, new XmlMergeCounters());

    private static List<Tuple<string, string>> Entries(params (string File, string Xsd)[] entries) =>
        entries.Select(e => Tuple.Create(e.File, e.Xsd)).ToList();

    private static List<string> Xslts(params string[] xslts) => xslts.ToList();
}
