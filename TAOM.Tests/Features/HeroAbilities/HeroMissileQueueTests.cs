using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// Native may raise the missile callbacks off the main thread (#634), so they only enqueue and the mission tick drains.
/// </summary>
[TestClass]
public class HeroMissileQueueTests
{
    [TestMethod]
    public void WorkerEnqueues_MainDrainSeesAll()
    {
        var queue = new HeroMissileQueue();
        const int workers = 8;
        const int each = 500;

        Parallel.For(0, workers, w =>
        {
            for (var i = 0; i < each; i++)
                queue.Enqueue(HeroMissileEvent.Hit(null!, null!, null, null, w * each + i, "", false, false, false));
        });

        var seen = new bool[workers * each];
        while (queue.TryDequeue(out var e))
            seen[e.MissileIndex] = true;

        Assert.IsTrue(seen.All(s => s), "every event a worker enqueued reaches the drain");
        Assert.IsFalse(queue.TryDequeue(out _));
    }

    [TestMethod]
    public void Hit_CarriesTheMissileItem()
    {
        var e = HeroMissileEvent.Hit(null!, null!, null, null, 3, "taom_legolas_arrow", false, false, false);

        Assert.AreEqual(HeroMissileKind.MissileHit, e.Kind);
        Assert.AreEqual("taom_legolas_arrow", e.ItemId);
    }

    // A missile's own hit callbacks come before its removal, and the drain is first in, first out, so the tally sees a
    // removal after the hits of the arrow it ends and before the hits of the next arrow on that index.
    [TestMethod]
    public void Removed_DrainsInOrderBetweenTheHitsOfTwoArrowsOnOneIndex()
    {
        var queue = new HeroMissileQueue();
        queue.Enqueue(HeroMissileEvent.Hit(null!, null!, null, null, 7, "", true, false, false));
        queue.Enqueue(HeroMissileEvent.Removed(7));
        queue.Enqueue(HeroMissileEvent.Hit(null!, null!, null, null, 7, "", false, false, false));

        var kinds = new[] { Next(queue), Next(queue), Next(queue) };

        CollectionAssert.AreEqual(new[] { HeroMissileKind.MissileHit, HeroMissileKind.Removed, HeroMissileKind.MissileHit }, kinds.Select(k => k.Kind).ToArray());
        Assert.AreEqual(7, kinds[1].MissileIndex);
    }

    [TestMethod]
    public void Clear_DropsEverything()
    {
        var queue = new HeroMissileQueue();
        queue.Enqueue(HeroMissileEvent.Hit(null!, null!, null, null, 1, "", false, false, false));
        queue.Enqueue(HeroMissileEvent.Removed(1));

        queue.Clear();

        Assert.IsFalse(queue.TryDequeue(out _));
    }

    private static HeroMissileEvent Next(HeroMissileQueue queue)
    {
        Assert.IsTrue(queue.TryDequeue(out var e));
        return e;
    }
}
