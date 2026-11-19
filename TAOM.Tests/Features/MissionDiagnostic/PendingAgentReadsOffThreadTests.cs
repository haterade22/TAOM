using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// Native raises OnAgentRemoved and OnAgentDeleted on the thread it chooses (#634, #607) while the main thread adds
// agents and drains the queue. A removal off the main thread is parked, and the next Drain applies it before it reads
// anything. Plain objects stand in for agents; MissionThreadGuard and DeferredCallbackQueue are engine-free.
[TestClass]
public class PendingAgentReadsOffThreadTests
{
    private const string Site = "PendingAgentReadsOffThreadTests.Remove";
    private PendingAgentReads<object> _sut = null!;
    private List<string> _reports = null!;
    private List<object> _read = null!;

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _reports = new List<string>();
        _read = new List<object>();
        _sut = new PendingAgentReads<object>(_reports.Add);
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void RemoveOrDefer_OnTheMainThread_RemovesAtOnce()
    {
        var a = new object();
        _sut.Add(a);

        _sut.RemoveOrDefer(Site, a);

        Assert.AreEqual(0, _sut.Count);
        Assert.AreEqual(0, _reports.Count, "a main-thread removal is not reported");
    }

    [TestMethod]
    public void RemoveOrDefer_OffTheMainThread_WaitsForTheNextDrain_WhichAppliesItBeforeReading()
    {
        var gone = new object();
        var kept = new object();
        _sut.Add(gone);
        _sut.Add(kept);

        OnWorker(() => _sut.RemoveOrDefer(Site, gone));

        Assert.AreEqual(2, _sut.Count, "the worker must not touch the pending set");
        _sut.Drain(_read.Add);
        CollectionAssert.AreEqual(new[] { kept }, _read);
    }

    [TestMethod]
    public void RemoveOrDefer_OffTheMainThread_ReportsTheSiteOnce()
    {
        OnWorker(() =>
        {
            _sut.RemoveOrDefer(Site, new object());
            _sut.RemoveOrDefer(Site, new object());
        });

        Assert.AreEqual(1, _reports.Count);
        StringAssert.Contains(_reports[0], Site + " ran off the main mission thread");
    }

    [TestMethod]
    public void Clear_DropsParkedRemovals_SoNoneIsAppliedAfterIt()
    {
        var a = new object();
        _sut.Add(a);
        OnWorker(() => _sut.RemoveOrDefer(Site, a));

        _sut.Clear();
        _sut.Add(a);
        _sut.Drain(_read.Add);

        CollectionAssert.AreEqual(new[] { a }, _read);
    }
}
