using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapViewRelease;

namespace TAOM.Tests.Features.MapViewRelease;

/// <summary>Every line the map-view release writes, pinned literally.</summary>
[TestClass]
public class MapViewReleaseLinesTests
{
    [TestMethod]
    public void InstallLine_ToggleOn_NamesTheInterval()
    {
        Assert.AreEqual("[MapViewRelease] ON: the campaign map's view releases its render targets once per 20 covers (toggle on)",
            MapViewReleaseLines.BuildInstallLine(toggleOn: true, interval: 20));
    }

    [TestMethod]
    public void InstallLine_IntervalOne_SaysEveryCover()
    {
        Assert.AreEqual("[MapViewRelease] ON: the campaign map's view releases its render targets on every cover (toggle on)",
            MapViewReleaseLines.BuildInstallLine(toggleOn: true, interval: 1));
    }

    [TestMethod]
    public void InstallLine_ToggleOff()
    {
        Assert.AreEqual("[MapViewRelease] installed, toggle off: closed menus leave the map's render targets alone as in the vanilla game",
            MapViewReleaseLines.BuildInstallLine(toggleOn: false, interval: 20));
    }

    [TestMethod]
    public void ReleasedLine_CarriesBothCounts()
    {
        Assert.AreEqual("[MapViewRelease] released 3 times, map covered 60 times", MapViewReleaseLines.BuildReleasedLine(3, 60));
    }

    [TestMethod]
    public void SwitchedOffLine_CarriesTheException()
    {
        var line = MapViewReleaseLines.BuildSwitchedOffLine(new InvalidOperationException("boom"));

        StringAssert.StartsWith(line, "[MapViewRelease] OFF after an error, the map view is no longer released for the rest of this game launch: ");
        StringAssert.Contains(line, "InvalidOperationException: boom");
    }
}
