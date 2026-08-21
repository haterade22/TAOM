using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

// The eye-height maths, split out of EyeHeightAdjustmentHook so the arithmetic can be pinned
// without standing up the hook's collaborators. (An earlier comment here claimed a TaleWorlds
// Monster could not be constructed in a test host. That is false against 1.4.8 — Monster declares no
// constructor and MBObjectBase has a public parameterless one — and EyeHeightAdjustmentHookTests now
// exercises the hook directly.)
//
// This became worth testing when the adjuster stopped being a compile-time const and became an MCM
// slider: a value the player can drag is untrusted input, and it is multiplied into a camera height
// that the engine consumes natively.
[TestClass]
public class EyeHeightAdjustmentTests
{
    private const float HumanStanding = 1.65f;

    [TestMethod]
    public void Resolve_TypicalDwarfAdjuster_LowersTheEyeHeight()
    {
        var applied = EyeHeightAdjustment.Resolve(HumanStanding, -0.2f, out var adjusted);

        Assert.IsTrue(applied);
        Assert.AreEqual(1.45f, adjusted, 0.0001f);
    }

    [TestMethod]
    public void Resolve_ZeroAdjuster_ReturnsTheHumanBaseline()
    {
        Assert.IsTrue(EyeHeightAdjustment.Resolve(HumanStanding, 0f, out var adjusted));
        Assert.AreEqual(HumanStanding, adjusted, 0.0001f);
    }

    [TestMethod]
    public void Resolve_PositiveAdjuster_RaisesTheEyeHeight()
    {
        Assert.IsTrue(EyeHeightAdjustment.Resolve(HumanStanding, 0.3f, out var adjusted));
        Assert.AreEqual(1.95f, adjusted, 0.0001f);
    }

    // --- Clamping: the camera must never leave a sane range -----------------------------------

    [TestMethod]
    public void Resolve_AdjusterDrivesResultBelowFloor_ClampsToFloor()
    {
        Assert.IsTrue(EyeHeightAdjustment.Resolve(1.0f, -5f, out var adjusted));
        Assert.AreEqual(EyeHeightAdjustment.MinEyeHeight, adjusted, 0.0001f);
    }

    [TestMethod]
    public void Resolve_AdjusterDrivesResultAboveCeiling_ClampsToCeiling()
    {
        Assert.IsTrue(EyeHeightAdjustment.Resolve(2.0f, 5f, out var adjusted));
        Assert.AreEqual(EyeHeightAdjustment.MaxEyeHeight, adjusted, 0.0001f);
    }

    // --- Non-finite inputs: positive requirement, so NaN fails the gate ------------------------

    // NaN comparisons are always false, so a gate written as "if (bad) return" would let NaN
    // through. These pin the polarity required by the engine-float rule.
    [TestMethod]
    public void Resolve_NaNAdjuster_DoesNotApply()
    {
        var applied = EyeHeightAdjustment.Resolve(HumanStanding, float.NaN, out var adjusted);

        Assert.IsFalse(applied, "A NaN adjuster must leave vanilla eye height in place.");
        Assert.AreEqual(0f, adjusted, "On false the out value is meaningless and is always 0.");
    }

    [TestMethod]
    public void Resolve_InfiniteAdjuster_DoesNotApply()
    {
        Assert.IsFalse(EyeHeightAdjustment.Resolve(HumanStanding, float.PositiveInfinity, out _));
        Assert.IsFalse(EyeHeightAdjustment.Resolve(HumanStanding, float.NegativeInfinity, out _));
    }

    [TestMethod]
    public void Resolve_NaNBaseline_DoesNotApply()
    {
        var applied = EyeHeightAdjustment.Resolve(float.NaN, -0.2f, out var adjusted);

        Assert.IsFalse(applied, "A corrupt human baseline must not be propagated into the camera.");
        Assert.AreEqual(0f, adjusted);
    }

    [TestMethod]
    public void Resolve_NonPositiveBaseline_DoesNotApply()
    {
        Assert.IsFalse(EyeHeightAdjustment.Resolve(0f, -0.2f, out _));
        Assert.IsFalse(EyeHeightAdjustment.Resolve(-1f, -0.2f, out _));
    }

    // --- Slider bounds ------------------------------------------------------------------------

    [TestMethod]
    public void ClampAdjuster_WithinRange_IsUnchanged()
    {
        Assert.AreEqual(-0.2f, EyeHeightAdjustment.ClampAdjuster(-0.2f), 0.0001f);
    }

    [TestMethod]
    public void ClampAdjuster_OutOfRange_IsClampedToTheSliderBounds()
    {
        Assert.AreEqual(EyeHeightAdjustment.MinAdjuster, EyeHeightAdjustment.ClampAdjuster(-99f), 0.0001f);
        Assert.AreEqual(EyeHeightAdjustment.MaxAdjuster, EyeHeightAdjustment.ClampAdjuster(99f), 0.0001f);
    }

    // A stale json2 settings file can hold anything; the provider must not hand NaN onward.
    [TestMethod]
    public void ClampAdjuster_NonFinite_FallsBackToTheShippedDefault()
    {
        Assert.AreEqual(EyeHeightAdjustment.DefaultAdjuster, EyeHeightAdjustment.ClampAdjuster(float.NaN), 0.0001f);
        Assert.AreEqual(EyeHeightAdjustment.DefaultAdjuster, EyeHeightAdjustment.ClampAdjuster(float.PositiveInfinity), 0.0001f);
    }

    // Behaviour lock: TAOM shipped -0.2f as a compile-time const. Turning it into a slider must not
    // silently retune every existing install, so the default has to stay where it was.
    [TestMethod]
    public void DefaultAdjuster_MatchesThePreviouslyShippedConstant()
    {
        Assert.AreEqual(-0.2f, EyeHeightAdjustment.DefaultAdjuster, 0.0001f);
    }

    // One contract on every failure path, so a caller that forgets to check the bool cannot pick up
    // a plausible-looking baseline on one path and 0 on another.
    [TestMethod]
    public void Resolve_EveryFailurePath_LeavesTheOutValueAtZero()
    {
        EyeHeightAdjustment.Resolve(float.NaN, -0.2f, out var a);
        EyeHeightAdjustment.Resolve(0f, -0.2f, out var b);
        EyeHeightAdjustment.Resolve(HumanStanding, float.NaN, out var c);
        EyeHeightAdjustment.Resolve(HumanStanding, float.PositiveInfinity, out var d);

        Assert.AreEqual(0f, a);
        Assert.AreEqual(0f, b);
        Assert.AreEqual(0f, c);
        Assert.AreEqual(0f, d);
    }
}
