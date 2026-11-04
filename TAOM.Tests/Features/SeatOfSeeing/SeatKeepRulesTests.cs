using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The pure rules of stage S3 (build-spec section 7): the pick, the let-go and the orbit around a kept target.</summary>
[TestClass]
public class SeatKeepRulesTests
{
    private const float Eps = 0.001f;
    private const float Pi = (float)Math.PI;

    private static SeatInput Input(float right = 0f, float forward = 0f, float lift = 0f, float zoom = 0f, float lookYaw = 0f, float lookPitch = 0f,
        bool fast = false) => new(right, forward, lift, zoom, lookYaw, lookPitch, fast);

    // ---- Letting go ----

    [DataTestMethod]
    [DataRow(1f, 0f, 0f, DisplayName = "right")]
    [DataRow(-0.5f, 0f, 0f, DisplayName = "left")]
    [DataRow(0f, 1f, 0f, DisplayName = "forward")]
    [DataRow(0f, -1f, 0f, DisplayName = "back")]
    [DataRow(0f, 0f, 1f, DisplayName = "rise")]
    [DataRow(0f, 0f, -1f, DisplayName = "sink")]
    public void LetsGo_OnMovementRiseOrSink(float right, float forward, float lift)
    {
        Assert.IsTrue(SeatKeepRule.LetsGo(Input(right, forward, lift)));
    }

    [TestMethod]
    public void LetsGo_NotOnTheMouseTheWheelOrTheFastKey()
    {
        Assert.IsFalse(SeatKeepRule.LetsGo(Input(lookYaw: 40f, lookPitch: -30f)), "the mouse turns the orbit");
        Assert.IsFalse(SeatKeepRule.LetsGo(Input(zoom: 2f)), "the wheel changes the distance");
        Assert.IsFalse(SeatKeepRule.LetsGo(Input(fast: true)), "the fast key alone moves nothing");
        Assert.IsFalse(SeatKeepRule.LetsGo(default));
    }

    // ---- The pick ----

    [TestMethod]
    public void KeepsCompany_WithThePanelOpenAndOneSelectedCompanyWithUnits_IsTrue()
    {
        Assert.IsTrue(SeatKeepRule.KeepsCompany(panelOpen: true, selectedCount: 1, firstUnits: 30));
    }

    [DataTestMethod]
    [DataRow(true, 0, 0, DisplayName = "nothing is selected")]
    [DataRow(true, 2, 30, DisplayName = "two companies are selected")]
    [DataRow(true, 1, 0, DisplayName = "the selected company is empty")]
    [DataRow(false, 1, 30, DisplayName = "the panel is closed")]
    public void KeepsCompany_WhenAnyConditionFails_IsFalse(bool panelOpen, int selectedCount, int firstUnits)
    {
        Assert.IsFalse(SeatKeepRule.KeepsCompany(panelOpen, selectedCount, firstUnits));
    }

    [TestMethod]
    public void AgentCounts_WhenTheTerrainRayHitsNothing_IsTrue()
    {
        Assert.IsTrue(SeatKeepRule.AgentCounts(terrainHit: false, terrainDistance: float.NaN, agentDistance: 80f));
    }

    [TestMethod]
    public void AgentCounts_WhenTheAgentIsNearerThanTheTerrain_Or_AsFar_IsTrue()
    {
        Assert.IsTrue(SeatKeepRule.AgentCounts(true, 50f, 40f), "a soldier in a wood: the trees are behind him");
        Assert.IsTrue(SeatKeepRule.AgentCounts(true, 50f, 50f), "not above the terrain distance");
    }

    [TestMethod]
    public void AgentCounts_WhenTheTerrainIsNearer_IsFalse()
    {
        Assert.IsFalse(SeatKeepRule.AgentCounts(true, 40f, 50f), "a soldier behind a hill does not count");
    }

    [TestMethod]
    public void AgentCounts_WithATerrainDistanceThatIsStillNaN_IsTrue()
    {
        // Scene.RayCastForClosestEntityOrTerrain starts the out distance at NaN (Scene.cs:1320), and a plain comparison against it is false.
        Assert.IsTrue(SeatKeepRule.AgentCounts(true, float.NaN, 50f));
    }

    // ---- The orbit ----

    [TestMethod]
    public void Constants_MatchTheContract()
    {
        Assert.AreEqual(16f, SeatOrbit.StartDistance);
        Assert.AreEqual(4f, SeatOrbit.MinDistance);
        Assert.AreEqual(90f, SeatOrbit.MaxDistance);
        Assert.AreEqual(0.05f, SeatOrbit.MinPitch);
        Assert.AreEqual(SeatCameraMath.MaxPitch, SeatOrbit.MaxPitch, "the pitch ceiling is the flight's");
        Assert.AreEqual(5f, SeatOrbit.EaseRate);
        Assert.AreEqual(SeatCameraMath.ZoomPerNotch, SeatOrbit.ZoomPerNotch, "the wheel has the flight's step");
        Assert.AreEqual(SeatCameraMath.LookPerUnit, SeatOrbit.LookPerUnit, "the mouse has the flight's scale");
        Assert.AreEqual(SeatCameraMath.MinPitch, SeatTrialAim.MinPitch, "the trial's aim stops where the flight does");
    }

    [TestMethod]
    public void Ease_IsOneMinusExpOfMinusFiveDt()
    {
        Assert.AreEqual(1f - (float)Math.Exp(-0.5), SeatOrbit.Ease(0.1f), Eps);
        Assert.AreEqual(0f, SeatOrbit.Ease(0f), Eps);
    }

    [TestMethod]
    public void Begin_PutsThePointOnTheLookLineAtTheStartDistance_SoThePoseIsTheCamerasOwn()
    {
        var camera = new SeatPose(100f, 200f, 120f, 0.7f, 0.6f);

        var pose = SeatOrbit.Pose(SeatOrbit.Begin(camera));

        Assert.AreEqual(camera.X, pose.X, Eps);
        Assert.AreEqual(camera.Y, pose.Y, Eps);
        Assert.AreEqual(camera.Z, pose.Z, Eps);
        Assert.AreEqual(camera.Yaw, pose.Yaw, Eps);
        Assert.AreEqual(camera.Pitch, pose.Pitch, Eps);
    }

    [TestMethod]
    public void Begin_HoldsThePitchAboveItsFloor_SoTheCameraIsNeverBelowTheTarget()
    {
        var state = SeatOrbit.Begin(new SeatPose(0f, 0f, 50f, 0f, -0.3f));

        Assert.AreEqual(SeatOrbit.MinPitch, state.Pitch, Eps);
        Assert.AreEqual(SeatOrbit.StartDistance, state.Distance);
    }

    [TestMethod]
    public void Pose_StandsAboveAndBehindThePointItLooksAt()
    {
        // Yaw 0 looks along +Y and the camera looks down by the pitch: it stands south of the point and above it.
        var pose = SeatOrbit.Pose(new SeatOrbitState(10f, 20f, 5f, 0f, 0.6f, 16f));

        Assert.AreEqual(10f, pose.X, Eps);
        Assert.AreEqual(20f - 16f * (float)Math.Cos(0.6), pose.Y, Eps);
        Assert.AreEqual(5f + 16f * (float)Math.Sin(0.6), pose.Z, Eps);
    }

    [TestMethod]
    public void Pose_APositiveYawTurnsLeft_SoTheCameraStandsEastOfThePoint()
    {
        var pose = SeatOrbit.Pose(new SeatOrbitState(0f, 0f, 0f, Pi / 2f, 0.05f, 10f));

        Assert.IsTrue(pose.X > 9.9f, "looking along -X, the camera is on the +X side: " + pose.X);
        Assert.AreEqual(0f, pose.Y, 0.01f);
    }

    [TestMethod]
    public void Pose_IsAlwaysTheDistanceFromThePoint()
    {
        var state = new SeatOrbitState(3f, -4f, 7f, 1.1f, 0.9f, 23f);
        var pose = SeatOrbit.Pose(state);

        var d = Math.Sqrt(Math.Pow(pose.X - state.PointX, 2) + Math.Pow(pose.Y - state.PointY, 2) + Math.Pow(pose.Z - state.PointZ, 2));
        Assert.AreEqual(23.0, d, 0.01);
    }

    [TestMethod]
    public void Step_TheMouseTurnsTheOrbit_WithTheFlightsSigns()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0f, 0.5f, 16f);

        var next = SeatOrbit.Step(state, Input(lookYaw: 100f, lookPitch: 100f), 0f, 0f, 0f, 0.016f);

        Assert.AreEqual(100f * SeatOrbit.LookPerUnit, next.Yaw, Eps, "a positive look turns left");
        Assert.AreEqual(0.5f + 100f * SeatOrbit.LookPerUnit, next.Pitch, Eps, "a positive look pitches down");
    }

    [TestMethod]
    public void Step_HoldsThePitchBetweenItsFloorAndTheCeiling()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0f, 0.5f, 16f);

        Assert.AreEqual(SeatOrbit.MinPitch, SeatOrbit.Step(state, Input(lookPitch: -10000f), 0f, 0f, 0f, 0.016f).Pitch, Eps);
        Assert.AreEqual(SeatOrbit.MaxPitch, SeatOrbit.Step(state, Input(lookPitch: 10000f), 0f, 0f, 0f, 0.016f).Pitch, Eps);
    }

    [TestMethod]
    public void Step_TheYawWrapsAtPi()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, Pi - 0.001f, 0.5f, 16f);

        var next = SeatOrbit.Step(state, Input(lookYaw: 10f), 0f, 0f, 0f, 0.016f);

        Assert.IsTrue(next.Yaw < 0f && next.Yaw > -Pi, "wrapped to the other side: " + next.Yaw);
    }

    [TestMethod]
    public void Step_TheWheelChangesTheDistance_AwayLowersIt_AndTheRangeHolds()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0f, 0.5f, 16f);

        Assert.AreEqual(16f * 0.88f, SeatOrbit.Step(state, Input(zoom: 1f), 0f, 0f, 0f, 0.016f).Distance, Eps);
        Assert.AreEqual(16f / 0.88f, SeatOrbit.Step(state, Input(zoom: -1f), 0f, 0f, 0f, 0.016f).Distance, Eps);
        Assert.AreEqual(SeatOrbit.MinDistance, SeatOrbit.Step(state, Input(zoom: 100f), 0f, 0f, 0f, 0.016f).Distance, Eps);
        Assert.AreEqual(SeatOrbit.MaxDistance, SeatOrbit.Step(state, Input(zoom: -100f), 0f, 0f, 0f, 0.016f).Distance, Eps);
    }

    [TestMethod]
    public void Step_ThePointEasesTowardTheTarget_ByOneMinusExpOfMinusFiveDt()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0f, 0.5f, 16f);

        var next = SeatOrbit.Step(state, default, 10f, -20f, 4f, 0.1f);

        var share = 1f - (float)Math.Exp(-0.5);
        Assert.AreEqual(10f * share, next.PointX, Eps);
        Assert.AreEqual(-20f * share, next.PointY, Eps);
        Assert.AreEqual(4f * share, next.PointZ, Eps);
    }

    [TestMethod]
    public void Step_ManyFrames_ConvergeOnTheTarget()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0f, 0.5f, 16f);
        for (var i = 0; i < 180; i++)
            state = SeatOrbit.Step(state, default, 10f, -20f, 4f, 1f / 60f);

        Assert.AreEqual(10f, state.PointX, 0.01f);
        Assert.AreEqual(-20f, state.PointY, 0.01f);
        Assert.AreEqual(4f, state.PointZ, 0.01f);
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f, 0f)]
    [DataRow(0f, float.PositiveInfinity, 0f)]
    [DataRow(0f, 0f, float.NegativeInfinity)]
    public void Step_ATargetThatIsNotARealNumber_KeepsTheLastPoint_ButStillTurns(float x, float y, float z)
    {
        var state = new SeatOrbitState(5f, 6f, 7f, 0f, 0.5f, 16f);

        var next = SeatOrbit.Step(state, Input(lookYaw: 100f, zoom: 1f), x, y, z, 0.1f);

        Assert.AreEqual(5f, next.PointX);
        Assert.AreEqual(6f, next.PointY);
        Assert.AreEqual(7f, next.PointZ);
        Assert.AreEqual(100f * SeatOrbit.LookPerUnit, next.Yaw, Eps, "the mouse still turns it");
        Assert.AreEqual(16f * 0.88f, next.Distance, Eps, "the wheel still works");
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-0.1f)]
    public void Step_ABadDt_LeavesTheStateAsItIs(float dt)
    {
        var state = new SeatOrbitState(5f, 6f, 7f, 0.3f, 0.5f, 16f);

        Assert.AreEqual(state, SeatOrbit.Step(state, Input(lookYaw: 100f, zoom: 3f), 1f, 1f, 1f, dt));
    }

    [TestMethod]
    public void Step_AnInputThatIsNotARealNumber_CountsAsZero()
    {
        var state = new SeatOrbitState(0f, 0f, 0f, 0.3f, 0.5f, 16f);

        var next = SeatOrbit.Step(state, Input(lookYaw: float.NaN, lookPitch: float.PositiveInfinity, zoom: float.NaN), 0f, 0f, 0f, 0.1f);

        Assert.AreEqual(0.3f, next.Yaw, Eps);
        Assert.AreEqual(0.5f, next.Pitch, Eps);
        Assert.AreEqual(16f, next.Distance, Eps);
    }

    [TestMethod]
    public void FlightOf_HoldsThePoseAsAFlight_SoLettingGoContinuesFromTheView()
    {
        var flight = SeatCameraMath.FlightOf(new SeatPose(1f, 2f, 130f, 0.4f, 0.3f), 100f);

        Assert.AreEqual(new SeatFlight(1f, 2f, 30f, 0.4f, 0.3f), flight);
        Assert.AreEqual(130f, SeatCameraMath.Pose(flight, 100f).Z, Eps, "flight and pose agree");
    }

    [TestMethod]
    public void FlightOf_HoldsTheHeightAndPitchInTheirRanges()
    {
        var low = SeatCameraMath.FlightOf(new SeatPose(0f, 0f, 100.2f, 0f, -2f), 100f);
        var high = SeatCameraMath.FlightOf(new SeatPose(0f, 0f, 400f, 0f, 3f), 100f);

        Assert.AreEqual(SeatCameraMath.MinHeight, low.Height, Eps);
        Assert.AreEqual(SeatCameraMath.MinPitch, low.Pitch, Eps);
        Assert.AreEqual(SeatCameraMath.MaxHeight, high.Height, Eps);
        Assert.AreEqual(SeatCameraMath.MaxPitch, high.Pitch, Eps);
    }
}
