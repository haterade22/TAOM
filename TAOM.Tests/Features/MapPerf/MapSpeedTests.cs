using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapPerf;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The Patch101 speed class of a map frame, from the engine's <c>CampaignTimeControlMode</c> (0 to 6) and
/// <c>SpeedUpMultiplier</c>, against TAOM's fast-forward and extra fast-forward multipliers (4 and 8 here).
/// </summary>
[TestClass]
public class MapSpeedTests
{
    private static MapSpeedClass Classify(int mode, float multiplier) => MapSpeed.Classify(mode, multiplier, 4, 8);

    [TestMethod]
    public void Classify_StopAndFastForwardStop_ReturnStop()
    {
        Assert.AreEqual(MapSpeedClass.Stop, Classify(0, 4f));
        Assert.AreEqual(MapSpeedClass.Stop, Classify(6, 4f));
    }

    [TestMethod]
    public void Classify_UnstoppableAndStoppablePlay_ReturnPlay()
    {
        Assert.AreEqual(MapSpeedClass.Play, Classify(1, 4f));
        Assert.AreEqual(MapSpeedClass.Play, Classify(3, 4f));
    }

    [TestMethod]
    public void Classify_FastForwardModesAtTheFastMultiplier_ReturnFF()
    {
        Assert.AreEqual(MapSpeedClass.FF, Classify(2, 4f));
        Assert.AreEqual(MapSpeedClass.FF, Classify(4, 4f));
        Assert.AreEqual(MapSpeedClass.FF, Classify(5, 4f));
        Assert.AreEqual(MapSpeedClass.FF, Classify(2, 1f));
    }

    [TestMethod]
    public void Classify_AboveFastUpToExtra_ReturnsFF2()
    {
        Assert.AreEqual(MapSpeedClass.FF2, Classify(2, 4.5f));
        Assert.AreEqual(MapSpeedClass.FF2, Classify(4, 8f));
    }

    [TestMethod]
    public void Classify_AboveExtra_ReturnsFF3()
    {
        Assert.AreEqual(MapSpeedClass.FF3, Classify(2, 16f));
    }

    [TestMethod]
    public void Classify_UnknownMode_ReturnsUnknown()
    {
        Assert.AreEqual(MapSpeedClass.Unknown, Classify(7, 4f));
        Assert.AreEqual(MapSpeedClass.Unknown, Classify(-1, 4f));
    }

    [TestMethod]
    public void Classify_NonFiniteMultiplierInAFastForwardMode_ReturnsUnknown()
    {
        Assert.AreEqual(MapSpeedClass.Unknown, Classify(2, float.NaN));
        Assert.AreEqual(MapSpeedClass.Unknown, Classify(4, float.PositiveInfinity));
    }

    [TestMethod]
    public void Token_EveryClass_MatchesTheLineContract()
    {
        Assert.AreEqual("na", MapSpeed.Token(MapSpeedClass.Unknown));
        Assert.AreEqual("Stop", MapSpeed.Token(MapSpeedClass.Stop));
        Assert.AreEqual("Play", MapSpeed.Token(MapSpeedClass.Play));
        Assert.AreEqual("FF", MapSpeed.Token(MapSpeedClass.FF));
        Assert.AreEqual("FF2", MapSpeed.Token(MapSpeedClass.FF2));
        Assert.AreEqual("FF3", MapSpeed.Token(MapSpeedClass.FF3));
    }
}
