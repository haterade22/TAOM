using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaStoreAndQueueTests
{
    [TestMethod]
    public void Admit_QueuedLiveSoldier_GetsAFreshCell()
    {
        var store = new StaminaStore<object>();
        var soldier = new object();
        store.Enqueue(soldier);

        var admitted = store.Admit(12f, _ => true);

        Assert.AreEqual(1, admitted);
        Assert.IsTrue(store.TryGet(soldier, out var cell));
        Assert.AreEqual(StaminaBand.Fresh, cell.Band);
        Assert.AreEqual(1f, cell.Fraction);
        Assert.AreEqual(12f, cell.BornAt);
        Assert.AreEqual(1, store.Count);
    }

    [TestMethod]
    public void Admit_SoldierRemovedBeforeTheTick_GetsNoCell()
    {
        var store = new StaminaStore<object>();
        var soldier = new object();
        store.Enqueue(soldier);

        var admitted = store.Admit(0f, _ => false);

        Assert.AreEqual(0, admitted);
        Assert.IsFalse(store.TryGet(soldier, out _));
        Assert.AreEqual(0, store.Count);
    }

    [TestMethod]
    public void Admit_SameSoldierQueuedTwice_KeepsOneCell()
    {
        var store = new StaminaStore<object>();
        var soldier = new object();
        store.Enqueue(soldier);
        store.Enqueue(soldier);

        store.Admit(0f, _ => true);

        Assert.AreEqual(1, store.Count);
    }

    [TestMethod]
    public void TryGet_EqualButDifferentObject_FindsNothing()
    {
        var store = new StaminaStore<string>();
        store.Enqueue(new string('a', 3));
        store.Admit(0f, _ => true);

        Assert.IsFalse(store.TryGet(new string('a', 3), out _));
    }

    [TestMethod]
    public void MarkRemoved_HidesTheCellFromReadersAndFlagsItForTheTick()
    {
        var store = new StaminaStore<object>();
        var soldier = new object();
        store.Enqueue(soldier);
        store.Admit(0f, _ => true);
        var cell = store.At(0).Value;

        store.MarkRemoved(soldier);

        Assert.IsFalse(store.TryGet(soldier, out _));
        Assert.IsTrue(cell.Removed);
        Assert.AreEqual(1, store.Count, "the roster entry stays until the tick drops it");
    }

    [TestMethod]
    public void RemoveAt_MiddleEntry_MovesTheLastIntoItsPlace()
    {
        var store = new StaminaStore<object>();
        var first = new object();
        var second = new object();
        var third = new object();
        store.Enqueue(first);
        store.Enqueue(second);
        store.Enqueue(third);
        store.Admit(0f, _ => true);

        store.RemoveAt(0);

        Assert.AreEqual(2, store.Count);
        Assert.AreSame(third, store.At(0).Key);
        Assert.AreSame(second, store.At(1).Key);
        Assert.IsFalse(store.TryGet(first, out _));
    }

    [TestMethod]
    public void Clear_DropsCellsRosterAndQueuedSoldiers()
    {
        var store = new StaminaStore<object>();
        var admitted = new object();
        store.Enqueue(admitted);
        store.Admit(0f, _ => true);
        store.Enqueue(new object());

        store.Clear();

        Assert.AreEqual(0, store.Count);
        Assert.IsFalse(store.TryGet(admitted, out _));
        Assert.AreEqual(0, store.Admit(0f, _ => true), "a soldier queued before the clear is not admitted after it");
    }

    [TestMethod]
    public void Publish_FractionOutOfRangeOrNotANumber_IsClamped()
    {
        var cell = new StaminaCell(0f);

        cell.Publish(StaminaBand.Tired, 1.7f);
        Assert.AreEqual(1f, cell.Fraction);
        cell.Publish(StaminaBand.Tired, -0.2f);
        Assert.AreEqual(0f, cell.Fraction);
        cell.Publish(StaminaBand.Exhausted, float.NaN);
        Assert.AreEqual(0f, cell.Fraction);
        Assert.AreEqual(StaminaBand.Exhausted, cell.Band);
    }

    [TestMethod]
    public void Take_MoreSoldiersThanTheBudget_TakesTheBudgetInQueueOrder()
    {
        var queue = new StaminaRefreshQueue<object>();
        var soldiers = new[] { new object(), new object(), new object() };
        foreach (var soldier in soldiers)
            queue.Enqueue(soldier, bornAt: 0f, first: false);
        var output = new List<object>();

        queue.Take(now: 10f, budget: 2, minAgeSeconds: 1f, output);

        Assert.AreEqual(2, output.Count);
        Assert.AreSame(soldiers[0], output[0]);
        Assert.AreSame(soldiers[1], output[1]);
        Assert.AreEqual(1, queue.Count);
    }

    [TestMethod]
    public void Take_PlayerQueuedLast_ComesFirst()
    {
        var queue = new StaminaRefreshQueue<object>();
        var soldier = new object();
        var player = new object();
        queue.Enqueue(soldier, bornAt: 0f, first: false);
        queue.Enqueue(player, bornAt: 0f, first: true);
        var output = new List<object>();

        queue.Take(now: 10f, budget: 1, minAgeSeconds: 1f, output);

        Assert.AreSame(player, output[0]);
        Assert.AreEqual(1, queue.Count);
    }

    [TestMethod]
    public void Take_SoldierYoungerThanTheMinimumAge_StaysQueuedUntilOldEnough()
    {
        var queue = new StaminaRefreshQueue<object>();
        var recruit = new object();
        queue.Enqueue(recruit, bornAt: 9.5f, first: false);
        var output = new List<object>();

        queue.Take(now: 10f, budget: 8, minAgeSeconds: 1f, output);
        Assert.AreEqual(0, output.Count);
        Assert.AreEqual(1, queue.Count);

        queue.Take(now: 10.5f, budget: 8, minAgeSeconds: 1f, output);
        Assert.AreSame(recruit, output[0]);
        Assert.AreEqual(0, queue.Count);
    }

    [TestMethod]
    public void Take_BirthTimeNotANumber_NeverRefreshes()
    {
        var queue = new StaminaRefreshQueue<object>();
        queue.Enqueue(new object(), bornAt: float.NaN, first: false);
        var output = new List<object>();

        queue.Take(now: 100f, budget: 8, minAgeSeconds: 1f, output);

        Assert.AreEqual(0, output.Count);
    }

    [TestMethod]
    public void Enqueue_SameSoldierTwice_WaitsOnce_AndCanQueueAgainAfterATake()
    {
        var queue = new StaminaRefreshQueue<object>();
        var soldier = new object();
        queue.Enqueue(soldier, bornAt: 0f, first: false);
        queue.Enqueue(soldier, bornAt: 0f, first: false);
        Assert.AreEqual(1, queue.Count);
        var output = new List<object>();
        queue.Take(now: 10f, budget: 8, minAgeSeconds: 1f, output);

        queue.Enqueue(soldier, bornAt: 0f, first: false);

        Assert.AreEqual(1, queue.Count);
    }

    [TestMethod]
    public void EffectsOf_Fresh_IsNoEffect_AndEachOtherBandIsItsOwn()
    {
        var tuning = StaminaTuning.Default;

        Assert.AreSame(StaminaBandEffects.None, tuning.EffectsOf(StaminaBand.Fresh));
        Assert.AreSame(StaminaTuning.DefaultWinded, tuning.EffectsOf(StaminaBand.Winded));
        Assert.AreSame(StaminaTuning.DefaultTired, tuning.EffectsOf(StaminaBand.Tired));
        Assert.AreSame(StaminaTuning.DefaultExhausted, tuning.EffectsOf(StaminaBand.Exhausted));
        Assert.AreSame(StaminaBandEffects.None, tuning.EffectsOf((StaminaBand)99));
    }
}
