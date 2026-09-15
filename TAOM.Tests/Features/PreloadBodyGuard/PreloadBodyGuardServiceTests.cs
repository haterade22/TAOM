using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PreloadBodyGuard;

namespace TAOM.Tests.Features.PreloadBodyGuard;

/// <summary>
/// <c>PreloadHelper.WaitForMeshesToBeLoaded</c> counts every registered collision-body name that
/// <c>PhysicsShape.GetFromResource(name, true)</c> returns null for, on every pass of a do/while
/// with no exit. A name no tpac ships never resolves, so one bad <c>body_name</c> freezes the game
/// on the first frame after the loading window drops (#352, #599, #601). The guard repairs the
/// precondition vanilla relies on: it drains the names that cannot resolve before the loop runs.
///
/// The service is pure: the resolver, the clock and the sleep are injected, so "cannot resolve"
/// versus "not loaded yet" is decided against a budget the test controls, not wall time.
/// </summary>
[TestClass]
public class PreloadBodyGuardServiceTests
{
    private const double Budget = 5.0;

    private sealed class FakeClock
    {
        public double Elapsed;
        public int Sleeps;
        public double SecondsPerSleep = 0.001;

        public double Now() => Elapsed;

        public void SleepOnce()
        {
            Sleeps++;
            Elapsed += SecondsPerSleep;
        }
    }

    [TestMethod]
    public void AllNamesResolveOnFirstPass_NothingDroppedAndNoSleep()
    {
        var names = new HashSet<string> { "bo_sword", "bo_shield" };
        var clock = new FakeClock();

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, _ => true, clock.Now, clock.SleepOnce, Budget);

        Assert.AreEqual(0, dropped.Count);
        Assert.AreEqual(2, names.Count);
        Assert.AreEqual(0, clock.Sleeps, "a healthy load must pay nothing measurable");
    }

    [TestMethod]
    public void NameThatNeverResolves_IsDroppedAfterTheBudget_OthersKept()
    {
        var names = new HashSet<string> { "bo_sword", "bo_wm_elven_bow_v1" };
        var clock = new FakeClock { SecondsPerSleep = 0.5 };

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, n => n != "bo_wm_elven_bow_v1", clock.Now, clock.SleepOnce, Budget);

        CollectionAssert.AreEqual(new[] { "bo_wm_elven_bow_v1" }, dropped.ToList());
        CollectionAssert.AreEquivalent(new[] { "bo_sword" }, names.ToList());
        Assert.IsTrue(clock.Elapsed >= Budget, "the drop must wait out the whole budget");
    }

    [TestMethod]
    public void NameThatResolvesOnTheThirdPass_IsKept()
    {
        var names = new HashSet<string> { "bo_late" };
        var clock = new FakeClock();
        int calls = 0;

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, _ => ++calls >= 3, clock.Now, clock.SleepOnce, Budget);

        Assert.AreEqual(0, dropped.Count);
        Assert.AreEqual(1, names.Count);
        Assert.AreEqual(2, clock.Sleeps, "two failed passes, then resolved");
        Assert.IsTrue(clock.Elapsed < Budget);
    }

    [TestMethod]
    public void EmptySet_ReturnsAtOnce()
    {
        var names = new HashSet<string>();
        var clock = new FakeClock();

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, _ => throw new InvalidOperationException("must not be called"), clock.Now, clock.SleepOnce, Budget);

        Assert.AreEqual(0, dropped.Count);
        Assert.AreEqual(0, clock.Sleeps);
    }

    [TestMethod]
    public void BudgetIsMeasuredAgainstTheInjectedClock()
    {
        var names = new HashSet<string> { "bo_never" };
        var clock = new FakeClock { SecondsPerSleep = 2.0 };

        new PreloadBodyGuardService().DrainUnresolvable(names, _ => false, clock.Now, clock.SleepOnce, 3.0);

        Assert.AreEqual(2, clock.Sleeps, "0 -> 2 -> 4 s: the second sleep crosses a 3 s budget");
        Assert.AreEqual(0, names.Count);
    }

    [TestMethod]
    public void ResolverThatThrows_CountsAsUnresolvedAndNeverEscapes()
    {
        var names = new HashSet<string> { "bo_bad", "bo_good" };
        var clock = new FakeClock { SecondsPerSleep = 1.0 };

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, n => n == "bo_bad" ? throw new InvalidOperationException("native says no") : true,
            clock.Now, clock.SleepOnce, 2.0);

        CollectionAssert.AreEqual(new[] { "bo_bad" }, dropped.ToList());
        CollectionAssert.AreEquivalent(new[] { "bo_good" }, names.ToList());
    }

    [TestMethod]
    public void NonPositiveBudget_DropsUnresolvedNamesWithoutSleeping()
    {
        var names = new HashSet<string> { "bo_never", "bo_good" };
        var clock = new FakeClock();

        var dropped = new PreloadBodyGuardService().DrainUnresolvable(
            names, n => n == "bo_good", clock.Now, clock.SleepOnce, 0.0);

        CollectionAssert.AreEqual(new[] { "bo_never" }, dropped.ToList());
        Assert.AreEqual(0, clock.Sleeps);
    }
}
