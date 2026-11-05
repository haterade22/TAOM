using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// The per-thread allocation counter the tick profiler reads. TAOM compiles against net472 reference
/// assemblies, which do not declare <c>GC.GetAllocatedBytesForCurrentThread</c>, so the counter is
/// bound by reflection; these tests prove the binding on the desktop CLR the tests run on (the same
/// runtime family as the game) and that a missing method yields null instead of throwing.
/// </summary>
[TestClass]
public class AllocationCounterTests
{
    [TestMethod]
    public void Bind_InstalledFramework_FindsTheCounter()
        => Assert.IsTrue(AllocationCounter.Available,
            "GC.GetAllocatedBytesForCurrentThread did not bind on this CLR; every allocKB field would read na.");

    [TestMethod]
    public void ReadOrZero_AfterAllocating_GrowsByAtLeastTheAllocation()
    {
        var before = AllocationCounter.ReadOrZero();
        var buffer = new byte[1_000_000];
        var after = AllocationCounter.ReadOrZero();
        GC.KeepAlive(buffer);

        Assert.IsTrue(after - before >= 1_000_000, $"Counter grew by {after - before} bytes.");
    }

    [TestMethod]
    public void Bind_MissingMethod_ReturnsNull()
        => Assert.IsNull(AllocationCounter.Bind(typeof(GC), "NoSuchMethod"));
}
