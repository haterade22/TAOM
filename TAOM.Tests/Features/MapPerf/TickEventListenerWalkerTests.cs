using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.MapPerf;
using TAOM.Features.MapPerf.Hooks;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// Differential tests for the Patch101 TickEvent listener walk: vanilla <c>MbEvent&lt;float&gt;.Invoke</c> and
/// <see cref="TickEventListenerWalker.InvokeTimed"/> run over identically built lists and must call the same
/// listeners in the same order, propagate the same exception instance, survive a listener removing itself,
/// and leave a listener added during the dispatch for the next one. Constructs and invokes a real
/// <see cref="MbEvent{T}"/>, so it needs the game assemblies.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class TickEventListenerWalkerTests
{
    private sealed class OwnerA { }

    private sealed class OwnerB { }

    private MapFrameProfiler _profiler = null!;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsTrue(TickEventListenerWalker.TryBind(out var failure), failure);
        _profiler = new MapFrameProfiler(1000);
        _profiler.BeginSession(0, true, 0, 0, 0);
        _profiler.Boundary(0, 0, MapSkip.None, MapSpeedClass.FF);
    }

    private MapWindow CloseAndTake()
    {
        _profiler.AddAppTick(0);
        _profiler.Boundary(10, 0, MapSkip.None, MapSpeedClass.FF);
        return _profiler.TakeWindow(10, 20, 0, 0, 0);
    }

    private void Walk(MbEvent<float> evt) =>
        TickEventListenerWalker.InvokeTimed(evt, 0.1f, _profiler, typeof(MapFrameProfiler).Assembly);

    // Builds the same registrations on a fresh event, appending each listener's label to `log`.
    private static MbEvent<float> Build(List<string> log, object a, object b)
    {
        var evt = new MbEvent<float>();
        evt.AddNonSerializedListener(a, _ => log.Add("A1"));
        evt.AddNonSerializedListener(b, _ => log.Add("B1"));
        evt.AddNonSerializedListener(a, _ => log.Add("A2"));
        return evt;
    }

    [TestMethod]
    public void TryBind_InstalledEngine_Binds()
    {
        Assert.IsTrue(TickEventListenerWalker.TryBind(out var failure));
        Assert.IsTrue(TickEventListenerWalker.Bound);
        Assert.AreEqual(string.Empty, failure);
    }

    [TestMethod]
    public void InvokeTimed_CallsListenersInTheSameOrderAsInvoke()
    {
        var vanilla = new List<string>();
        var walked = new List<string>();

        Build(vanilla, new OwnerA(), new OwnerB()).Invoke(0.1f);
        Walk(Build(walked, new OwnerA(), new OwnerB()));

        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.AreEqual(new[] { "A2", "B1", "A1" }, walked, "Listeners run newest first.");
    }

    [TestMethod]
    public void InvokeTimed_ListenerThrows_SameExceptionAndSkipsTheRestLikeInvoke_AndRecordsTheCall()
    {
        var boom = new InvalidOperationException("listener broke");
        MbEvent<float> Throwing(List<string> log)
        {
            var evt = new MbEvent<float>();
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A1"));
            evt.AddNonSerializedListener(new OwnerB(), _ => { log.Add("B1"); throw boom; });
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A2"));
            return evt;
        }

        var vanilla = new List<string>();
        var walked = new List<string>();
        var vanillaThrown = Assert.ThrowsException<InvalidOperationException>(() => Throwing(vanilla).Invoke(0.1f));
        var walkedThrown = Assert.ThrowsException<InvalidOperationException>(() => Walk(Throwing(walked)));

        Assert.AreSame(boom, vanillaThrown);
        Assert.AreSame(boom, walkedThrown);
        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.AreEqual(new[] { "A2", "B1" }, walked);
        var top = CloseAndTake().Top;
        Assert.IsTrue(top.Any(t => t.Name == nameof(OwnerB) && t.Calls == 1), "The throwing owner's call is recorded.");
    }

    [TestMethod]
    public void InvokeTimed_ListenerClearsItself_ContinuesToItsSuccessorLikeInvoke()
    {
        MbEvent<float> SelfClearing(List<string> log)
        {
            var evt = new MbEvent<float>();
            var messenger = new OwnerB();
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A1"));
            evt.AddNonSerializedListener(messenger, _ => { log.Add("B1"); evt.ClearListeners(messenger); });
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A2"));
            return evt;
        }

        var vanilla = new List<string>();
        var walked = new List<string>();
        var vanillaEvent = SelfClearing(vanilla);
        var walkedEvent = SelfClearing(walked);
        vanillaEvent.Invoke(0.1f);
        Walk(walkedEvent);

        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.Contains(walked, "A1", "The successor of the self-removed listener ran.");

        vanillaEvent.Invoke(0.1f);
        Walk(walkedEvent);
        CollectionAssert.AreEqual(vanilla, walked, "The removed listener is gone from both lists afterwards.");
    }

    [TestMethod]
    public void InvokeTimed_ListenerClearsItsSuccessor_SkipsItLikeInvoke()
    {
        MbEvent<float> ClearingNext(List<string> log)
        {
            var evt = new MbEvent<float>();
            var victim = new OwnerA();
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A1"));
            evt.AddNonSerializedListener(victim, _ => log.Add("Victim"));
            evt.AddNonSerializedListener(new OwnerB(), _ => { log.Add("B1"); evt.ClearListeners(victim); });
            return evt;
        }

        var vanilla = new List<string>();
        var walked = new List<string>();
        ClearingNext(vanilla).Invoke(0.1f);
        Walk(ClearingNext(walked));

        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.DoesNotContain(walked, "Victim", "Next is read after the call, so a successor removed by it is skipped.");
    }

    [TestMethod]
    public void InvokeTimed_HeadListenerClearsItself_RestStillRunLikeInvoke()
    {
        MbEvent<float> HeadClearing(List<string> log)
        {
            var evt = new MbEvent<float>();
            var head = new OwnerB();
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A1"));
            evt.AddNonSerializedListener(new OwnerA(), _ => log.Add("A2"));
            evt.AddNonSerializedListener(head, _ => { log.Add("Head"); evt.ClearListeners(head); });
            return evt;
        }

        var vanilla = new List<string>();
        var walked = new List<string>();
        HeadClearing(vanilla).Invoke(0.1f);
        Walk(HeadClearing(walked));

        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.AreEqual(new[] { "Head", "A2", "A1" }, walked);
    }

    [TestMethod]
    public void InvokeTimed_ListenerAddsAListener_NewOneWaitsForTheNextDispatchLikeInvoke()
    {
        MbEvent<float> Adding(List<string> log)
        {
            var evt = new MbEvent<float>();
            var added = false;
            evt.AddNonSerializedListener(new OwnerA(), _ =>
            {
                log.Add("A1");
                if (added) return;
                added = true;
                evt.AddNonSerializedListener(new OwnerB(), __ => log.Add("New"));
            });
            return evt;
        }

        var vanilla = new List<string>();
        var walked = new List<string>();
        var vanillaEvent = Adding(vanilla);
        var walkedEvent = Adding(walked);
        vanillaEvent.Invoke(0.1f);
        Walk(walkedEvent);

        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.DoesNotContain(walked, "New");

        vanillaEvent.Invoke(0.1f);
        Walk(walkedEvent);
        CollectionAssert.AreEqual(vanilla, walked);
        CollectionAssert.Contains(walked, "New");
    }

    [TestMethod]
    public void InvokeTimed_RecordsOneEntryPerOwnerType_AndFlagsTheGivenAssemblyAsTaom()
    {
        var evt = new MbEvent<float>();
        evt.AddNonSerializedListener(new OwnerA(), _ => { });
        evt.AddNonSerializedListener(new OwnerB(), _ => { });
        evt.AddNonSerializedListener(new object(), _ => { });

        TickEventListenerWalker.InvokeTimed(evt, 0.1f, _profiler, typeof(OwnerA).Assembly);
        var w = CloseAndTake();

        CollectionAssert.AreEquivalent(new[] { nameof(OwnerA), nameof(OwnerB), "Object" }, w.Top.Select(t => t.Name).ToArray());
        Assert.IsTrue(w.Top.All(t => t.Calls == 1));
        var testOwned = w.Top.Where(t => t.Name != "Object").Sum(t => t.Ms);
        Assert.AreEqual(testOwned, w.TaomMs, 1e-9, "Only the given assembly's owners count toward taomMs (no app tick time here).");
    }

    [TestMethod]
    public void InvokeTimed_NoListeners_RecordsNothing()
    {
        Walk(new MbEvent<float>());

        Assert.AreEqual(0, CloseAndTake().Top.Count);
    }
}
