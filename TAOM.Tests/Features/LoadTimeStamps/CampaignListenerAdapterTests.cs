using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps.Domain;
using TAOM.Features.MapLoadDiagnostics;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// The listener swap on real engine MbEvent instances (no campaign needed; invoked with a null
/// starter): the invoke order, the arguments and every exception are unchanged, each call is
/// recorded once on its listener's timing, and restore puts back the very delegates that were added. RequiresGame because it
/// runs engine code.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CampaignListenerAdapterTests
{
    private FakeStampClock _clock = null!;
    private CampaignListenerAdapter _sut = null!;
    private List<string> _ran = null!;

    private sealed class OwnerA { }

    private sealed class OwnerB { }

    private sealed class OwnerC { }

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _sut = new CampaignListenerAdapter(_clock);
        _ran = new List<string>();
    }

    private static (int Calls, long Ticks, int MaxArgument)[] Calls(IReadOnlyList<ListenerTiming> timings) =>
        timings.Select(t => (t.Calls, t.Ticks, t.MaxArgument)).ToArray();

    private static object? RecordAt(object mbEvent, int position)
    {
        var record = AccessTools.Field(mbEvent.GetType(), "_nonSerializedListenerList").GetValue(mbEvent);
        for (var i = 0; i < position; i++)
            record = AccessTools.Field(record!.GetType(), "Next").GetValue(record);
        return record;
    }

    private static object? ActionOf(object mbEvent, int position)
    {
        var record = RecordAt(mbEvent, position);
        return AccessTools.Property(record!.GetType(), "Action").GetValue(record);
    }

    // The engine's setter is private; a test plays another party that replaces a record's delegate.
    private static void SetActionOf(object mbEvent, int position, Delegate action)
    {
        var record = RecordAt(mbEvent, position);
        AccessTools.PropertySetter(record!.GetType(), "Action").Invoke(record, new object[] { action });
    }

    // Listeners A, B and C are added in that order, so the records (and the invoke order) read C, B, A.
    private (MbEvent<CampaignGameStarter> Event, Action<CampaignGameStarter> A, Action<CampaignGameStarter> B, Action<CampaignGameStarter> C) ThreeListeners()
    {
        Action<CampaignGameStarter> a = _ => _ran.Add("A");
        Action<CampaignGameStarter> b = _ => _ran.Add("B");
        Action<CampaignGameStarter> c = _ => _ran.Add("C");
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), a);
        mbEvent.AddNonSerializedListener(new OwnerB(), b);
        mbEvent.AddNonSerializedListener(new OwnerC(), c);
        return (mbEvent, a, b, c);
    }

    [TestMethod]
    public void Constructor_AgainstTheInstalledEngine_ResolvesEveryMember()
    {
        Assert.IsNull(_sut.BindingProblem);
    }

    [TestMethod]
    public void WrapOne_KeepsTheInvokeOrder_AndReportsEachListenerOnce()
    {
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), _ => { _ran.Add("A"); _clock.Advance(1); });
        mbEvent.AddNonSerializedListener(new OwnerB(), _ => { _ran.Add("B"); _clock.Advance(2); });
        mbEvent.AddNonSerializedListener(new OwnerC(), _ => { _ran.Add("C"); _clock.Advance(3); });
        mbEvent.Invoke(null!);
        CollectionAssert.AreEqual(new[] { "C", "B", "A" }, _ran);
        _ran.Clear();

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        mbEvent.Invoke(null!);

        CollectionAssert.AreEqual(new[] { "C", "B", "A" }, _ran);
        CollectionAssert.AreEqual(new[] { "OwnerC", "OwnerB", "OwnerA" }, timings.Select(i => i.Handler.Split('.')[0]).ToArray());
        CollectionAssert.AreEqual(new[] { (1, 3L, -1), (1, 2L, -1), (1, 1L, -1) }, Calls(timings));
    }

    [TestMethod]
    public void WrapOne_AListenerThrows_TheSameExceptionLeavesInvoke_AndItsTimeIsStillReported()
    {
        var thrown = new InvalidOperationException("handler broke");
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), _ => _ran.Add("A"));
        mbEvent.AddNonSerializedListener(new OwnerB(), _ => { _clock.Advance(4); throw thrown; });
        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);

        var caught = Assert.ThrowsException<InvalidOperationException>(() => mbEvent.Invoke(null!));

        Assert.AreSame(thrown, caught);
        Assert.AreEqual(0, _ran.Count, "a listener after the throwing one ran; vanilla stops there");
        CollectionAssert.AreEqual(new[] { (1, 4L, -1), (0, 0L, -1) }, Calls(timings));
    }

    [TestMethod]
    public void RestoreListeners_PutsBackTheOriginalDelegates()
    {
        Action<CampaignGameStarter> first = _ => _ran.Add("first");
        Action<CampaignGameStarter> second = _ => _ran.Add("second");
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), first);
        mbEvent.AddNonSerializedListener(new OwnerB(), second);
        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnGameLoaded);
        Assert.AreNotSame(second, ActionOf(mbEvent, 0), "the wrap did not swap the delegate");

        _sut.RestoreListeners(LifecycleEvent.OnGameLoaded);

        Assert.AreSame(second, ActionOf(mbEvent, 0));
        Assert.AreSame(first, ActionOf(mbEvent, 1));
        mbEvent.Invoke(null!);
        CollectionAssert.AreEqual(new[] { "second", "first" }, _ran);
        Assert.AreEqual(0, timings.Sum(t => t.Calls), "a restored listener still recorded a call");
    }

    [TestMethod]
    public void RestoreListeners_Twice_IsHarmless()
    {
        Action<CampaignGameStarter> only = _ => _ran.Add("only");
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), only);
        _sut.WrapOne(mbEvent, LifecycleEvent.OnGameLoaded);

        _sut.RestoreListeners(LifecycleEvent.OnGameLoaded);
        _sut.RestoreListeners(LifecycleEvent.OnGameLoaded);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        Assert.AreSame(only, ActionOf(mbEvent, 0));
    }

    // The walk can only fail midway through the engine's own records if a write fails, and no real
    // record makes it fail, so these tests make the third write (A's, the last in invoke order) fail
    // through the adapter's BeforeWrite seam, on real MbEvent records that the first two writes
    // already changed.
    [TestMethod]
    public void WrapOne_AWriteFailsMidWalk_PutsBackEveryDelegateAlreadySwapped()
    {
        var (mbEvent, a, b, c) = ThreeListeners();
        var writes = 0;
        _sut.BeforeWrite = (_, _) => { if (++writes == 3) throw new InvalidOperationException("setter moved"); };

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched));

        Assert.AreEqual("setter moved", thrown.Message);
        Assert.AreSame(c, ActionOf(mbEvent, 0), "C was left wrapped");
        Assert.AreSame(b, ActionOf(mbEvent, 1), "B was left wrapped");
        Assert.AreSame(a, ActionOf(mbEvent, 2));
        mbEvent.Invoke(null!);
        CollectionAssert.AreEqual(new[] { "C", "B", "A" }, _ran, "the dispatch after the failed swap differs from vanilla's");
        Assert.AreEqual(5, writes, "expected three writes in the walk (the third fails) and two in the rollback");
    }

    [TestMethod]
    public void WrapOne_ARollbackWriteFailsToo_StillPutsBackTheOthers_AndThrowsTheWrapsOwnFailure()
    {
        var (mbEvent, a, b, c) = ThreeListeners();
        var writes = 0;
        // Writes 1 and 2 swap C and B, write 3 (A's) fails, then the rollback writes B (4, fails) and C (5).
        _sut.BeforeWrite = (_, _) =>
        {
            writes++;
            if (writes == 3) throw new InvalidOperationException("setter moved");
            if (writes == 4) throw new InvalidOperationException("rollback of B failed");
        };

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched));

        Assert.AreEqual("setter moved", thrown.Message, "the rollback's own failure replaced the wrap's");
        Assert.AreSame(c, ActionOf(mbEvent, 0), "B's failed rollback stopped C's");
        Assert.AreNotSame(b, ActionOf(mbEvent, 1), "B's rollback did not fail, so this test proves nothing");
        Assert.AreSame(a, ActionOf(mbEvent, 2));
        mbEvent.Invoke(null!);
        CollectionAssert.AreEqual(new[] { "C", "B", "A" }, _ran, "a record left wrapped must still call its original once");
    }

    [TestMethod]
    public void RestoreListeners_ARecordSomeoneElseReplaced_IsLeftAlone()
    {
        Action<CampaignGameStarter> mine = _ => _ran.Add("mine");
        Action<CampaignGameStarter> theirs = _ => _ran.Add("theirs");
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), mine);
        _sut.WrapOne(mbEvent, LifecycleEvent.OnGameLoaded);
        SetActionOf(mbEvent, 0, theirs);

        _sut.RestoreListeners(LifecycleEvent.OnGameLoaded);

        Assert.AreSame(theirs, ActionOf(mbEvent, 0), "the restore overwrote a delegate another party installed");
        mbEvent.Invoke(null!);
        CollectionAssert.AreEqual(new[] { "theirs" }, _ran);
    }

    [TestMethod]
    public void WrapOne_AListenerThatClearsAnotherOwnersListener_DoesNotBreakTheDispatchOrTheRestore()
    {
        var ownerB = new OwnerB();
        var mbEvent = new MbEvent<CampaignGameStarter>();
        Action<CampaignGameStarter> a = _ => _ran.Add("A");
        Action<CampaignGameStarter> b = _ => _ran.Add("B");
        mbEvent.AddNonSerializedListener(new OwnerA(), a);
        mbEvent.AddNonSerializedListener(ownerB, b);
        Action<CampaignGameStarter> c = _ => { _ran.Add("C"); mbEvent.ClearListeners(ownerB); };
        mbEvent.AddNonSerializedListener(new OwnerC(), c);
        _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);

        mbEvent.Invoke(null!);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        CollectionAssert.AreEqual(new[] { "C", "A" }, _ran, "the dispatch differs from vanilla's (C unlinks B, then A runs)");
        Assert.AreSame(c, ActionOf(mbEvent, 0));
        Assert.AreSame(a, ActionOf(mbEvent, 1));
    }

    [TestMethod]
    public void WrapTwo_ReportsTheIntArgument()
    {
        var seen = new List<int>();
        var mbEvent = new MbEvent<CampaignGameStarter, int>();
        Action<CampaignGameStarter, int> listener = (_, i) => { seen.Add(i); _clock.Advance(5); };
        mbEvent.AddNonSerializedListener(new OwnerA(), listener);
        var timings = _sut.WrapTwo(mbEvent, LifecycleEvent.OnNewGameCreatedPartialFollowUp);

        mbEvent.Invoke(null!, 7);
        _sut.RestoreListeners(LifecycleEvent.OnNewGameCreatedPartialFollowUp);

        CollectionAssert.AreEqual(new[] { 7 }, seen);
        CollectionAssert.AreEqual(new[] { (1, 5L, 7) }, Calls(timings));
        Assert.AreSame(listener, ActionOf(mbEvent, 0));
    }

    [TestMethod]
    public void ListenerTiming_NamesTheOwnerTypeAndTheMethod_AndTheAssemblyOfTheCallback()
    {
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), OnSession);

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual("OwnerA.OnSession", timings[0].Handler);
        Assert.AreEqual(typeof(CampaignListenerAdapterTests).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsFalse(timings[0].IsTaom);
    }

    // The owner is only the token ClearListeners matches (v1.5.3 MbEvent`1.cs:46-79): vanilla stores it
    // beside the delegate and never ties the two. TAOM's StaleCharacterAdapter registers a lambda under
    // a plain object, so the owner's assembly says nothing about whose code runs.
    [TestMethod]
    public void ListenerTiming_ATaomCallbackUnderAPlainOwnerToken_IsAttributedToTaom()
    {
        var taomBehavior = new MapLoadDiagnosticsBehavior(Substitute.For<IMapLoadHeartbeatService>());
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new object(), taomBehavior.OnSessionLaunched);

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual(typeof(CampaignListenerAdapter).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsTrue(timings[0].IsTaom, "a TAOM callback under a plain token counted as someone else's");
    }

    [TestMethod]
    public void ListenerTiming_AnotherAssemblysCallbackUnderATaomOwner_IsNotAttributedToTaom()
    {
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new StopwatchStampClock(), OnSession);

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual("StopwatchStampClock.OnSession", timings[0].Handler);
        Assert.AreEqual(typeof(CampaignListenerAdapterTests).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsFalse(timings[0].IsTaom, "a callback from another assembly counted as TAOM's because of its owner");
    }

    // A dynamic method has no declaring type, so the owner is the only evidence left.
    [TestMethod]
    public void ListenerTiming_ACallbackWithNoDeclaringType_FallsBackToTheOwnersAssembly()
    {
        var method = new DynamicMethod("dynamic_listener", typeof(void), new[] { typeof(CampaignGameStarter) });
        method.GetILGenerator().Emit(OpCodes.Ret);
        var callback = (Action<CampaignGameStarter>)method.CreateDelegate(typeof(Action<CampaignGameStarter>));
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new StopwatchStampClock(), callback);

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual("StopwatchStampClock.dynamic_listener", timings[0].Handler);
        Assert.AreEqual(typeof(StopwatchStampClock).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsTrue(timings[0].IsTaom);
    }

    // A restore whose write fails leaves the record holding the adapter's own wrapper, and the adapter
    // forgets it (C6). The next dispatch with the toggle on wraps that record again and reads the
    // wrapper as the callback. The wrapper is TAOM's code but says nothing about whose handler runs
    // inside it, so the owner's assembly stands in, as for a dynamic method.
    [TestMethod]
    public void ListenerTiming_ARecordStillHoldingTheAdaptersOwnWrapper_IsAttributedToItsOwner()
    {
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), OnSession);
        _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);
        _sut.BeforeWrite = (_, _) => throw new InvalidOperationException("setter moved");
        Assert.ThrowsException<InvalidOperationException>(() => _sut.RestoreListeners(LifecycleEvent.OnSessionLaunched));
        _sut.BeforeWrite = null;

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual(typeof(CampaignListenerAdapterTests).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsFalse(timings[0].IsTaom, "the adapter's own wrapper counted as a TAOM handler");
    }

    // The other way into that state: a record wrapped twice before its restore (the case the restore
    // unwinds newest first) reads the first wrapper as the callback.
    [TestMethod]
    public void ListenerTiming_ARecordWrappedTwiceBeforeItsRestore_IsAttributedToItsOwner()
    {
        var mbEvent = new MbEvent<CampaignGameStarter>();
        mbEvent.AddNonSerializedListener(new OwnerA(), OnSession);
        _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);

        var timings = _sut.WrapOne(mbEvent, LifecycleEvent.OnSessionLaunched);

        Assert.AreEqual(typeof(CampaignListenerAdapterTests).Assembly.GetName().Name, timings[0].Assembly);
        Assert.IsFalse(timings[0].IsTaom, "the first wrapper of a twice-wrapped record counted as a TAOM handler");
    }

    private void OnSession(CampaignGameStarter starter) => _ran.Add("session");
}
