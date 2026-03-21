using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WeatherBoundsGuard;

namespace TAOM.Tests.Features.WeatherBoundsGuard;

[TestClass]
public class WeatherPositionClamperTests
{
    [TestMethod]
    public void ClampPosition_InsideBounds_ReturnsUnchanged()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(50f, 75f, 100f, 200f);

        Assert.AreEqual(50f, x);
        Assert.AreEqual(75f, y);
    }

    [TestMethod]
    public void ClampPosition_AtExactBoundary_ClampsBelow()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(100f, 200f, 100f, 200f);

        Assert.IsTrue(x < 100f, $"x should be clamped below 100, was {x}");
        Assert.IsTrue(y < 200f, $"y should be clamped below 200, was {y}");
        Assert.IsTrue(x > 99f, $"x should be close to 100, was {x}");
        Assert.IsTrue(y > 199f, $"y should be close to 200, was {y}");
    }

    [TestMethod]
    public void ClampPosition_BeyondBoundary_ClampsToMax()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(150f, 300f, 100f, 200f);

        Assert.IsTrue(x < 100f, $"x should be clamped below 100, was {x}");
        Assert.IsTrue(y < 200f, $"y should be clamped below 200, was {y}");
    }

    [TestMethod]
    public void ClampPosition_NegativePosition_ClampsToZero()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(-5f, -10f, 100f, 200f);

        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void ClampPosition_ZeroTerrainSize_ReturnsZero()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(50f, 75f, 0f, 0f);

        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void ClampPosition_NegativeTerrainSize_ReturnsZero()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(50f, 75f, -100f, -200f);

        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void ClampPosition_AtOrigin_ReturnsZero()
    {
        var (x, y) = WeatherPositionClamper.ClampPosition(0f, 0f, 100f, 200f);

        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }
}
