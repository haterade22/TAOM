using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The match speed: 1x leaves the engine's clock alone, 2x and 4x give the mission a longer fixed step, and 10x is the engine's own fast forward.</summary>
[TestClass]
public class FordsGameSpeedTests
{
    [TestMethod]
    public void AtFirst_TheMatchRunsAtOne_AndTheClockIsLeftAlone()
    {
        var speed = new FordsGameSpeed();

        Assert.AreEqual(1, speed.Current);
        Assert.AreEqual((false, 0f), speed.Frame(0.01f));
    }

    [TestMethod]
    public void Cycle_GoesThroughEveryLevel_AndWraps()
    {
        var speed = new FordsGameSpeed();
        var seen = new[] { 0, 0, 0, 0 };
        for (var i = 0; i < 4; i++)
        {
            speed.Cycle();
            seen[i] = speed.Current;
        }

        CollectionAssert.AreEqual(new[] { 2, 4, 10, 1 }, seen);
    }

    [TestMethod]
    public void TwoAndFour_StretchTheFramesTime_IntoAFixedStep()
    {
        var speed = new FordsGameSpeed();

        speed.Set(2);
        Assert.AreEqual((false, 0.02f), speed.Frame(0.01f));

        speed.Set(4);
        Assert.AreEqual((false, 0.04f), speed.Frame(0.01f));
    }

    [TestMethod]
    public void ASlowFrame_IsCapped_SoOneStepNeverJumpsTooFar()
    {
        var speed = new FordsGameSpeed();
        speed.Set(4);

        Assert.AreEqual((false, 0.1f), speed.Frame(0.5f));
    }

    [TestMethod]
    public void AFrameWithNoTime_LeavesTheClockAlone()
    {
        var speed = new FordsGameSpeed();
        speed.Set(4);

        Assert.AreEqual((false, 0f), speed.Frame(0f));
    }

    [TestMethod]
    public void Ten_IsTheEnginesFastForward_WithNoFixedStep()
    {
        var speed = new FordsGameSpeed();
        speed.Set(10);

        Assert.AreEqual((true, 0f), speed.Frame(0.01f));
        Assert.AreEqual((true, 0f), speed.Frame(0f), "the Seat writes level 10 with no frame time to turn its fixed step off, and the step must still read none");
    }

    [TestMethod]
    public void Set_RefusesASpeedThatIsNotALevel()
    {
        var speed = new FordsGameSpeed();
        speed.Set(4);

        speed.Set(3);

        Assert.AreEqual(4, speed.Current);
        Assert.IsTrue(FordsGameSpeed.IsLevel(10));
        Assert.IsFalse(FordsGameSpeed.IsLevel(0));
    }
}
