using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LoadTimeStamps;

[TestClass]
public class LoadXmlStampServiceTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private LoadXmlStampService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _sut = new LoadXmlStampService(_clock, _logger);
    }

    private static List<Tuple<string, string>> Files(params string[] paths) =>
        paths.Select(p => Tuple.Create(p, p == "" ? "" : "x")).ToList();

    private sealed class ThrowingClock : IStampClock
    {
        public long Now => throw new InvalidOperationException("clock broke");
        public long Frequency => 1000;
    }

    [TestMethod]
    public void End_WithoutAMerge_LogsNoneForMergeAndObjects()
    {
        var call = _sut.Begin("Items", "Campaign");
        _clock.Advance(3);
        _sut.End(call, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=Items files=0 ms=3.00 xslt=0 merge_ms=none objects_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void End_AfterAMerge_SplitsMergeAndObjectTime()
    {
        var call = _sut.Begin("NPCCharacters", "Campaign");
        _clock.Advance(40);
        _sut.MergeFinished(Files("a", "", "b"), new List<string> { "", "", "t.xslt" });
        _clock.Advance(15);
        _sut.End(call, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=NPCCharacters files=2 ms=55.00 xslt=1 merge_ms=40.00 objects_ms=15.00 result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void CountFiles_SkipsEmptyAndNullPaths_AndANullListIsZero()
    {
        var list = new List<Tuple<string, string>> { Tuple.Create("a", "x"), Tuple.Create("", ""), Tuple.Create<string, string>(null!, "x"), Tuple.Create("b", "x") };
        list.Add(null!);

        Assert.AreEqual(2, LoadXmlStampService.CountFiles(list));
        Assert.AreEqual(0, LoadXmlStampService.CountFiles(null));
    }

    [TestMethod]
    public void CountXslt_IgnoresIndexZeroAndEmptyEntries()
    {
        Assert.AreEqual(2, LoadXmlStampService.CountXslt(new List<string> { "first.xslt", "", "a.xslt", null!, "b.xslt" }));
        Assert.AreEqual(0, LoadXmlStampService.CountXslt(new List<string> { "only.xslt" }));
        Assert.AreEqual(0, LoadXmlStampService.CountXslt(null));
    }

    [TestMethod]
    public void End_WithAnException_LogsItsTypeNameAndCountsItFailed()
    {
        var call = _sut.Begin("Items", "Campaign");
        _clock.Advance(2);
        _sut.End(call, new System.Xml.XmlException("bad"));
        _sut.LogSummary();

        Assert.AreEqual("INFO [LoadXml] id=Items files=0 ms=2.00 xslt=0 merge_ms=none objects_ms=none result=XmlException", _logger.Lines[0]);
        StringAssert.EndsWith(_logger.Lines[1], " failed=1");
    }

    [TestMethod]
    public void MergeFinished_WithNoCallInFlight_IsIgnored()
    {
        _sut.MergeFinished(Files("a"), new List<string> { "" });

        var call = _sut.Begin("Items", "Campaign");
        _clock.Advance(4);
        _sut.End(call, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=Items files=0 ms=4.00 xslt=0 merge_ms=none objects_ms=none result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void MergeFinished_Twice_KeepsTheFirst()
    {
        var call = _sut.Begin("Items", "Campaign");
        _clock.Advance(10);
        _sut.MergeFinished(Files("a"), new List<string> { "" });
        _clock.Advance(10);
        _sut.MergeFinished(Files("a", "b", "c"), new List<string> { "", "x", "y" });
        _clock.Advance(5);
        _sut.End(call, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=Items files=1 ms=25.00 xslt=0 merge_ms=10.00 objects_ms=15.00 result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void NestedCalls_AttributeTheMergeToTheInnerCall_AndRestoreTheOuter()
    {
        var outer = _sut.Begin("Outer", "Campaign");
        _clock.Advance(10);
        var inner = _sut.Begin("Inner", "Campaign");
        _clock.Advance(10);
        _sut.MergeFinished(Files("a"), new List<string> { "" });
        _clock.Advance(5);
        _sut.End(inner, null);
        _clock.Advance(5);
        _sut.MergeFinished(Files("a", "b"), new List<string> { "", "" });
        _clock.Advance(10);
        _sut.End(outer, null);

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] id=Inner files=1 ms=15.00 xslt=0 merge_ms=10.00 objects_ms=5.00 result=ok",
            "INFO [LoadXml] id=Outer files=2 ms=40.00 xslt=0 merge_ms=30.00 objects_ms=10.00 result=ok",
        }, _logger.Lines);
    }

    [TestMethod]
    public void LogSummary_AggregatesSinceTheLastSummary_ThenResets()
    {
        var a = _sut.Begin("Items", "Campaign");
        _clock.Advance(30);
        _sut.MergeFinished(Files("a", "b"), new List<string> { "", "x" });
        _clock.Advance(10);
        _sut.End(a, null);
        var b = _sut.Begin("NPCCharacters", "Campaign");
        _clock.Advance(50);
        _sut.MergeFinished(Files("a", "b", "c"), new List<string> { "", "", "" });
        _clock.Advance(20);
        _sut.End(b, null);
        var c = _sut.Begin("GameText", "Campaign");
        _clock.Advance(5);
        _sut.End(c, null);
        _logger.Lines.Clear();

        _sut.LogSummary();
        _sut.LogSummary();

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] summary game=Campaign calls=3 files=5 xslt=1 ms=115.00 merge_ms=80.00 objects_ms=30.00 max_ms=70.00 max_id=NPCCharacters failed=0",
            "INFO [LoadXml] summary game=none calls=0 files=0 xslt=0 ms=0.00 merge_ms=0.00 objects_ms=0.00 max_ms=0.00 max_id=none failed=0",
        }, _logger.Lines);
    }

    [TestMethod]
    public void LogSummary_WithNoCalls_LogsZerosAndNone()
    {
        _sut.LogSummary();

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] summary game=none calls=0 files=0 xslt=0 ms=0.00 merge_ms=0.00 objects_ms=0.00 max_ms=0.00 max_id=none failed=0",
        }, _logger.Lines);
    }

    [TestMethod]
    public void LogSummary_GameIsTheLastNonEmptyGameType()
    {
        _sut.End(_sut.Begin("A", "Campaign"), null);
        _sut.End(_sut.Begin("B", "CustomGame"), null);
        _sut.End(_sut.Begin("C", ""), null);
        _sut.End(_sut.Begin("D", null), null);
        _logger.Lines.Clear();

        _sut.LogSummary();

        StringAssert.StartsWith(_logger.Lines[0], "INFO [LoadXml] summary game=CustomGame calls=4 ");
    }

    [TestMethod]
    public void LogSummary_ATie_NamesTheFirstSlowestCall()
    {
        var a = _sut.Begin("First", "Campaign");
        _clock.Advance(7);
        _sut.End(a, null);
        var b = _sut.Begin("Second", "Campaign");
        _clock.Advance(7);
        _sut.End(b, null);
        _logger.Lines.Clear();

        _sut.LogSummary();

        StringAssert.Contains(_logger.Lines[0], "max_ms=7.00 max_id=First ");
    }

    [TestMethod]
    public void End_NullCall_DoesNothing()
    {
        _sut.End(null, new InvalidOperationException("x"));
        _sut.LogSummary();

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadXml] summary game=none calls=0 files=0 xslt=0 ms=0.00 merge_ms=0.00 objects_ms=0.00 max_ms=0.00 max_id=none failed=0",
        }, _logger.Lines);
    }

    [TestMethod]
    public void End_NullId_PrintsNull()
    {
        var call = _sut.Begin(null, "Campaign");
        _sut.End(call, null);

        StringAssert.StartsWith(_logger.Lines[0], "INFO [LoadXml] id=null files=0 ");
    }

    [TestMethod]
    public void End_WhenTheCallLineFails_TheSummaryStillCountsTheCall()
    {
        _logger.OnLog = line => { if (line.StartsWith("INFO [LoadXml] id=")) throw new InvalidOperationException("disk full"); };
        var call = _sut.Begin("Items", "Campaign");
        _clock.Advance(9);
        _sut.End(call, null);

        _sut.LogSummary();

        StringAssert.StartsWith(_logger.Lines.Last(), "INFO [LoadXml] summary game=Campaign calls=1 files=0 xslt=0 ms=9.00 ");
    }

    [TestMethod]
    public void Begin_WhenTheClockThrows_WarnsOnceAndReturnsNull()
    {
        var sut = new LoadXmlStampService(new ThrowingClock(), _logger);

        Assert.IsNull(sut.Begin("Items", "Campaign"));
        Assert.IsNull(sut.Begin("NPCCharacters", "Campaign"));

        CollectionAssert.AreEqual(new[]
        {
            "WARN [LoadXml] stamp fault, some [LoadXml] lines may be missing this session: InvalidOperationException: clock broke",
        }, _logger.Lines);
    }
}
