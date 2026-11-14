using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Diagnostics;

namespace TAOM.Tests.Core.Diagnostics;

/// <summary>
/// Issue #1185: the process-wide record of a fix that turned itself off. The ledger is reachable from any
/// thread, so it is lock-free and writes nothing; the main-thread drain does the logging and the chat line.
/// </summary>
[TestClass]
public class SelfDisableLedgerTests
{
    private SelfDisableLedger _saved = null!;

    [TestInitialize]
    public void SwapInAFreshLedger()
    {
        _saved = SelfDisables.Ledger;
        SelfDisables.Ledger = new SelfDisableLedger();
    }

    [TestCleanup]
    public void PutTheOldLedgerBack() => SelfDisables.Ledger = _saved;

    [TestMethod]
    public void Report_SameIdTwice_RecordsItOnce()
    {
        var ledger = new SelfDisableLedger();

        Assert.IsTrue(ledger.Report("Patch1_A", "first"));
        Assert.IsFalse(ledger.Report("Patch1_A", "second"));

        var taken = ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("first", taken[0].Reason);
    }

    [TestMethod]
    public void Report_NullOrEmptyId_RecordsNothing()
    {
        var ledger = new SelfDisableLedger();

        Assert.IsFalse(ledger.Report(null!, "x"));
        Assert.IsFalse(ledger.Report("", "x"));

        Assert.IsFalse(ledger.HasPending);
        Assert.AreEqual(0, ledger.Take().Count);
    }

    [TestMethod]
    public void Take_AfterReports_ReturnsEachIdWithItsReason_InReportOrder()
    {
        var ledger = new SelfDisableLedger();
        ledger.Report("B", "reason b");
        ledger.Report("A", null);
        ledger.Report("C", "reason c");

        var taken = ledger.Take();

        CollectionAssert.AreEqual(new[] { "B", "A", "C" }, taken.Select(e => e.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "reason b", "", "reason c" }, taken.Select(e => e.Reason).ToArray());
    }

    [TestMethod]
    public void Take_Twice_ReturnsTheEntriesOnlyTheFirstTime()
    {
        var ledger = new SelfDisableLedger();
        ledger.Report("A", "x");

        Assert.AreEqual(1, ledger.Take().Count);
        Assert.AreEqual(0, ledger.Take().Count);
    }

    [TestMethod]
    public void HasPending_IsTrueAfterAReport_AndFalseAfterTake()
    {
        var ledger = new SelfDisableLedger();
        Assert.IsFalse(ledger.HasPending);

        ledger.Report("A", "x");
        Assert.IsTrue(ledger.HasPending);

        ledger.Take();
        Assert.IsFalse(ledger.HasPending);
    }

    [TestMethod]
    public void Report_TheSameIdFromTwoThreadsAtOnce_RecordsItOnce()
    {
        var ledger = new SelfDisableLedger();
        using var barrier = new Barrier(2);

        void Run(bool ascending)
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 1000; i++)
                ledger.Report("id" + (ascending ? i : 999 - i), "x");
        }

        var a = Task.Run(() => Run(true));
        var b = Task.Run(() => Run(false));
        Task.WaitAll(a, b);

        var taken = ledger.Take();
        Assert.AreEqual(1000, taken.Count);
        Assert.AreEqual(1000, taken.Select(e => e.Id).Distinct().Count());
        Assert.IsFalse(ledger.HasPending);
    }

    [TestMethod]
    public void Report_AfterTake_DoesNotRecordAnIdAgain()
    {
        var ledger = new SelfDisableLedger();
        ledger.Report("A", "x");
        ledger.Take();

        Assert.IsFalse(ledger.Report("A", "again"));
        Assert.IsFalse(ledger.HasPending);
    }

    [TestMethod]
    public void SelfDisablesReport_GoesToTheSwappedLedger()
    {
        SelfDisables.Report("Facade", "why");

        var taken = SelfDisables.Ledger.Take();
        Assert.AreEqual(1, taken.Count);
        Assert.AreEqual("Facade", taken[0].Id);
        Assert.AreEqual("why", taken[0].Reason);
    }
}
