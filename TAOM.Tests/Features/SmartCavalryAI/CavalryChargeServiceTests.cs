using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.SmartCavalryAI;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Tests.Features.SmartCavalryAI;

[TestClass]
public class CavalryChargeServiceTests
{
    private ISmartCavalryAISettingsProvider _settings = null!;
    private ICavalryPathPlanner _pathPlanner = null!;
    private CavalryChargeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISmartCavalryAISettingsProvider>();
        _pathPlanner = Substitute.For<ICavalryPathPlanner>();
        _settings.IsEnabled.Returns(true);
        _settings.AvoidFriendlies.Returns(true);
        _settings.ChargeFormationStrictness.Returns(0.7f);
        _settings.ReformDistanceAfterCharge.Returns(25f);
        _settings.ChargeLineSpacing.Returns(1.2f);
        _settings.IsDebugMode.Returns(false);
        _sut = new CavalryChargeService(_settings, _pathPlanner);
    }

    private static IFormationAdapter MakeCav(
        Vec2 currentPosition,
        Vec2 direction,
        bool isAligned = false,
        object? formationKey = null)
    {
        var f = Substitute.For<IFormationAdapter>();
        f.FormationKey.Returns(formationKey ?? new object());
        f.CurrentPosition.Returns(currentPosition);
        f.Direction.Returns(direction);
        f.RepresentativeIsCavalry.Returns(true);
        f.IsAligned(Arg.Any<float>()).Returns(isAligned);
        return f;
    }

    private static ICavalryCommandAdapter MakeCommands(
        Vec2 currentPosition,
        Vec2 direction,
        bool targetAlive = true)
    {
        var c = Substitute.For<ICavalryCommandAdapter>();
        c.CurrentPosition.Returns(currentPosition);
        c.Direction.Returns(direction);
        c.IsTargetAlive(Arg.Any<object>()).Returns(targetAlive);
        return c;
    }

    private static IBattlefieldQueryAdapter MakeBattlefield(
        IReadOnlyList<IFormationAdapter>? friendlies = null,
        bool isFieldBattle = true)
    {
        var b = Substitute.For<IBattlefieldQueryAdapter>();
        b.HasPlayerTeam.Returns(true);
        b.IsFieldBattle.Returns(isFieldBattle);
        b.GetFriendlyFormationsExcluding(Arg.Any<object>())
            .Returns(friendlies ?? new List<IFormationAdapter>());
        b.GetGroundHeightAtPosition(Arg.Any<Vec3>()).Returns(0f);
        return b;
    }

    private void StubPlannerReroute(Vec2 waypoint)
    {
        _pathPlanner
            .TryGetReroutePoint(
                Arg.Any<Vec2>(),
                Arg.Any<Vec2>(),
                Arg.Any<IReadOnlyList<IFormationAdapter>>(),
                out Arg.Any<Vec2>())
            .Returns(call =>
            {
                call[3] = waypoint;
                return true;
            });
    }

    private void StubPlannerNoReroute()
    {
        _pathPlanner
            .TryGetReroutePoint(
                Arg.Any<Vec2>(),
                Arg.Any<Vec2>(),
                Arg.Any<IReadOnlyList<IFormationAdapter>>(),
                out Arg.Any<Vec2>())
            .Returns(call =>
            {
                call[3] = Vec2.Zero;
                return false;
            });
    }

    // ============ HandleChargeOrder ============

    [TestMethod]
    public void HandleChargeOrder_FeatureDisabled_DoesNothing()
    {
        _settings.IsEnabled.Returns(false);
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueStop();
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    [TestMethod]
    public void HandleChargeOrder_NoBlockers_TransitionsToForming()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_NoBlockers_AppliesChargeLineAndStops()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.Received(1).IssueStop();
    }

    [TestMethod]
    public void HandleChargeOrder_BlockersDetected_TransitionsToRerouting()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_BlockersDetected_IssuesMoveToWaypoint()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        commands.Received(1).IssueMoveTo(new Vec2(50f, -10f), Arg.Any<float>());
    }

    [TestMethod]
    public void HandleChargeOrder_AvoidFriendliesOff_SkipsPathPlanner_GoesToForming()
    {
        _settings.AvoidFriendlies.Returns(false);
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        _pathPlanner.DidNotReceive()
            .TryGetReroutePoint(
                Arg.Any<Vec2>(), Arg.Any<Vec2>(),
                Arg.Any<IReadOnlyList<IFormationAdapter>>(),
                out Arg.Any<Vec2>());
    }

    [TestMethod]
    public void HandleChargeOrder_StoresOriginalTargetToken_ForRerouting()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        // After waypoint reach, IssueChargeToTarget should be called with the SAME token.
        var cavAtWaypoint = MakeCav(new Vec2(50f, -10f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commands2 = MakeCommands(new Vec2(50f, -10f), new Vec2(1f, 0f));  // arrived at waypoint
        _sut.Tick(cavAtWaypoint, commands2, battlefield, dt: 0.1f, currentMissionTime: 1f);
        commands2.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void HandleChargeOrder_PassesChargeLineSpacingToCommands()
    {
        _settings.ChargeLineSpacing.Returns(2.4f);
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);

        commands.Received(1).ApplyChargeLine(
            Arg.Any<Vec3>(), Arg.Any<Vec2>(),
            Arg.Is<int>(s => s == 2));  // round(2.4f) == 2
    }

    // ============ Tick: Forming → Charging ============

    [TestMethod]
    public void Tick_FormingAndAligned_TransitionsToCharging()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, dt: 0.1f, currentMissionTime: 1f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void Tick_FormingAndNotAligned_StaysForming()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: false);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void Tick_FormingUsesChargeFormationStrictness()
    {
        _settings.ChargeFormationStrictness.Returns(0.9f);
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        cav.IsAligned(0.9f).Returns(true);
        cav.IsAligned(Arg.Is<float>(f => f != 0.9f)).Returns(false);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        cav.Received().IsAligned(0.9f);
    }

    // ============ Tick: Charging → PassingThrough ============

    [TestMethod]
    public void Tick_ChargingAndDistanceLessThan10_TransitionsToPassingThrough()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);  // → Charging

        // Move cav to within 10m of target
        var cav2 = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), isAligned: true, formationKey: cav.FormationKey);
        var commands2 = MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f));
        _sut.Tick(cav2, commands2, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void Tick_ChargingAndDistanceGreaterThan10_StaysCharging()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);  // → Charging

        // Cav at midpoint, distance ~50
        var cav2 = MakeCav(new Vec2(50f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commands2 = MakeCommands(new Vec2(50f, 0f), new Vec2(1f, 0f));
        _sut.Tick(cav2, commands2, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
    }

    // ============ Tick: PassingThrough → Reforming ============

    [TestMethod]
    public void Tick_PassingThroughAndDistanceGreaterThanReform_TransitionsToReforming()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        // HandleChargeOrder → Forming, then Tick → Charging, then Tick at <10m → PassingThrough
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);  // → Charging
        var cav2 = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cav2, MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 2f);  // → PassingThrough

        // Move to 30m past target. ReformDistanceAfterCharge = 25.
        var cav3 = MakeCav(new Vec2(130f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commands3 = MakeCommands(new Vec2(130f, 0f), new Vec2(1f, 0f));
        _sut.Tick(cav3, commands3, battlefield, 0.1f, 3f);

        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
        commands3.Received(1).IssueStop();
    }

    [TestMethod]
    public void Tick_PassingThroughUsesReformDistanceAfterChargeSetting()
    {
        _settings.ReformDistanceAfterCharge.Returns(50f);
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);
        var cav2 = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cav2, MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 2f);

        // 30m past target — under custom 50m threshold; should NOT reform yet
        var cav3 = MakeCav(new Vec2(130f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cav3, MakeCommands(new Vec2(130f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 3f);
        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));

        // 60m past — over threshold; should reform
        var cav4 = MakeCav(new Vec2(160f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cav4, MakeCommands(new Vec2(160f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 4f);
        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
    }

    // ============ Tick: Reforming → Idle (THE BUG-FIX REGRESSION TEST) ============

    [TestMethod]
    public void Tick_ReformingAndAligned_TransitionsToIdle()
    {
        // Set strictness to 0.9 — original (decompiled) code used hardcoded 0.5 here.
        // Verifies our fix: Reforming honors ChargeFormationStrictness.
        _settings.ChargeFormationStrictness.Returns(0.9f);
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        cav.IsAligned(0.9f).Returns(true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        // Drive to Reforming
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);  // → Charging
        var cavClose = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavClose.IsAligned(Arg.Any<float>()).Returns(true);
        _sut.Tick(cavClose, MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 2f);  // → PassingThrough
        var cavFar = MakeCav(new Vec2(130f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavFar.IsAligned(Arg.Any<float>()).Returns(true);
        _sut.Tick(cavFar, MakeCommands(new Vec2(130f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 3f);  // → Reforming

        var cavReform = MakeCav(new Vec2(140f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavReform.IsAligned(0.9f).Returns(true);
        _sut.Tick(cavReform, MakeCommands(new Vec2(140f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        cavReform.Received().IsAligned(0.9f);  // proves we used the setting, not 0.5f
    }

    [TestMethod]
    public void Tick_ReformingAndNotAligned_StaysReforming()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        cav.IsAligned(Arg.Any<float>()).Returns(true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        // Drive to Reforming
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);
        var cavClose = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavClose.IsAligned(Arg.Any<float>()).Returns(true);
        _sut.Tick(cavClose, MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 2f);
        var cavFar = MakeCav(new Vec2(130f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavFar.IsAligned(Arg.Any<float>()).Returns(true);
        _sut.Tick(cavFar, MakeCommands(new Vec2(130f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 3f);
        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));

        // Now NOT aligned during reform
        var cavReform = MakeCav(new Vec2(140f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        cavReform.IsAligned(Arg.Any<float>()).Returns(false);
        _sut.Tick(cavReform, MakeCommands(new Vec2(140f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
    }

    // ============ Tick: Rerouting ============

    [TestMethod]
    public void Tick_ReroutingAndWaypointReached_AndTargetAlive_TransitionsToIdleAndReissuesCharge()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        // Move to within 10m of waypoint
        var cavAtWaypoint = MakeCav(new Vec2(50f, -10f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commandsAtWaypoint = MakeCommands(new Vec2(50f, -10f), new Vec2(1f, 0f), targetAlive: true);
        _sut.Tick(cavAtWaypoint, commandsAtWaypoint, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commandsAtWaypoint.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void Tick_ReroutingAndWaypointReached_TargetDead_TransitionsToIdleNoCharge()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);

        var cavAtWaypoint = MakeCav(new Vec2(50f, -10f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commandsAtWaypoint = MakeCommands(new Vec2(50f, -10f), new Vec2(1f, 0f), targetAlive: false);
        _sut.Tick(cavAtWaypoint, commandsAtWaypoint, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commandsAtWaypoint.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_ReroutingWaypointNotReached_StaysRerouting()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);

        var cavMidway = MakeCav(new Vec2(20f, -3f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        var commandsMidway = MakeCommands(new Vec2(20f, -3f), new Vec2(1f, 0f));
        _sut.Tick(cavMidway, commandsMidway, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
    }

    // ============ Idle Tick ============

    [TestMethod]
    public void Tick_IdleState_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueStop();
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    // ============ OnMissionEnd ============

    [TestMethod]
    public void OnMissionEnd_ClearsAllPerFormationState()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        _sut.OnMissionEnd();

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void GetState_UnknownFormationKey_ReturnsIdle()
    {
        var unknownKey = new object();
        Assert.AreEqual(CavalryState.Idle, _sut.GetState(unknownKey));
    }

    // ============ Settings consumption assertions (dead-promise gate) ============

    [TestMethod]
    public void HandleChargeOrder_ReadsIsEnabled()
    {
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        _sut.HandleChargeOrder(cav, MakeCommands(Vec2.Zero, new Vec2(1f, 0f)),
            MakeBattlefield(), new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _ = _settings.Received().IsEnabled;
    }

    [TestMethod]
    public void HandleChargeOrder_ReadsAvoidFriendlies()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        _sut.HandleChargeOrder(cav, MakeCommands(Vec2.Zero, new Vec2(1f, 0f)),
            MakeBattlefield(), new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _ = _settings.Received().AvoidFriendlies;
    }

    [TestMethod]
    public void Tick_FormingState_ReadsChargeFormationStrictness()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _settings.ClearReceivedCalls();
        _sut.Tick(cav, commands, MakeBattlefield(), 0.1f, 1f);
        _ = _settings.Received().ChargeFormationStrictness;
    }

    [TestMethod]
    public void Tick_PassingThroughState_ReadsReformDistanceAfterCharge()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);  // → Charging
        var cavClose = MakeCav(new Vec2(95f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cavClose, MakeCommands(new Vec2(95f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 2f);  // → PassingThrough

        _settings.ClearReceivedCalls();
        var cavMidway = MakeCav(new Vec2(110f, 0f), new Vec2(1f, 0f), formationKey: cav.FormationKey);
        _sut.Tick(cavMidway, MakeCommands(new Vec2(110f, 0f), new Vec2(1f, 0f)), battlefield, 0.1f, 3f);
        _ = _settings.Received().ReformDistanceAfterCharge;
    }

    [TestMethod]
    public void HandleChargeOrder_ReadsChargeLineSpacing_WhenForming()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        _ = _settings.Received().ChargeLineSpacing;
    }

    // ============ Recursion guard ============

    [TestMethod]
    public void RecursionGuard_DefaultIsNotSuppressed()
    {
        SmartCavalryRecursionGuard.Reset();
        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed);
    }

    [TestMethod]
    public void RecursionGuard_DuringEnterScope_IsSuppressed()
    {
        SmartCavalryRecursionGuard.Reset();
        using (SmartCavalryRecursionGuard.Enter())
        {
            Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed);
        }
        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed);
    }

    [TestMethod]
    public void RecursionGuard_NestedScopes_StaySuppressedUntilOuterDispose()
    {
        SmartCavalryRecursionGuard.Reset();
        using (SmartCavalryRecursionGuard.Enter())
        {
            using (SmartCavalryRecursionGuard.Enter())
            {
                Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed);
            }
            // Inner dispose must NOT clear the flag while outer scope is active.
            Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed,
                "Counter must not clear after inner dispose; outer scope still active.");
        }
        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed);
    }

    [TestMethod]
    public void RecursionGuard_Reset_ClearsStuckFlag()
    {
        SmartCavalryRecursionGuard.Enter();  // never disposed — simulates abnormal termination
        Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed);

        SmartCavalryRecursionGuard.Reset();

        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed,
            "Reset() must clear stuck flag from abnormal mission termination.");
    }

    // ============ Idempotency + correctness fixes from /deep-review ============

    [TestMethod]
    public void HandleChargeOrder_AlreadyForming_DoesNotResetState()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();
        var target = new object();

        // First call → Forming + ApplyChargeLine + IssueStop fired exactly once each.
        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        // Second call (player double-tapped) — must short-circuit, no extra commands issued.
        _sut.HandleChargeOrder(cav, commands, battlefield, target, new Vec3(100f, 0f, 0f, -1f), 0.1f);

        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.Received(1).IssueStop();
    }

    [TestMethod]
    public void HandleChargeOrder_ZeroLengthDirection_DoesNotEnterFormingOrIssueCommands()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(new Vec2(50f, 50f), new Vec2(1f, 0f));
        var commands = MakeCommands(new Vec2(50f, 50f), new Vec2(1f, 0f));
        var battlefield = MakeBattlefield();

        // Target at same position as cav → length < 1 → must abort entirely.
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(),
            new Vec3(50.5f, 50f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueStop();
    }

    [TestMethod]
    public void Tick_NullFormationKey_DoesNotThrow()
    {
        var cav = Substitute.For<IFormationAdapter>();
        cav.FormationKey.Returns((object)null!);
        cav.RepresentativeIsCavalry.Returns(true);
        cav.CurrentPosition.Returns(Vec2.Zero);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));

        _sut.Tick(cav, commands, MakeBattlefield(), 0.1f, 1f);
        // No throw is the assertion. Silently no-ops.
    }

    [TestMethod]
    public void Tick_FormingAndTargetDead_TransitionsToIdleWithoutCharging()
    {
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f), targetAlive: false);
        var battlefield = MakeBattlefield();

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(),
            new Vec3(100f, 0f, 0f, -1f), 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    // ============ Settings out-of-range fallback (verified via provider clamp) ============

    [TestMethod]
    public void HandleChargeOrder_NonCavalryFormation_DoesNothing()
    {
        var cav = Substitute.For<IFormationAdapter>();
        cav.FormationKey.Returns(new object());
        cav.RepresentativeIsCavalry.Returns(false);
        cav.CurrentPosition.Returns(Vec2.Zero);
        cav.Direction.Returns(new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(),
            new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_NoPlayerTeam_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = Substitute.For<IBattlefieldQueryAdapter>();
        battlefield.HasPlayerTeam.Returns(false);
        // Explicit so this test isolates the HasPlayerTeam guard only. Without it, NSubstitute's
        // bool default would leave IsFieldBattle == false and the test would pass for the wrong
        // reason if the guard order in HandleChargeOrder is ever changed.
        battlefield.IsFieldBattle.Returns(true);

        _sut.HandleChargeOrder(cav, commands, battlefield, new object(),
            new Vec3(100f, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    // ============ Open-field-only siege guard (crash-fix regression tests) ============

    [TestMethod]
    public void HandleChargeOrder_NotFieldBattle_DoesNothing()
    {
        // Arrange — every charge precondition is satisfied EXCEPT the mission is not an open-field
        // battle (e.g. a siege). SmartCavalryAI must not re-enter native formation code here.
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f));
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        var battlefield = MakeBattlefield(isFieldBattle: false);

        // Act
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(100f, 0f, 0f, -1f), 0f);

        // Assert — no native formation commands issued, state never leaves Idle.
        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueStop();
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_NotFieldBattle_DoesNotDriveStateMachine()
    {
        // Arrange — reach Forming in a field battle (aligned, so a normal Tick would advance to Charging).
        StubPlannerNoReroute();
        var cav = MakeCav(Vec2.Zero, new Vec2(1f, 0f), isAligned: true);
        var commands = MakeCommands(Vec2.Zero, new Vec2(1f, 0f));
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), new Vec3(100f, 0f, 0f, -1f), 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        // Act — tick with a non-field battlefield; the state machine must be frozen.
        _sut.Tick(cav, commands, MakeBattlefield(isFieldBattle: false), 0.1f, 1f);

        // Assert — no advance to Charging, no charge order issued.
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }
}

// Note: SmartCavalryAISettingsProvider has no direct unit tests — it accesses
// TaomSettings.Instance which inherits from MCMv5's AttributeGlobalSettings and triggers
// an MCMv5 assembly load that's unavailable in the test environment. The NaN-guard logic
// (SafeClamp returns the compiled default for NaN/Infinity inputs) is verified by code
// review against the adversarial review finding. Future refactor: inject a settings-source
// delegate (Func<TaomSettings?>) into the provider so the static singleton is mockable.
