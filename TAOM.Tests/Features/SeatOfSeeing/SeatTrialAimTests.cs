using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The field trial's aim (build-spec section 12) and the pass rules that stages S2 and S3 added to <see cref="SeatTrialRules"/>.</summary>
[TestClass]
public class SeatTrialAimTests
{
    private const float Eps = 0.001f;
    private const float Pi = (float)Math.PI;

    // ---- The aim ----

    [TestMethod]
    public void Step_ATargetToTheLeftAhead_TurnsLeft_WhichIsAPositiveYaw()
    {
        var (yaw, pitch, error) = SeatTrialAim.Step(0f, 0f, 0f, 0f, 0f, -10f, 10f, 0f);

        Assert.IsTrue(yaw > 0f, "a positive look turns left: " + yaw);
        Assert.AreEqual(0f, pitch, Eps);
        Assert.AreEqual(Pi / 4f, error, Eps);
    }

    [TestMethod]
    public void Step_ATargetToTheRight_TurnsRight()
    {
        var (yaw, _, _) = SeatTrialAim.Step(0f, 0f, 0f, 0f, 0f, 10f, 10f, 0f);

        Assert.IsTrue(yaw < 0f);
    }

    [TestMethod]
    public void Step_ATargetBelow_PitchesDown_WhichIsAPositivePitch()
    {
        var (_, pitch, error) = SeatTrialAim.Step(0f, 0f, 10f, 0f, 0f, 0f, 10f, 0f);

        Assert.IsTrue(pitch > 0f, "a positive look pitches down: " + pitch);
        Assert.AreEqual(Pi / 4f, error, Eps);
    }

    [TestMethod]
    public void Step_ATargetFarAbove_AimsNoHigherThanTheFlightsLowestPitch()
    {
        var (_, _, error) = SeatTrialAim.Step(0f, 0f, 0f, 0f, 0f, 0f, 1f, 500f);

        Assert.AreEqual(0.35f, error, Eps, "the flight cannot look higher than its minimum pitch of -0.35");
    }

    [TestMethod]
    public void Step_ATargetStraightBelow_KeepsTheYaw_AndPitchesToTheMaximum()
    {
        var (yaw, pitch, error) = SeatTrialAim.Step(5f, 5f, 40f, 0.8f, 0.6f, 5f, 5f, 0f);

        Assert.AreEqual(0f, yaw, Eps);
        Assert.AreEqual(SeatTrialAim.Gain * (1.45f - 0.6f) / SeatOrbit.LookPerUnit, pitch, 0.5f);
        Assert.AreEqual(1.45f - 0.6f, error, Eps);
    }

    [TestMethod]
    public void Step_TheYawTakesTheShortArcAcrossTheSeam()
    {
        // Looking at yaw +3.0 with the target at yaw -3.0: the short way is 0.283 rad through pi, a positive turn.
        var (yaw, _, error) = SeatTrialAim.Step(0f, 0f, 0f, 3.0f, 0f, 10f * (float)Math.Sin(3.0), 10f * (float)Math.Cos(3.0), 0f);

        Assert.AreEqual(2f * Pi - 6.0f, error, Eps);
        Assert.IsTrue(yaw > 0f, "turning on through pi, not the long way back: " + yaw);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f)]
    [DataRow(0f, float.PositiveInfinity)]
    public void Step_AnInputThatIsNotARealNumber_TurnsNothing_AndIsNeverOnTarget(float x, float yaw)
    {
        var (lookYaw, lookPitch, error) = SeatTrialAim.Step(x, 0f, 0f, yaw, 0f, 1f, 1f, 0f);

        Assert.AreEqual(0f, lookYaw);
        Assert.AreEqual(0f, lookPitch);
        Assert.IsTrue(float.IsNaN(error));
        Assert.IsFalse(SeatTrialAim.OnTarget(error));
    }

    [TestMethod]
    public void OnTarget_IsWithinAHundredthOfARadian()
    {
        Assert.IsTrue(SeatTrialAim.OnTarget(0.01f));
        Assert.IsFalse(SeatTrialAim.OnTarget(0.0101f));
        Assert.IsFalse(SeatTrialAim.OnTarget(float.NaN));
    }

    [TestMethod]
    public void Aim_FedToTheFlightsOwnStep_ReachesTheTarget_WithNoSwing()
    {
        // The trial feeds the look input to SeatCameraMath.Step each frame: the signs and the scale must turn the real camera onto the point.
        var flight = new SeatFlight(0f, 0f, 20f, 2.8f, 0.2f);
        var target = (X: -30f, Y: -25f, Z: 1.2f);
        var previous = float.MaxValue;
        for (var i = 0; i < 40; i++)
        {
            var pose = SeatCameraMath.Pose(flight, 0f);
            var (lookYaw, lookPitch, error) = SeatTrialAim.Step(pose.X, pose.Y, pose.Z, pose.Yaw, pose.Pitch, target.X, target.Y, target.Z);
            Assert.IsTrue(error <= previous + 1e-4f, "the error never grows: step " + i + " " + error + " after " + previous);
            previous = error;
            if (SeatTrialAim.OnTarget(error))
                return;
            flight = SeatCameraMath.Step(flight, new SeatInput(0f, 0f, 0f, 0f, lookYaw, lookPitch, false), 1f / 60f);
        }

        Assert.Fail("the camera never reached the target, last error " + previous);
    }

    // ---- The pass rules of stages S2 and S3 ----

    [TestMethod]
    public void KeptNearTarget_AllowsTheOrbitDistanceAndATwoMetreLag()
    {
        Assert.IsTrue(SeatTrialRules.KeptNearTarget(16f, 16f));
        Assert.IsTrue(SeatTrialRules.KeptNearTarget(18f, 16f));
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(18.1f, 16f));
        Assert.IsTrue(SeatTrialRules.KeptNearTarget(14f, 16f), "the lower bound is the same tolerance");
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(13.9f, 16f), "just outside it, nearer");
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(3f, 16f), "a camera collapsed onto the target is not near it");
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(0f, 16f));
    }

    [TestMethod]
    public void KeptNearTarget_ADistanceThatIsNotARealNumber_ProvesNothing()
    {
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(float.NaN, 16f));
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(float.PositiveInfinity, 16f));
        Assert.IsFalse(SeatTrialRules.KeptNearTarget(16f, float.NaN));
    }

    [TestMethod]
    public void CursorPlaced_IsWithinTwoPixelsOnEachAxis()
    {
        Assert.IsTrue(SeatTrialRules.CursorPlaced(576f, 432f, 577f, 431f));
        Assert.IsTrue(SeatTrialRules.CursorPlaced(576f, 432f, 578f, 434f));
        Assert.IsFalse(SeatTrialRules.CursorPlaced(576f, 432f, 579f, 432f));
        Assert.IsFalse(SeatTrialRules.CursorPlaced(576f, 432f, 576f, 435f));
        Assert.IsFalse(SeatTrialRules.CursorPlaced(float.NaN, 432f, 576f, 432f), "a wanted pixel nobody set");
        Assert.IsFalse(SeatTrialRules.CursorPlaced(576f, 432f, 0f, 0f), "the cursor did not move");
    }

    [TestMethod]
    public void FocusFollowed_IsTheHandlersPointWithinTwoPixelsOfThePointerFunction()
    {
        SeatFocusReading Reading(float fieldX, float fieldY) => new(576f, 432f, 576f, 432f, 576f, 432f, fieldX, fieldY, "none");

        Assert.IsTrue(SeatTrialRules.FocusFollowed(Reading(576f, 432f)));
        Assert.IsTrue(SeatTrialRules.FocusFollowed(Reading(577.5f, 430.5f)));
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(960f, 540f)), "the handler kept the screen centre");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(576f, 540f)), "right across, wrong down");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(960f, 432f)), "right down, wrong across");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(576f, float.NaN)), "no field was read down");
        Assert.IsTrue(SeatTrialRules.FocusFollowed(Reading(576f, 433.9f)), "just inside the tolerance down");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(576f, 434.1f)), "just outside the tolerance down");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(Reading(float.NaN, 432f)), "no field was read");
        Assert.IsFalse(SeatTrialRules.FocusFollowed(new SeatFocusReading(0f, 0f, 0f, 0f, float.NaN, float.NaN, 0f, 0f, "none")));
    }

    [TestMethod]
    public void ReopenedPromptly_IsWithinThreeFramesOfTheStepsOwnWaits()
    {
        Assert.AreEqual(3, SeatTrialRules.ReopenFrameLimit);
        Assert.IsTrue(SeatTrialRules.ReopenedPromptly(0));
        Assert.IsTrue(SeatTrialRules.ReopenedPromptly(1));
        Assert.IsTrue(SeatTrialRules.ReopenedPromptly(3));
        Assert.IsFalse(SeatTrialRules.ReopenedPromptly(4));
        Assert.IsFalse(SeatTrialRules.ReopenedPromptly(-1));
    }

    [TestMethod]
    public void ForwardDot_IsTheCosineBetweenTheDisplacementAndTheViewsForward()
    {
        Assert.AreEqual(1f, SeatTrialRules.ForwardDot(0f, 60f, 0f, 1f), 0.0001f);
        Assert.AreEqual(-1f, SeatTrialRules.ForwardDot(0f, -60f, 0f, 1f), 0.0001f);
        Assert.AreEqual(0f, SeatTrialRules.ForwardDot(60f, 0f, 0f, 1f), 0.0001f);
        Assert.AreEqual(0.7071f, SeatTrialRules.ForwardDot(30f, 30f, 0f, 1f), 0.001f);
        Assert.AreEqual(1f, SeatTrialRules.ForwardDot(0f, 5f, 0f, 3f), 0.0001f, "the lengths do not matter");
    }

    [DataTestMethod]
    [DataRow(0f, 0f, 0f, 1f, DisplayName = "no displacement")]
    [DataRow(0f, 5f, 0f, 0f, DisplayName = "no forward")]
    [DataRow(float.NaN, 5f, 0f, 1f, DisplayName = "dx not a number")]
    [DataRow(0f, 5f, 0f, float.PositiveInfinity, DisplayName = "forward not a number")]
    public void ForwardDot_WithNothingToCompare_IsNotANumber(float dx, float dy, float fx, float fy)
    {
        Assert.IsTrue(float.IsNaN(SeatTrialRules.ForwardDot(dx, dy, fx, fy)));
    }

    [TestMethod]
    public void FlewForward_NeedsADotOfNinetyHundredthsOrMore_OnEachSideOfTheLimit()
    {
        Assert.IsTrue(SeatTrialRules.FlewForward(1f));
        Assert.IsTrue(SeatTrialRules.FlewForward(0.9f));
        Assert.IsFalse(SeatTrialRules.FlewForward(0.899f), "just under the limit");
        Assert.IsFalse(SeatTrialRules.FlewForward(0f), "sideways");
        Assert.IsFalse(SeatTrialRules.FlewForward(-1f), "backward");
        Assert.IsFalse(SeatTrialRules.FlewForward(float.NaN), "a direction nobody measured proves nothing");
    }

    [TestMethod]
    public void SmallestKillable_IsTheSmallestCompanyWithUnitsAndNoHero()
    {
        var companies = new (int Units, bool HoldsHero)[] { (40, false), (5, true), (12, false), (0, false), (12, false) };

        Assert.AreEqual(2, SeatTrialRules.SmallestKillable(companies), "the hero's company of 5 and the empty one are passed over; the first of equals wins");
    }

    [TestMethod]
    public void SmallestKillable_WithNoCandidate_IsMinusOne()
    {
        Assert.AreEqual(-1, SeatTrialRules.SmallestKillable(new (int Units, bool HoldsHero)[] { (5, true), (0, false) }));
        Assert.AreEqual(-1, SeatTrialRules.SmallestKillable(Array.Empty<(int Units, bool HoldsHero)>()));
    }

    [TestMethod]
    public void Largest_IsTheCompanyWithTheMostUnits_HeroOrNot()
    {
        var companies = new (int Units, bool HoldsHero)[] { (12, false), (40, true), (40, false), (0, false) };

        Assert.AreEqual(1, SeatTrialRules.Largest(companies), "the first of equals");
        Assert.AreEqual(-1, SeatTrialRules.Largest(new (int Units, bool HoldsHero)[] { (0, false) }));
    }
}
