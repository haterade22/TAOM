using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapViewRelease;

namespace TAOM.Tests.Features.MapViewRelease;

[TestClass]
public class MapViewReleaseCounterTests
{
    // ---- ClampInterval: 1 to 1000 ----

    [TestMethod]
    public void ClampInterval_InsideTheRange_IsUnchanged()
    {
        Assert.AreEqual(1, MapViewReleaseCounter.ClampInterval(1));
        Assert.AreEqual(20, MapViewReleaseCounter.ClampInterval(20));
        Assert.AreEqual(1000, MapViewReleaseCounter.ClampInterval(1000));
    }

    [TestMethod]
    public void ClampInterval_BelowOne_IsOne()
    {
        Assert.AreEqual(1, MapViewReleaseCounter.ClampInterval(0));
        Assert.AreEqual(1, MapViewReleaseCounter.ClampInterval(-5));
        Assert.AreEqual(1, MapViewReleaseCounter.ClampInterval(int.MinValue));
    }

    [TestMethod]
    public void ClampInterval_AboveAThousand_IsAThousand()
    {
        Assert.AreEqual(1000, MapViewReleaseCounter.ClampInterval(1001));
        Assert.AreEqual(1000, MapViewReleaseCounter.ClampInterval(int.MaxValue));
    }

    [TestMethod]
    public void Defaults_AreTwentyWithinOneToAThousand()
    {
        Assert.AreEqual(20, MapViewReleaseCounter.DefaultInterval);
        Assert.AreEqual(1, MapViewReleaseCounter.MinInterval);
        Assert.AreEqual(1000, MapViewReleaseCounter.MaxInterval);
    }

    // ---- Cover: which one releases ----

    private static List<bool> Run(MapViewReleaseCounter counter, int interval, int covers)
    {
        var result = new List<bool>();
        for (var i = 0; i < covers; i++) result.Add(counter.Cover(interval));
        return result;
    }

    [TestMethod]
    public void Cover_EveryThird_ReleasesOnTheThirdSixthAndNinth()
    {
        var sut = new MapViewReleaseCounter();

        var released = Run(sut, 3, 9);

        CollectionAssert.AreEqual(new[] { false, false, true, false, false, true, false, false, true }, released);
    }

    [TestMethod]
    public void Cover_IntervalOne_ReleasesEveryCover()
    {
        var sut = new MapViewReleaseCounter();

        CollectionAssert.AreEqual(new[] { true, true, true }, Run(sut, 1, 3));
    }

    [TestMethod]
    public void Cover_DefaultInterval_ReleasesOnTheTwentiethNotTheFirst()
    {
        var sut = new MapViewReleaseCounter();

        var released = Run(sut, 20, 40);

        Assert.IsFalse(released[0]);
        Assert.IsTrue(released[19]);
        Assert.IsTrue(released[39]);
        Assert.AreEqual(2, released.FindAll(r => r).Count);
    }

    [TestMethod]
    public void Cover_CountsCovers_ButNotReleases()
    {
        var sut = new MapViewReleaseCounter();

        var due = Run(sut, 4, 10);

        Assert.AreEqual(10, sut.Covers);
        Assert.AreEqual(2, due.FindAll(r => r).Count, "two covers were due");
        Assert.AreEqual(0, sut.Releases, "a due cover is not a release that ran");
    }

    [TestMethod]
    public void Released_CountsOnlyTheReleasesThatRan()
    {
        var sut = new MapViewReleaseCounter();
        Run(sut, 4, 12);                     // three due covers

        sut.Released();
        sut.Released();

        Assert.AreEqual(2, sut.Releases);
        Assert.AreEqual(12, sut.Covers);
    }

    [TestMethod]
    public void Cover_AnIntervalOutsideTheRange_IsClampedBeforeUse()
    {
        var zero = new MapViewReleaseCounter();
        var huge = new MapViewReleaseCounter();

        Assert.IsTrue(zero.Cover(0), "0 acts as 1");
        Assert.IsFalse(huge.Cover(int.MaxValue));
        CollectionAssert.AreEqual(new bool[998], Run(huge, int.MaxValue, 998), "999 covers so far, still short of 1000");
        Assert.IsTrue(huge.Cover(int.MaxValue), "the 1000th");
    }

    [TestMethod]
    public void Cover_IntervalLoweredMidway_ReleasesAtOnceWhenTheCountHasPassedIt()
    {
        var sut = new MapViewReleaseCounter();
        Run(sut, 20, 10);

        Assert.IsTrue(sut.Cover(5), "10 covers since the last release, and the limit is now 5");
        Assert.IsFalse(sut.Cover(5), "and the count starts again");
    }

    [TestMethod]
    public void Cover_IntervalRaisedMidway_WaitsForTheNewLimit()
    {
        var sut = new MapViewReleaseCounter();
        Run(sut, 3, 2);

        Assert.IsFalse(sut.Cover(5), "3 covers since the last release, limit 5");
        Assert.IsFalse(sut.Cover(5));
        Assert.IsTrue(sut.Cover(5), "the fifth");
    }
}
