using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The buff tracker is read behind the agent stat model, which the engine can reach from a
/// native callback, and written by ability activations and their restores (#595). Contribution
/// counting must hold under the lock, and the lock must hold under contention.
/// </summary>
[TestClass]
public class CareerAbilityBuffTrackerTests
{
    [TestInitialize]
    public void Reset() => CareerAbilityBuffTracker.ClearAll();

    [TestCleanup]
    public void Cleanup() => CareerAbilityBuffTracker.ClearAll();

    private static ActiveBuffs Deltas(float damage) => new ActiveBuffs { DamageBonus = damage };

    [TestMethod]
    public void AddAllyContribution_ThenRemove_RetiresTheEntry()
    {
        CareerAbilityBuffTracker.AddAllyContribution(7, Deltas(2f));
        Assert.IsNotNull(CareerAbilityBuffTracker.GetAllyBuff(7));

        CareerAbilityBuffTracker.RemoveAllyContribution(7, Deltas(2f));

        Assert.IsNull(CareerAbilityBuffTracker.GetAllyBuff(7), "the last contribution's restore retires the entry");
    }

    [TestMethod]
    public void RemoveAllyContribution_AfterClearAllyBuff_IsANoOp()
    {
        CareerAbilityBuffTracker.AddAllyContribution(7, Deltas(2f));
        CareerAbilityBuffTracker.ClearAllyBuff(7);

        CareerAbilityBuffTracker.RemoveAllyContribution(7, Deltas(2f));

        Assert.IsNull(CareerAbilityBuffTracker.GetAllyBuff(7));
    }

    [TestMethod]
    public void TwoContributions_OneRemoved_KeepsTheEntryWithTheOtherDelta()
    {
        CareerAbilityBuffTracker.AddAllyContribution(7, Deltas(2f));
        CareerAbilityBuffTracker.AddAllyContribution(7, Deltas(3f));

        CareerAbilityBuffTracker.RemoveAllyContribution(7, Deltas(2f));

        var buff = CareerAbilityBuffTracker.GetAllyBuff(7);
        Assert.IsNotNull(buff);
        Assert.AreEqual(1, buff.ActiveContributions);
        Assert.AreEqual(3f, buff.DamageBonus, 0.001f);
    }

    [TestMethod]
    public void ConcurrentContributions_FromSeveralThreads_NeverThrow_AndBalanceToZero()
    {
        var threads = new Thread[8];
        Exception failure = null;
        for (int t = 0; t < threads.Length; t++)
        {
            int seed = t;
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int n = 0; n < 2000; n++)
                    {
                        int agent = (n + seed) % 16;
                        CareerAbilityBuffTracker.AddAllyContribution(agent, Deltas(1f));
                        CareerAbilityBuffTracker.GetAllyBuff(agent);
                        CareerAbilityBuffTracker.GetBuffedAllyIndices();
                        CareerAbilityBuffTracker.RemoveAllyContribution(agent, Deltas(1f));
                    }
                }
                catch (Exception e) { failure = e; }
            });
        }
        foreach (var th in threads) th.Start();
        foreach (var th in threads) th.Join();

        Assert.IsNull(failure, failure?.ToString());
        Assert.AreEqual(0, CareerAbilityBuffTracker.GetBuffedAllyIndices().Count, "every add was matched by a remove");
    }
}
