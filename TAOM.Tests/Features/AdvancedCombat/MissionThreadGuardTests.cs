using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The tripwire behind #592's second half. The engine ticks agents on an asynchronous thread in
/// single-player (<c>MissionState.cs:201</c>, v1.4.8), and TAOM's creature trees used to run there
/// and register blows from it. Every TAOM synthetic blow and creature action now runs from the
/// main-thread mission tick; this guard is how a player's log proves it, and how a future regression
/// announces itself instead of freezing the game.
/// </summary>
[TestClass]
public class MissionThreadGuardTests
{
    [TestInitialize]
    public void Reset() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void BeforeAnyMark_NothingIsOffThread()
    {
        int reported = 0;

        bool off = MissionThreadGuard.NoteCall("site", _ => reported++);

        Assert.IsFalse(off, "with no main thread recorded there is nothing to compare against");
        Assert.AreEqual(0, reported);
    }

    [TestMethod]
    public void OnTheMarkedThread_NoteCallIsSilent()
    {
        MissionThreadGuard.MarkMainThread();
        int reported = 0;

        bool off = MissionThreadGuard.NoteCall("site", _ => reported++);

        Assert.IsFalse(off);
        Assert.AreEqual(0, reported);
    }

    [TestMethod]
    public void OnAnotherThread_NoteCallReportsOnce_PerSite()
    {
        MissionThreadGuard.MarkMainThread();
        int reported = 0;
        string lastMessage = null;
        bool first = false, second = false, otherSite = false;

        var worker = new Thread(() =>
        {
            first = MissionThreadGuard.NoteCall("TakeDamage", m => { reported++; lastMessage = m; });
            second = MissionThreadGuard.NoteCall("TakeDamage", m => { reported++; lastMessage = m; });
            otherSite = MissionThreadGuard.NoteCall("CustomAttack", m => { reported++; lastMessage = m; });
        });
        worker.Start();
        worker.Join();

        Assert.IsTrue(first);
        Assert.IsTrue(second, "the call is still off-thread; only the report is deduplicated");
        Assert.IsTrue(otherSite);
        Assert.AreEqual(2, reported, "one report per site");
        StringAssert.Contains(lastMessage, "CustomAttack");
        Assert.AreEqual(3, MissionThreadGuard.OffThreadCalls);
    }

    [TestMethod]
    public void MarkMainThread_ReplacesAnEarlierMark()
    {
        var worker = new Thread(() => MissionThreadGuard.MarkMainThread());
        worker.Start();
        worker.Join();
        Assert.IsTrue(MissionThreadGuard.NoteCall("site", _ => { }), "this thread is not the marked one yet");

        MissionThreadGuard.MarkMainThread();

        Assert.IsFalse(MissionThreadGuard.NoteCall("site", _ => { }));
    }

    [TestMethod]
    public void ResetForTests_ClearsMarkCountAndReportedSites()
    {
        MissionThreadGuard.MarkMainThread();
        var worker = new Thread(() => MissionThreadGuard.NoteCall("site", _ => { }));
        worker.Start();
        worker.Join();

        MissionThreadGuard.ResetForTests();

        Assert.AreEqual(0, MissionThreadGuard.OffThreadCalls);
        Assert.IsFalse(MissionThreadGuard.NoteCall("site", _ => { }));
    }

    [TestMethod]
    public void IsOnMainThread_BeforeAnyMark_IsTrue()
    {
        Assert.IsTrue(MissionThreadGuard.IsOnMainThread, "no asynchronous tick can have run before the first mission tick");
    }

    [TestMethod]
    public void IsOnMainThread_OnTheMarkedThread_IsTrue()
    {
        MissionThreadGuard.MarkMainThread();

        Assert.IsTrue(MissionThreadGuard.IsOnMainThread);
    }

    [TestMethod]
    public void IsOnMainThread_OnAnotherThread_IsFalse()
    {
        MissionThreadGuard.MarkMainThread();
        bool onWorker = true;
        var worker = new Thread(() => onWorker = MissionThreadGuard.IsOnMainThread);
        worker.Start();
        worker.Join();

        Assert.IsFalse(onWorker);
    }
}
