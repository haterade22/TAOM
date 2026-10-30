using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Quests;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

[TestClass]
public class QuestLogDeferServiceTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private IQuestJournalAdapter _adapter = null!;
    private QuestLogDeferService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock { Now = 100 };
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.QuestLogFastOpen.Returns(true);
        _adapter = Substitute.For<IQuestJournalAdapter>();
        _adapter.IsBound.Returns(true);
        _sut = new QuestLogDeferService(_settings, _adapter, _clock, _logger);
    }

    private static object Item() => new object();

    // ---- TryDefer: when a journal item's stages are skipped ----

    [TestMethod]
    public void TryDefer_InScopeOnAndBound_DefersAndMarksTheItem()
    {
        var item = Item();
        _sut.BeginOpen();

        Assert.IsTrue(_sut.TryDefer(item, true));
        Assert.IsTrue(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void TryDefer_ToggleOff_DoesNotDeferOrMark()
    {
        _settings.QuestLogFastOpen.Returns(false);
        var item = Item();
        _sut.BeginOpen();

        Assert.IsFalse(_sut.TryDefer(item, true));
        Assert.IsFalse(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void TryDefer_AdapterNotBound_DoesNotDefer()
    {
        _adapter.IsBound.Returns(false);
        var item = Item();
        _sut.BeginOpen();

        Assert.IsFalse(_sut.TryDefer(item, true));
        Assert.IsFalse(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void BeginOpen_AdapterIsBoundThrows_DoesNotDefer_AndEndOpenReturnsDepthToZero()
    {
        var throwing = true;
        _adapter.IsBound.Returns(_ => throwing ? throw new InvalidOperationException("engine gone") : true);
        var item = Item();

        _sut.BeginOpen();

        Assert.IsFalse(_sut.TryDefer(item, true));
        Assert.IsNotNull(_sut.EndOpen(), "the throwing open was the outermost one");

        throwing = false;
        _sut.BeginOpen();
        Assert.IsTrue(_sut.TryDefer(Item(), true), "the next open is not nested and snapshots afresh");
        Assert.IsNotNull(_sut.EndOpen());
    }

    [TestMethod]
    public void TryDefer_OutOfScope_DoesNotDefer()
    {
        var item = Item();

        Assert.IsFalse(_sut.TryDefer(item, true));
        Assert.IsFalse(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void TryDefer_AfterTheOpenEnded_DoesNotDefer()
    {
        _sut.BeginOpen();
        _sut.EndOpen();

        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void TryDefer_ItemWithoutAJournalEntry_DoesNotDefer()
    {
        var item = Item();
        _sut.BeginOpen();

        Assert.IsFalse(_sut.TryDefer(item, false));
        Assert.IsFalse(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void TryDefer_WhileReplaying_DoesNotDefer()
    {
        var item = Item();
        var other = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        bool? deferredDuringReplay = null;
        _adapter.When(a => a.Rebuild(item)).Do(_ => deferredDuringReplay = _sut.TryDefer(other, true));

        _sut.EnsureBuilt(item);

        Assert.AreEqual(false, deferredDuringReplay);
        Assert.IsFalse(_sut.IsDeferred(other));
    }

    [TestMethod]
    public void TryDefer_ToggleSnapshottedAtBeginOpen_ALaterFlipDoesNotChangeTheOpen()
    {
        _sut.BeginOpen();
        _settings.QuestLogFastOpen.Returns(false);

        Assert.IsTrue(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void TryDefer_BoundStateSnapshottedAtBeginOpen()
    {
        _adapter.IsBound.Returns(false);
        _sut.BeginOpen();
        _adapter.IsBound.Returns(true);

        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void TryDefer_Disarmed_DoesNotDefer()
    {
        var bad = Item();
        _sut.BeginOpen();
        _sut.TryDefer(bad, true);
        _adapter.When(a => a.Rebuild(bad)).Do(_ => throw new InvalidOperationException("boom"));
        _sut.EnsureBuilt(bad);

        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void TryDefer_NestedBeginOpen_StaysInScopeUntilTheOuterEnds()
    {
        _sut.BeginOpen();
        _sut.BeginOpen();
        _sut.EndOpen();

        Assert.IsTrue(_sut.TryDefer(Item(), true));
        _sut.EndOpen();
        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void EndOpen_WithoutBeginOpen_NeverGoesNegative()
    {
        _sut.EndOpen();
        _sut.EndOpen();
        _sut.BeginOpen();

        Assert.IsTrue(_sut.TryDefer(Item(), true));
        _sut.EndOpen();
        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    [TestMethod]
    public void TryDefer_ScopeIsPerThread()
    {
        _sut.BeginOpen();
        bool? onOtherThread = null;
        var thread = new System.Threading.Thread(() => onOtherThread = _sut.TryDefer(Item(), true));
        thread.Start();
        thread.Join();

        Assert.AreEqual(false, onOtherThread);
    }

    // ---- EnsureBuilt ----

    [TestMethod]
    public void EnsureBuilt_DeferredItem_RebuildsOnceThenIsANoOp()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);

        _sut.EnsureBuilt(item);
        _sut.EnsureBuilt(item);

        _adapter.Received(1).Rebuild(item);
        Assert.IsFalse(_sut.IsDeferred(item));
    }

    [TestMethod]
    public void EnsureBuilt_NeverTouchesAnUnmarkedItem()
    {
        _sut.EnsureBuilt(Item());

        _adapter.DidNotReceive().Rebuild(Arg.Any<object>());
    }

    [TestMethod]
    public void EnsureBuilt_Null_DoesNothing()
    {
        _sut.EnsureBuilt(null);

        _adapter.DidNotReceive().Rebuild(Arg.Any<object>());
    }

    [TestMethod]
    public void EnsureBuilt_AfterTheOpenEnded_StillBuildsAMarkedItem()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _sut.EndOpen();

        _sut.EnsureBuilt(item);

        _adapter.Received(1).Rebuild(item);
    }

    [TestMethod]
    public void EnsureBuilt_ToggleTurnedOffMidOpen_StillBuilds()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _settings.QuestLogFastOpen.Returns(false);

        _sut.EnsureBuilt(item);

        _adapter.Received(1).Rebuild(item);
    }

    [TestMethod]
    public void EnsureBuilt_RebuildThrows_DisarmsLogsOnceAndTheItemStaysMarked()
    {
        var bad = Item();
        _sut.BeginOpen();
        _sut.TryDefer(bad, true);
        _adapter.When(a => a.Rebuild(bad)).Do(_ => throw new InvalidOperationException("boom"));

        _sut.EnsureBuilt(bad);
        _sut.EnsureBuilt(bad);

        Assert.IsTrue(_sut.Disarmed);
        Assert.IsTrue(_sut.IsDeferred(bad), "the item stays marked so the sort fallback keeps working");
        _adapter.Received(1).Rebuild(bad);
        var lines = _logger.Lines.Where(l => l.StartsWith("WARN") || l.StartsWith("ERROR")).ToList();
        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "InvalidOperationException");
    }

    [TestMethod]
    public void EnsureBuilt_AfterADisarm_OtherDeferredItemsStillBuild()
    {
        var bad = Item();
        var good = Item();
        _sut.BeginOpen();
        _sut.TryDefer(bad, true);
        _sut.TryDefer(good, true);
        _adapter.When(a => a.Rebuild(bad)).Do(_ => throw new InvalidOperationException("boom"));
        _sut.EnsureBuilt(bad);

        _sut.EnsureBuilt(good);

        _adapter.Received(1).Rebuild(good);
        Assert.IsFalse(_sut.IsDeferred(good));
    }

    [TestMethod]
    public void EnsureBuilt_RebuildThrows_NeverEscapes()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _adapter.When(a => a.Rebuild(item)).Do(_ => throw new InvalidOperationException("boom"));

        _sut.EnsureBuilt(item);
    }

    // ---- UpdatedFor / SortFallback ----

    [TestMethod]
    public void UpdatedFor_DeferredItem_AsksTheAdapter()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _adapter.AnyUnexamined(item).Returns(true);

        Assert.AreEqual(true, _sut.UpdatedFor(item));
    }

    [TestMethod]
    public void UpdatedFor_UnmarkedItem_IsNullAndNeverAsksTheAdapter()
    {
        Assert.IsNull(_sut.UpdatedFor(Item()));
        _adapter.DidNotReceive().AnyUnexamined(Arg.Any<object>());
    }

    [TestMethod]
    public void UpdatedFor_AdapterThrows_IsNullSoVanillasValueStands()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _adapter.AnyUnexamined(item).Throws(new InvalidOperationException("no tracker"));

        Assert.IsNull(_sut.UpdatedFor(item));
    }

    [TestMethod]
    public void SortFallback_DeferredItem_ReturnsTheFirstAndLastLogTheDeferralsSaw()
    {
        var item = Item();
        var first = new object();
        var middle = new object();
        var last = new object();
        _sut.BeginOpen();
        _sut.TryDefer(item, true, first);
        _sut.TryDefer(item, true, middle);
        _sut.TryDefer(item, true, last);

        Assert.AreSame(first, _sut.SortFallback(item, false));
        Assert.AreSame(last, _sut.SortFallback(item, true));
    }

    [TestMethod]
    public void SortFallback_ASingleEntry_IsBothTheFirstAndTheLast()
    {
        var item = Item();
        var only = new object();
        _sut.BeginOpen();
        _sut.TryDefer(item, true, only);

        Assert.AreSame(only, _sut.SortFallback(item, false));
        Assert.AreSame(only, _sut.SortFallback(item, true));
    }

    [TestMethod]
    public void SortFallback_UnmarkedItem_IsNull()
    {
        Assert.IsNull(_sut.SortFallback(Item(), false));
    }

    [TestMethod]
    public void SortFallback_ABuiltItem_IsNull()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _sut.EnsureBuilt(item);

        Assert.IsNull(_sut.SortFallback(item, true));
    }

    // ---- The open's detail ----

    [TestMethod]
    public void EndOpen_ReportsTheCountsAndTheToggle()
    {
        _settings.LogScreenOpenTimes.Returns(true);
        _sut.BeginOpen();
        var a = Item();
        var b = Item();
        _adapter.AnyUnexamined(a).Returns(true);
        var start1 = _sut.BeginItem();
        _clock.Advance(4);
        _sut.TryDefer(a, true);
        _sut.TryDefer(a, true);
        _sut.TryDefer(a, true);
        _sut.UpdatedFor(a);
        _sut.EndItem(start1);
        var start2 = _sut.BeginItem();
        _clock.Advance(6);
        _sut.TryDefer(b, true);
        _sut.EndItem(start2);

        var detail = _sut.EndOpen();

        Assert.AreEqual("fast open on; old=2, deferred=2, entries=4, updated=1, bound, journal items 10.0 ms", detail);
    }

    [TestMethod]
    public void EndOpen_ToggleOffUnbound_SaysSoAndCountsNothingDeferred()
    {
        _settings.QuestLogFastOpen.Returns(false);
        _adapter.IsBound.Returns(false);
        _sut.BeginOpen();
        _sut.EndItem(_sut.BeginItem());

        var detail = _sut.EndOpen();

        Assert.AreEqual("fast open off; old=1, deferred=0, entries=0, updated=0, unbound, journal items 0.0 ms", detail);
    }

    [TestMethod]
    public void BeginItem_TimerOff_NeverReadsTheClock()
    {
        _settings.LogScreenOpenTimes.Returns(false);
        _clock.ThrowOnRead = 1;
        _sut.BeginOpen();

        var start = _sut.BeginItem();
        _sut.EndItem(start);

        Assert.AreEqual(0, start);
    }

    [TestMethod]
    public void EndOpen_Nested_OnlyTheOuterReturnsADetail()
    {
        _sut.BeginOpen();
        _sut.BeginOpen();

        Assert.IsNull(_sut.EndOpen());
        Assert.IsNotNull(_sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_WithoutBeginOpen_IsNull()
    {
        Assert.IsNull(_sut.EndOpen());
    }

    [TestMethod]
    public void BeginOpen_ResetsTheCountsOfThePreviousOpen()
    {
        _sut.BeginOpen();
        _sut.TryDefer(Item(), true);
        _sut.EndOpen();
        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(), "fast open on; old=0, deferred=0, entries=0");
    }

    [TestMethod]
    public void BeginItem_OutOfScope_CountsNothing()
    {
        var start = _sut.BeginItem();
        _sut.EndItem(start);
        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(), "fast open on; old=0");
    }

    [TestMethod]
    public void EndItem_WithoutAPrefix_AddsNothing()
    {
        _sut.BeginOpen();
        _sut.EndItem(0);

        StringAssert.EndsWith(_sut.EndOpen(), "journal items 0.0 ms");
    }

    [TestMethod]
    public void EndOpen_ADisarmedProcess_SaysFastOpenOffEvenWithTheToggleOnAndBound()
    {
        var bad = Item();
        _sut.BeginOpen();
        _sut.TryDefer(bad, true);
        _adapter.When(a => a.Rebuild(bad)).Do(_ => throw new InvalidOperationException("boom"));
        _sut.EnsureBuilt(bad);
        _sut.EndOpen();

        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(), "fast open off; ");
    }

    [TestMethod]
    public void EndOpen_ToggleOnButUnbound_SaysFastOpenOff()
    {
        _adapter.IsBound.Returns(false);
        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(), "fast open off; ");
    }

    [TestMethod]
    public void UpdatedFor_DuringTheReplay_IsNotCountedInTheOpensUpdatedTotal()
    {
        var item = Item();
        _sut.BeginOpen();
        _sut.TryDefer(item, true);
        _adapter.AnyUnexamined(item).Returns(true);
        _adapter.When(a => a.Rebuild(item)).Do(_ => _sut.UpdatedFor(item));

        _sut.EnsureBuilt(item);

        StringAssert.Contains(_sut.EndOpen(), "updated=0");
    }

    [TestMethod]
    public void BeginOpen_AdapterNoLongerBound_TheNextOpenDoesNotDefer()
    {
        _sut.BeginOpen();
        Assert.IsTrue(_sut.TryDefer(Item(), true));
        _sut.EndOpen();
        _adapter.IsBound.Returns(false);

        _sut.BeginOpen();

        Assert.IsFalse(_sut.TryDefer(Item(), true));
    }

    // ---- Install ----

    [TestMethod]
    public void Install_Bound_LogsTheInstallLineWithTheToggle()
    {
        _adapter.Bind().Returns((string?)null);

        var problem = _sut.Install();

        Assert.IsNull(problem);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "fast open on");
    }

    [TestMethod]
    public void Install_BindFails_LogsAWarningNamingTheReason()
    {
        _adapter.Bind().Returns("PopulateQuestLog not found");

        var problem = _sut.Install();

        Assert.AreEqual("PopulateQuestLog not found", problem);
        StringAssert.StartsWith(_logger.Lines.Single(), "WARN");
        StringAssert.Contains(_logger.Lines[0], "PopulateQuestLog not found");
    }

    [TestMethod]
    public void Install_BindThrows_LogsAWarningAndDoesNotThrow()
    {
        _adapter.Bind().Throws(new InvalidOperationException("reflection"));

        var problem = _sut.Install();

        StringAssert.Contains(problem, "InvalidOperationException");
        StringAssert.StartsWith(_logger.Lines.Single(), "WARN");
    }
}
