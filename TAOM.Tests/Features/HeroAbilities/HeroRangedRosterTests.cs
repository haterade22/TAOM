using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroRangedRosterTests
{
    private sealed class Key
    {
    }

    [TestMethod]
    public void Add_ThenKitOf()
    {
        var sut = new HeroRangedRoster<Key>();
        var key = new Key();
        var kit = new HeroRangedConfig { ZeroAimError = true };

        sut.Add(key, kit);

        Assert.AreSame(kit, sut.KitOf(key));
        Assert.IsFalse(sut.IsEmpty);
        Assert.IsNull(sut.KitOf(new Key()), "keyed by reference");
    }

    [TestMethod]
    public void Remove_ThenNull()
    {
        var sut = new HeroRangedRoster<Key>();
        var key = new Key();
        sut.Add(key, new HeroRangedConfig());

        Assert.IsTrue(sut.Remove(key));
        Assert.IsNull(sut.KitOf(key));
        Assert.IsTrue(sut.IsEmpty);
        Assert.IsFalse(sut.Remove(key));
    }

    [TestMethod]
    public void NullKey_Null()
    {
        var sut = new HeroRangedRoster<Key>();
        sut.Add(new Key(), new HeroRangedConfig());

        Assert.IsNull(sut.KitOf(null));
        Assert.IsFalse(sut.Remove(null!));
    }

    [TestMethod]
    public void Clear_EmptiesAll()
    {
        var sut = new HeroRangedRoster<Key>();
        var a = new Key();
        sut.Add(a, new HeroRangedConfig());
        sut.Add(new Key(), new HeroRangedConfig());

        sut.Clear();

        Assert.IsTrue(sut.IsEmpty);
        Assert.IsNull(sut.KitOf(a));
    }

    [TestMethod]
    public void ReAdd_Replaces()
    {
        var sut = new HeroRangedRoster<Key>();
        var key = new Key();
        var second = new HeroRangedConfig { PierceShields = true };
        sut.Add(key, new HeroRangedConfig());

        sut.Add(key, second);

        Assert.AreSame(second, sut.KitOf(key));
    }

    // A reader never sees a half-built map: every lookup of a key that stays listed finds its kit
    // while another thread adds and removes other keys.
    [TestMethod]
    public void ConcurrentReaders_SeeWholeSnapshots()
    {
        var sut = new HeroRangedRoster<Key>();
        var stable = new Key();
        var kit = new HeroRangedConfig();
        sut.Add(stable, kit);
        var stop = false;
        var misses = 0;

        var reader = Task.Run(() =>
        {
            while (!Volatile.Read(ref stop))
                if (!ReferenceEquals(sut.KitOf(stable), kit))
                    Interlocked.Increment(ref misses);
        });
        var churn = new List<Key>();
        for (var i = 0; i < 2000; i++)
        {
            var k = new Key();
            churn.Add(k);
            sut.Add(k, new HeroRangedConfig());
            if (i % 3 == 0)
                sut.Remove(churn[i / 3]);
        }
        Volatile.Write(ref stop, true);
        reader.Wait();

        Assert.AreEqual(0, misses);
    }

    // The ranged mission logic samples each listed hero once a second; a later write must not change a list it holds.
    [TestMethod]
    public void Keys_AreASnapshot()
    {
        var sut = new HeroRangedRoster<Key>();
        var a = new Key();
        var b = new Key();
        sut.Add(a, new HeroRangedConfig());

        var keys = sut.Keys;
        sut.Add(b, new HeroRangedConfig());
        sut.Remove(a);

        CollectionAssert.AreEqual(new[] { a }, new List<Key>(keys));
        CollectionAssert.AreEqual(new[] { b }, new List<Key>(sut.Keys));
    }
}
