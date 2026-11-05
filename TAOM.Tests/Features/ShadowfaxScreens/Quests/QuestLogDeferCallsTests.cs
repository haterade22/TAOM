using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Quests;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

/// <summary>The static facade the quest-log patches call: off while unwired, and one [ScreenOpen] line per open when the timer is on.</summary>
[TestClass]
public class QuestLogDeferCallsTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private IQuestJournalAdapter _adapter = null!;
    private QuestLogDeferService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock { Now = 100 };
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.QuestLogFastOpen.Returns(true);
        _settings.LogScreenOpenTimes.Returns(true);
        _adapter = Substitute.For<IQuestJournalAdapter>();
        _adapter.IsBound.Returns(true);
        _service = new QuestLogDeferService(_settings, _adapter, _clock, _logger);
        QuestLogDeferCalls.Initialize(_service);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, _settings));
    }

    [TestCleanup]
    public void Cleanup()
    {
        QuestLogDeferCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
    }

    [TestMethod]
    public void Unwired_EveryCallIsAHarmlessNoOp()
    {
        QuestLogDeferCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
        var item = new object();

        var call = QuestLogDeferCalls.BeginOpen();
        Assert.IsFalse(QuestLogDeferCalls.ShouldSkipPopulate(item, true, null));
        QuestLogDeferCalls.EndItem(QuestLogDeferCalls.BeginItem());
        QuestLogDeferCalls.EnsureBuilt(item);
        Assert.IsNull(QuestLogDeferCalls.UpdatedFor(item));
        Assert.IsNull(QuestLogDeferCalls.SortFallback(item, true));
        QuestLogDeferCalls.EndOpen(call, null);
    }

    [TestMethod]
    public void BeginOpenThenEndOpen_TimerOn_WritesOneQuestsOpenLineWithTheDetail()
    {
        var call = QuestLogDeferCalls.BeginOpen();
        _clock.Advance(25);
        QuestLogDeferCalls.EndOpen(call, null);

        var line = _logger.Lines.Single();
        StringAssert.StartsWith(line, "INFO [ScreenOpen] Quests open: 25.0 ms (fast open on; old=0, deferred=0, entries=0");
        StringAssert.Contains(line, "bound, journal items");
    }

    [TestMethod]
    public void EndOpen_AThrowingConstructor_NamesTheExceptionInTheLine()
    {
        var call = QuestLogDeferCalls.BeginOpen();

        QuestLogDeferCalls.EndOpen(call, new InvalidOperationException("x"));

        StringAssert.Contains(_logger.Lines.Single(), ", threw InvalidOperationException");
    }

    [TestMethod]
    public void EndOpen_NullState_WritesNothingAndLeavesAnOpenScopeAlone()
    {
        QuestLogDeferCalls.BeginOpen();

        QuestLogDeferCalls.EndOpen(null, null);

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.IsTrue(QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null), "the prefix's scope is still open");
    }

    [TestMethod]
    public void EndOpen_ClosesTheScope()
    {
        var call = QuestLogDeferCalls.BeginOpen();
        QuestLogDeferCalls.EndOpen(call, null);

        Assert.IsFalse(QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null));
    }

    [TestMethod]
    public void BeginOpen_TimerOff_StillScopesTheService_AndWritesNoLine()
    {
        _settings.LogScreenOpenTimes.Returns(false);
        var call = QuestLogDeferCalls.BeginOpen();
        var skipped = QuestLogDeferCalls.ShouldSkipPopulate(new object(), true, null);
        QuestLogDeferCalls.EndOpen(call, null);

        Assert.IsTrue(skipped);
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Calls_DelegateToTheService()
    {
        var item = new object();
        var entry = new object();
        _adapter.AnyUnexamined(item).Returns(true);
        var call = QuestLogDeferCalls.BeginOpen();

        Assert.IsTrue(QuestLogDeferCalls.ShouldSkipPopulate(item, true, entry));
        Assert.AreEqual(true, QuestLogDeferCalls.UpdatedFor(item));
        Assert.AreSame(entry, QuestLogDeferCalls.SortFallback(item, true));
        QuestLogDeferCalls.EnsureBuilt(item);
        QuestLogDeferCalls.EndOpen(call, null);

        _adapter.Received(1).Rebuild(item);
    }
}
