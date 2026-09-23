using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The per-kind cooldown stamps (#645). They replaced two named floats (LastSlamTime,
/// LastSweepTime) that every layer had to thread by hand; the contract that matters is that a new
/// <see cref="StrikeKind"/> gets a stamp without anyone editing this type, and that "never" is the
/// default rather than mission time zero.
/// </summary>
[TestClass]
public class StrikeKindTimesTests
{
    private static StrikeKind[] AllKinds => Enum.GetValues(typeof(StrikeKind)).Cast<StrikeKind>().ToArray();

    [TestMethod]
    public void Default_EveryKindIsNever()
    {
        var times = default(StrikeKindTimes);

        foreach (var kind in AllKinds)
            Assert.IsTrue(float.IsNaN(times.Get(kind)), $"{kind} must start as never (NaN), not t=0");
    }

    [TestMethod]
    public void With_EveryKind_RoundTripsAndLeavesTheOthersNever()
    {
        foreach (var kind in AllKinds)
        {
            var times = default(StrikeKindTimes).With(kind, 12.5f);

            Assert.AreEqual(12.5f, times.Get(kind), 0.0001f, kind.ToString());
            foreach (var other in AllKinds.Where(k => k != kind))
                Assert.IsTrue(float.IsNaN(times.Get(other)), $"stamping {kind} touched {other}");
        }
    }

    [TestMethod]
    public void With_ReturnsACopy_TheOriginalIsUnchanged()
    {
        // The context carries a copy of the roster's value; a later stamp must not reach it.
        var first = default(StrikeKindTimes).With(StrikeKind.Slam, 1f);
        var second = first.With(StrikeKind.Sweep, 2f);

        Assert.IsTrue(float.IsNaN(first.Get(StrikeKind.Sweep)));
        Assert.AreEqual(1f, second.Get(StrikeKind.Slam), 0.0001f);
        Assert.AreEqual(2f, second.Get(StrikeKind.Sweep), 0.0001f);
    }

    [TestMethod]
    public void With_ExistingStamp_IsOverwritten()
    {
        var times = default(StrikeKindTimes).With(StrikeKind.Scream, 5f).With(StrikeKind.Scream, 20f);

        Assert.AreEqual(20f, times.Get(StrikeKind.Scream), 0.0001f);
    }

    [TestMethod]
    public void With_UndefinedKindValue_ReturnsTheValueUnchanged()
    {
        var times = default(StrikeKindTimes).With(StrikeKind.Slam, 3f).With((StrikeKind)99, 7f);

        Assert.AreEqual(3f, times.Get(StrikeKind.Slam), 0.0001f);
        Assert.IsTrue(float.IsNaN(times.Get((StrikeKind)99)));
    }

    [TestMethod]
    public void StrikeKindValues_AreContiguousFromZero()
    {
        // StrikeKindTimes indexes by the member's value; a gap or an explicit large value would
        // silently drop that kind's stamp. Append new kinds at the end.
        var values = AllKinds.Select(k => (int)k).OrderBy(v => v).ToArray();

        CollectionAssert.AreEqual(Enumerable.Range(0, values.Length).ToArray(), values);
    }
}
