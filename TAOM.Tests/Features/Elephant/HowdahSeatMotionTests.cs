using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The two rules that decide whether a howdah archer can shoot at all (#627, 2026-09-20). Both were learned the
/// expensive way, over eight in-game rounds: correcting a resting archer's position every frame, or letting two
/// archers crowd each other, makes the engine read tens of m/s of movement and no bow draw ever completes.
/// </summary>
[TestClass]
public class HowdahSeatMotionTests
{
    private const float Deadband = ElephantConfig.HowdahSeatDeadbandMetres;

    [TestMethod]
    public void TheDeadband_IsWiderThanTheMeasuredSettle()
    {
        // A seated archer settles about 0.10 m from its frame. A deadband at or below that teleports every frame,
        // which is the state that stopped the crew shooting.
        Assert.IsTrue(Deadband > 0.11f, $"deadband {Deadband} is inside the measured 0.10 m settle");
        Assert.IsTrue(Deadband < 0.4f, $"deadband {Deadband} is wide enough to let an archer visibly lag its seat");
    }

    [TestMethod]
    public void ARestingArcher_IsLeftAlone()
    {
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(0.10f * 0.10f, Deadband), "0.10 m is the normal settle");
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(0f, Deadband), "an archer exactly on its frame");
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(Deadband * Deadband, Deadband), "exactly at the deadband");
    }

    [TestMethod]
    public void AnArcherThatHasActuallyDrifted_IsPlacedBack()
    {
        Assert.IsTrue(HowdahSeatMotion.ShouldCorrect(0.3f * 0.3f, Deadband));
        Assert.IsTrue(HowdahSeatMotion.ShouldCorrect(4f * 4f, Deadband), "an elephant that moved a lot in one frame");
    }

    [TestMethod]
    public void ANonFiniteDistance_NeverTeleports()
    {
        // A native position can come back NaN; `>` is false for NaN either way, so this is explicit rather than lucky.
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(float.NaN, Deadband));
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(float.PositiveInfinity, Deadband));
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(1f, float.NaN));
        Assert.IsFalse(HowdahSeatMotion.ShouldCorrect(1f, -1f));
    }

    [TestMethod]
    public void ANonFiniteSeatPosition_IsNeverHandedToTheEngine()
    {
        // Both the teleport and the scripted position write this straight to native, and the seat frame comes from
        // the elephant's own position with nothing validating it on the way.
        Assert.IsTrue(HowdahSeatMotion.IsPlaceable(1f, -2f, 3.2f));
        Assert.IsFalse(HowdahSeatMotion.IsPlaceable(float.NaN, 0f, 0f));
        Assert.IsFalse(HowdahSeatMotion.IsPlaceable(0f, float.NaN, 0f));
        Assert.IsFalse(HowdahSeatMotion.IsPlaceable(0f, 0f, float.NaN));
        Assert.IsFalse(HowdahSeatMotion.IsPlaceable(float.PositiveInfinity, 0f, 0f));
        Assert.IsFalse(HowdahSeatMotion.IsPlaceable(0f, float.NegativeInfinity, 0f));
    }

    [TestMethod]
    public void TwoFrames_MustStandAtLeastTwoCapsuleRadiiApart()
    {
        Assert.AreEqual(0.74f, HowdahSeatMotion.MinimumFrameSeparation, 1e-6f);
        Assert.IsFalse(HowdahSeatMotion.FramesAreClear(0.50f), "two abreast on this deck: they shove each other");
        Assert.IsFalse(HowdahSeatMotion.FramesAreClear(float.NaN));
        Assert.IsTrue(HowdahSeatMotion.FramesAreClear(0.74f), "touching exactly is clear");
        Assert.IsTrue(HowdahSeatMotion.FramesAreClear(0.78f), "the shipped layout");
    }
}
