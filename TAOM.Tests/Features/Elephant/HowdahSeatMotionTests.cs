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
    // --- Howdah height from the live spine (2026-09-22) ---
    // The visible howdah deck is skinned to Spine1_05 and bobs with it, about 0.18 m peak to peak on a 1.3x elephant.
    // A platform at a fixed height sits inside that bob and the archers stutter; one below it looks sunk. So the
    // platform takes its height from the live spine. These pin the arithmetic; the bone read lives in the machine.

    [TestMethod]
    public void RootAboveFeetFromSpine_RestPose_ReproducesTheRestRootHeight()
    {
        // Spine1_05's rest head is 2.199 m on elephant_skeleton at 1.0x, and the root was measured at 3.2 m there.
        float scale = 1.3f;
        Assert.AreEqual(3.2f * scale, HowdahSeatMotion.RootAboveFeetFromSpine(2.199f * scale, scale), 1e-4f);
    }

    [TestMethod]
    public void RootAboveFeetFromSpine_SpineRidesHigher_TheRootRisesByTheSameAmount()
    {
        // The measured case: in the live standing pose the spine sits about 0.19 m above its scaled rest height, and
        // the deck, being skinned to it, rises with it. The platform must follow one for one, not scaled again.
        float scale = 1.3f;
        float rest = HowdahSeatMotion.RootAboveFeetFromSpine(2.199f * scale, scale);
        Assert.AreEqual(rest + 0.19f, HowdahSeatMotion.RootAboveFeetFromSpine(2.199f * scale + 0.19f, scale), 1e-4f);
    }

    [TestMethod]
    public void RootAboveFeetFromSpine_BadInput_ReturnsNaN_SoTheCallerFallsBack()
    {
        // NaN means "use the fixed height", never "put the platform somewhere a bad bone read says".
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(float.NaN, 1.3f)));
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(float.PositiveInfinity, 1.3f)));
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(2.8f, float.NaN)));
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(2.8f, 0f)));
    }

    [TestMethod]
    public void RootAboveFeetFromSpine_ImplausibleSpine_ReturnsNaN()
    {
        // A spine reported at the feet or far above the animal is a bad read (a ragdoll, a recycled slot, a skeleton
        // not yet posed), not a deck to put archers on. Outside one body-length of the rest height: fall back.
        float scale = 1.3f;
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(0f, scale)));
        Assert.IsTrue(float.IsNaN(HowdahSeatMotion.RootAboveFeetFromSpine(2.199f * scale + 5f, scale)));
    }

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
