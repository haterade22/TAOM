using System;
using System.Collections.Generic;
using System.Threading;
using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.BehaviorTreeWrapper;

/// <summary>
/// The queue behind <c>BehaviorTreeMissionLogic</c>'s off-thread callbacks. Vanilla's
/// <c>CommonAIComponent.OnTick</c> raises <c>Mission.OnAgentPanicked</c> from the asynchronous agent
/// tick (v1.4.8), so a callback can arrive while the main thread owns the tree maps; it is parked
/// here and replayed from the next mission tick (#595, Codex review 109).
/// </summary>
[TestClass]
public class DeferredCallbackQueueTests
{
    [TestMethod]
    public void Drain_RunsCallbacksInArrivalOrder_AndEmptiesTheQueue()
    {
        var queue = new DeferredCallbackQueue();
        var order = new List<int>();
        queue.Enqueue(() => order.Add(1));
        queue.Enqueue(() => order.Add(2));
        queue.Enqueue(() => order.Add(3));
        Assert.AreEqual(3, queue.Count);

        int ran = queue.Drain();

        Assert.AreEqual(3, ran);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        Assert.AreEqual(0, queue.Count);
    }

    [TestMethod]
    public void Drain_OnAnEmptyQueue_RunsNothing()
    {
        var queue = new DeferredCallbackQueue();

        Assert.AreEqual(0, queue.Drain());
    }

    [TestMethod]
    public void ACallbackQueuedWhileDraining_WaitsForTheNextDrain()
    {
        var queue = new DeferredCallbackQueue();
        int nested = 0;
        queue.Enqueue(() => queue.Enqueue(() => nested++));

        int firstDrain = queue.Drain();

        Assert.AreEqual(1, firstDrain, "the replayed callback's own enqueue must not extend the current drain");
        Assert.AreEqual(0, nested);
        Assert.AreEqual(1, queue.Count);
        Assert.AreEqual(1, queue.Drain());
        Assert.AreEqual(1, nested);
    }

    [TestMethod]
    public void Clear_DropsQueuedCallbacksWithoutRunningThem()
    {
        var queue = new DeferredCallbackQueue();
        int ran = 0;
        queue.Enqueue(() => ran++);

        queue.Clear();

        Assert.AreEqual(0, queue.Count);
        Assert.AreEqual(0, queue.Drain());
        Assert.AreEqual(0, ran);
    }

    [TestMethod]
    public void Enqueue_Null_Throws()
    {
        var queue = new DeferredCallbackQueue();

        Assert.ThrowsException<ArgumentNullException>(() => queue.Enqueue(null));
    }

    [TestMethod]
    public void CallbacksQueuedFromAnotherThread_RunOnTheDrainingThread()
    {
        var queue = new DeferredCallbackQueue();
        int producerThread = -1;
        int ranOn = -1;
        var producer = new Thread(() =>
        {
            producerThread = Thread.CurrentThread.ManagedThreadId;
            for (int i = 0; i < 100; i++)
                queue.Enqueue(() => ranOn = Thread.CurrentThread.ManagedThreadId);
        });
        producer.Start();
        producer.Join();

        int ran = queue.Drain();

        Assert.AreEqual(100, ran);
        Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, ranOn);
        Assert.AreNotEqual(producerThread, ranOn);
    }
}
