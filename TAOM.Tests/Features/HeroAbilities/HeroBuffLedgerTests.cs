using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroBuffLedgerTests
{
    private readonly HeroBuffLedger<object> _sut = new HeroBuffLedger<object>();
    private readonly object _a = new object();
    private readonly object _b = new object();

    private static RaceAbilityEffects Fx(float melee = 0f, float reduction = 0f) =>
        new RaceAbilityEffects { MeleeDamagePercent = melee, DamageReductionPercent = reduction };

    [TestMethod]
    public void Grant_TwoSources_MergesStrongerPerChannel()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f, reduction: 5f), 10f);
        _sut.Grant(_a, "two", Fx(melee: 10f, reduction: 15f), 10f);

        var merged = _sut.EffectsOf(_a)!;
        Assert.AreEqual(20f, merged.MeleeDamagePercent, 0.001f);
        Assert.AreEqual(15f, merged.DamageReductionPercent, 0.001f);
    }

    [TestMethod]
    public void Grant_SameSource_ReplacesAndMovesEnd()
    {
        var changed = new List<object>();
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);
        _sut.Grant(_a, "one", Fx(melee: 5f), 30f);

        Assert.AreEqual(5f, _sut.EffectsOf(_a)!.MeleeDamagePercent, 0.001f);
        _sut.Expire(20f, changed);
        Assert.AreEqual(0, changed.Count);
        Assert.IsNotNull(_sut.EffectsOf(_a));
        _sut.Expire(30f, changed);
        Assert.IsNull(_sut.EffectsOf(_a));
    }

    [TestMethod]
    public void Grant_IsPublishedBeforeReturn()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);

        Assert.AreEqual(1, _sut.Count);
        Assert.IsNotNull(_sut.EffectsOf(_a));
    }

    [TestMethod]
    public void Grant_NaNUntil_IsIgnored()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f), float.NaN);

        Assert.IsNull(_sut.EffectsOf(_a));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Grant_NullEffects_IsIgnored()
    {
        _sut.Grant(_a, "one", null!, 10f);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Expire_OneOfTwo_KeepsOtherAndListsChange()
    {
        var changed = new List<object>();
        _sut.Grant(_a, "short", Fx(melee: 20f), 10f);
        _sut.Grant(_a, "long", Fx(reduction: 15f), 30f);
        _sut.Grant(_b, "other", Fx(melee: 1f), 30f);

        _sut.Expire(10f, changed);

        CollectionAssert.AreEqual(new[] { _a }, changed);
        var left = _sut.EffectsOf(_a)!;
        Assert.AreEqual(0f, left.MeleeDamagePercent, 0.001f);
        Assert.AreEqual(15f, left.DamageReductionPercent, 0.001f);
        Assert.IsNotNull(_sut.EffectsOf(_b));
    }

    [TestMethod]
    public void Expire_Infinity_NeverExpires()
    {
        var changed = new List<object>();
        _sut.Grant(_a, "passive", Fx(melee: 20f), float.PositiveInfinity);

        _sut.Expire(float.MaxValue, changed);

        Assert.AreEqual(0, changed.Count);
        Assert.IsNotNull(_sut.EffectsOf(_a));
    }

    [TestMethod]
    public void Expire_NaNNow_ExpiresNothing()
    {
        var changed = new List<object>();
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);

        _sut.Expire(float.NaN, changed);

        Assert.AreEqual(0, changed.Count);
        Assert.IsNotNull(_sut.EffectsOf(_a));
    }

    [TestMethod]
    public void Remove_Key_EffectsOfNull()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);

        Assert.IsTrue(_sut.Remove(_a));
        Assert.IsFalse(_sut.Remove(_a));
        Assert.IsNull(_sut.EffectsOf(_a));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void DistinctKeys_DoNotShare()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);

        Assert.IsNull(_sut.EffectsOf(_b));
        Assert.IsNull(_sut.EffectsOf(null!));
    }

    [TestMethod]
    public void Clear_EmptiesAll()
    {
        _sut.Grant(_a, "one", Fx(melee: 20f), 10f);
        _sut.Grant(_b, "one", Fx(melee: 20f), 10f);

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
        Assert.IsNull(_sut.EffectsOf(_a));
        _sut.Grant(_a, "one", Fx(melee: 7f), 10f);
        Assert.AreEqual(7f, _sut.EffectsOf(_a)!.MeleeDamagePercent, 0.001f);
    }

    [TestMethod]
    public void ConcurrentReaders_DuringWrites_NeverThrowAndSeeWholeValues()
    {
        var failures = new List<string>();
        var stop = false;
        var readers = new List<Task>();
        for (var r = 0; r < 3; r++)
        {
            readers.Add(Task.Run(() =>
            {
                try
                {
                    while (!Volatile.Read(ref stop))
                    {
                        var e = _sut.EffectsOf(_a);
                        if (e != null && e.MeleeDamagePercent != e.DamageReductionPercent)
                            lock (failures) failures.Add("torn value");
                        _ = _sut.Count;
                    }
                }
                catch (Exception ex)
                {
                    lock (failures) failures.Add(ex.Message);
                }
            }));
        }

        var changed = new List<object>();
        for (var i = 1; i <= 3000; i++)
        {
            _sut.Grant(_a, "s" + (i % 3), Fx(melee: i % 50, reduction: i % 50), i + 5f);
            _sut.Grant(_b, "s", Fx(melee: 1f, reduction: 1f), i + 1f);
            changed.Clear();
            _sut.Expire(i, changed);
            if (i % 500 == 0)
                _sut.Remove(_a);
        }
        Volatile.Write(ref stop, true);
        Task.WaitAll(readers.ToArray());

        Assert.AreEqual(0, failures.Count, string.Join(";", failures));
    }
}
