using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SmartCavalryAI;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Tests.Features.SmartCavalryAI;

/// <summary>
/// State machine v2 (#586). Geometry every test shares: the cavalry starts at the origin facing
/// east, the target formation sits 100 m east. Forming is entered at t=0, the aligned tick at
/// t=1 launches the charge, contact happens once the centroid is inside 10 m of the target
/// measured along the charge direction, and the reform point is ReformDistance past the target
/// plane (25 m by default, so x=125 for a head-on charge).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CavalryChargeServiceTests
{
    private static readonly Vec2 East = new(1f, 0f);
    private static readonly Vec2 Target = new(100f, 0f);
    private static readonly Vec3 Target3 = new(100f, 0f, 0f, -1f);

    private ISmartCavalryAISettingsProvider _settings = null!;
    private ICavalryPathPlanner _pathPlanner = null!;
    private IModLogger _logger = null!;
    private CavalryChargeService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISmartCavalryAISettingsProvider>();
        _pathPlanner = Substitute.For<ICavalryPathPlanner>();
        _logger = Substitute.For<IModLogger>();
        _settings.IsEnabled.Returns(true);
        _settings.AvoidFriendlies.Returns(true);
        _settings.ChargeFormationStrictness.Returns(0.7f);
        _settings.ReformDistanceAfterCharge.Returns(25f);
        _settings.ChargeLineSpacing.Returns(1.2f);
        _settings.MaxLineUpSeconds.Returns(4f);
        _settings.IsDebugMode.Returns(false);
        StubPlannerNoReroute();
        _sut = new CavalryChargeService(_settings, _pathPlanner, _logger);
    }

    // ============ helpers ============

    private static IFormationAdapter MakeCav(
        Vec2 position,
        bool isAligned = false,
        object? formationKey = null,
        bool isAIControlled = false)
    {
        var f = Substitute.For<IFormationAdapter>();
        f.FormationKey.Returns(formationKey ?? new object());
        f.CurrentPosition.Returns(position);
        f.Direction.Returns(East);
        f.RepresentativeIsCavalry.Returns(true);
        f.IsAIControlled.Returns(isAIControlled);
        f.IsAligned(Arg.Any<float>()).Returns(isAligned);
        return f;
    }

    /// <summary>Same formation, moved. Keeps the key so the service finds its state.</summary>
    private static IFormationAdapter Relocate(IFormationAdapter cav, Vec2 position, bool isAligned = false)
        => MakeCav(position, isAligned, cav.FormationKey);

    private static ICavalryCommandAdapter MakeCommands(
        bool targetAlive = true,
        Vec2? liveTargetPosition = null,
        bool moveSucceeds = true)
    {
        var c = Substitute.For<ICavalryCommandAdapter>();
        c.CurrentPosition.Returns(Vec2.Zero);
        c.Direction.Returns(East);
        c.IsTargetAlive(Arg.Any<object>()).Returns(targetAlive);
        c.IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>()).Returns(moveSucceeds);
        c.GetTargetDepthAlong(Arg.Any<object>(), Arg.Any<Vec2>()).Returns(0f);
        var live = liveTargetPosition ?? Target;
        c.TryGetTargetPosition(Arg.Any<object>(), out Arg.Any<Vec2>())
            .Returns(call =>
            {
                call[1] = live;
                return targetAlive;
            });
        return c;
    }

    private static IBattlefieldQueryAdapter MakeBattlefield(
        IReadOnlyList<IFormationAdapter>? friendlies = null,
        bool isFieldBattle = true,
        object? nearestEnemy = null,
        Vec2 nearestEnemyPosition = default)
    {
        var b = Substitute.For<IBattlefieldQueryAdapter>();
        b.HasPlayerTeam.Returns(true);
        b.IsFieldBattle.Returns(isFieldBattle);
        b.GetFriendlyFormationsExcluding(Arg.Any<object>())
            .Returns(friendlies ?? new List<IFormationAdapter>());
        b.GetGroundHeightAtPosition(Arg.Any<Vec3>()).Returns(0f);
        b.TryGetNearestEnemyFormation(Arg.Any<object>(), out Arg.Any<object?>(), out Arg.Any<Vec2>())
            .Returns(call =>
            {
                call[1] = nearestEnemy;
                call[2] = nearestEnemyPosition;
                return nearestEnemy != null;
            });
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

    private static Vec2 Near(Vec2 expected) => Arg.Is<Vec2>(v => (v - expected).Length < 0.01f);

    private static Vec3 NearXY(float x, float y) =>
        Arg.Is<Vec3>(p => System.Math.Abs(p.x - x) < 0.01f && System.Math.Abs(p.y - y) < 0.01f);

    /// <summary>Forming at t=0, aligned tick at t=1: the formation is Charging at its target.</summary>
    private (IFormationAdapter cav, object target) DriveToCharging(
        ICavalryCommandAdapter commands, IBattlefieldQueryAdapter battlefield)
    {
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, Target3, 0f);
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);
        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        return (cav, target);
    }

    /// <summary>Charging, then a contact tick at x=92 (t=2): PassingThrough toward x=125.</summary>
    private (IFormationAdapter cav, object target) DriveToPassingThrough(
        ICavalryCommandAdapter commands, IBattlefieldQueryAdapter battlefield)
    {
        var (cav, target) = DriveToCharging(commands, battlefield);
        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);
        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        return (cav, target);
    }

    /// <summary>PassingThrough, then an arrival tick at x=124 (t=3): Reforming.</summary>
    private (IFormationAdapter cav, object target) DriveToReforming(
        ICavalryCommandAdapter commands, IBattlefieldQueryAdapter battlefield)
    {
        var (cav, target) = DriveToPassingThrough(commands, battlefield);
        _sut.Tick(Relocate(cav, new Vec2(124f, 0f)), commands, battlefield, 0.1f, 3f);
        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
        return (cav, target);
    }

    // ============ HandleChargeOrder: entry ============

    [TestMethod]
    public void HandleChargeOrder_FeatureDisabled_DoesNothing()
    {
        _settings.IsEnabled.Returns(false);
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueCharge();
    }

    [TestMethod]
    public void HandleChargeOrder_NoBlockers_TransitionsToForming()
    {
        var cav = MakeCav(Vec2.Zero);

        _sut.HandleChargeOrder(cav, MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_NoBlockers_AppliesChargeLineAndMovesToIt_NeverStops()
    {
        // Stop is StandGround: riders hold their OWN positions and ignore the line. Only a Move
        // (Hold state) puts them in their slots, so the line-up must be a Move 5 m ahead.
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        commands.Received(1).ApplyChargeLine(NearXY(5f, 0f), Near(East), Arg.Any<int>());
        commands.Received(1).IssueMoveTo(Near(new Vec2(5f, 0f)), Arg.Any<float>());
        commands.DidNotReceive().IssueStop();
    }

    [TestMethod]
    public void HandleChargeOrder_LineMoveRefused_HandsBackToVanillaCharge()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands(moveSucceeds: false);

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void HandleChargeOrder_BlockersDetected_TransitionsToRerouting()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);

        _sut.HandleChargeOrder(cav, MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_BlockersDetected_IssuesMoveToWaypoint()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        commands.Received(1).IssueMoveTo(Near(new Vec2(50f, -10f)), Arg.Any<float>());
    }

    [TestMethod]
    public void HandleChargeOrder_AvoidFriendliesOff_SkipsPathPlanner_GoesToForming()
    {
        _settings.AvoidFriendlies.Returns(false);
        var cav = MakeCav(Vec2.Zero);

        _sut.HandleChargeOrder(cav, MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        _pathPlanner.DidNotReceive()
            .TryGetReroutePoint(
                Arg.Any<Vec2>(), Arg.Any<Vec2>(),
                Arg.Any<IReadOnlyList<IFormationAdapter>>(),
                out Arg.Any<Vec2>());
    }

    [TestMethod]
    public void HandleChargeOrder_PassesChargeLineSpacingToCommands()
    {
        _settings.ChargeLineSpacing.Returns(2.4f);
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        commands.Received(1).ApplyChargeLine(
            Arg.Any<Vec3>(), Arg.Any<Vec2>(),
            Arg.Is<int>(s => s == 2));  // round(2.4f) == 2
    }

    [TestMethod]
    public void HandleChargeOrder_ZeroLengthDirection_DoesNotEnterFormingOrIssueCommands()
    {
        var cav = MakeCav(new Vec2(50f, 50f));
        var commands = MakeCommands();

        // Target at the same position as the cavalry: no direction to line up along.
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(),
            new Vec3(50.5f, 50f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
    }

    [TestMethod]
    public void HandleChargeOrder_NonCavalryFormation_DoesNothing()
    {
        var cav = Substitute.For<IFormationAdapter>();
        cav.FormationKey.Returns(new object());
        cav.RepresentativeIsCavalry.Returns(false);
        cav.CurrentPosition.Returns(Vec2.Zero);
        cav.Direction.Returns(East);

        _sut.HandleChargeOrder(cav, MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_AIControlledFormation_DoesNothing()
    {
        // F6 delegation, an enlisted battle, a dead player: the team AI's orders are not ours.
        var cav = MakeCav(Vec2.Zero, isAIControlled: true);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    [TestMethod]
    public void HandleChargeOrder_NoPlayerTeam_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero);
        var battlefield = Substitute.For<IBattlefieldQueryAdapter>();
        battlefield.HasPlayerTeam.Returns(false);
        // Explicit so this test isolates the HasPlayerTeam guard only: NSubstitute's bool default
        // would otherwise leave IsFieldBattle false and pass for the wrong reason.
        battlefield.IsFieldBattle.Returns(true);

        _sut.HandleChargeOrder(cav, MakeCommands(), battlefield, new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void HandleChargeOrder_NotFieldBattle_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(isFieldBattle: false), new object(), Target3, 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
    }

    // ============ HandleChargeOrder: a second F3 means "charge now" ============

    [TestMethod]
    public void HandleChargeOrder_WhileForming_JumpsToChargingWithNewTarget()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var first = new object();
        var second = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, first, Target3, 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        _sut.HandleChargeOrder(cav, commands, battlefield, second, new Vec3(0f, 100f, 0f, -1f), 0.5f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(second);
        commands.DidNotReceive().IssueChargeToTarget(first);
        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    [TestMethod]
    public void HandleChargeOrder_WhileForming_ChargeNowTracksTheNewTargetDirection()
    {
        var cav = MakeCav(Vec2.Zero);
        var battlefield = MakeBattlefield();
        var north = new Vec2(0f, 100f);
        var commands = MakeCommands(liveTargetPosition: north);
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), new Vec3(0f, 100f, 0f, -1f), 0.5f);

        // Contact is measured along the NEW direction (north): x=0, y=92 is 8 m short.
        _sut.Tick(Relocate(cav, new Vec2(0f, 92f)), commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueMoveTo(Near(new Vec2(0f, 125f)), Arg.Any<float>());
    }

    [TestMethod]
    public void HandleChargeOrder_WhileReforming_JumpsToCharging()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToReforming(commands, battlefield);
        var second = new object();

        _sut.HandleChargeOrder(Relocate(cav, new Vec2(124f, 0f)), commands, battlefield, second, Target3, 3.5f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(second);
    }

    [TestMethod]
    public void HandleChargeOrder_WhileRerouting_JumpsToChargingWithNewTarget()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);
        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
        var second = new object();

        _sut.HandleChargeOrder(cav, commands, battlefield, second, Target3, 0.5f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(second);
    }

    // ============ Forming ============

    [TestMethod]
    public void Tick_FormingAndAligned_TransitionsToCharging()
    {
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void Tick_FormingNotAlignedBeforeMaxLineUp_StaysForming()
    {
        var cav = MakeCav(Vec2.Zero, isAligned: false);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 3.9f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_FormingNotAlignedAfterMaxLineUp_ChargesAnyway()
    {
        // The floor: a line that will not form must not hold the formation. 4 s, then go.
        var cav = MakeCav(Vec2.Zero, isAligned: false);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void Tick_FormingUsesChargeFormationStrictness()
    {
        _settings.ChargeFormationStrictness.Returns(0.9f);
        var cav = MakeCav(Vec2.Zero);
        cav.IsAligned(0.9f).Returns(true);
        cav.IsAligned(Arg.Is<float>(f => f != 0.9f)).Returns(false);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        cav.Received().IsAligned(0.9f);
    }

    [TestMethod]
    public void Tick_FormingAndTargetDead_RetargetsNearestEnemyAndRelines()
    {
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var commands = MakeCommands(targetAlive: false);
        var replacement = new object();
        var battlefield = MakeBattlefield(nearestEnemy: replacement, nearestEnemyPosition: new Vec2(0f, 100f));
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        // A new line toward the replacement (north), not a charge at a formation that is gone.
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.Received(1).ApplyChargeLine(NearXY(0f, 5f), Near(new Vec2(0f, 1f)), Arg.Any<int>());
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
        commands.DidNotReceive().IssueCharge();
    }

    [TestMethod]
    public void Tick_FormingAndTargetDeadNoEnemyLeft_HandsBackToVanillaCharge()
    {
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var commands = MakeCommands(targetAlive: false);
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    // ============ Charging ============

    [TestMethod]
    public void Tick_ChargingWithinContactAlongChargeDir_MovesToReformPointFacingEnemy()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        // 8 m short of the target along the charge direction: contact.
        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        // Reform point: 25 m past the target plane, on the far side, facing back west.
        commands.Received(1).IssueMoveTo(Near(new Vec2(125f, 0f)), Arg.Any<float>());
        commands.Received(1).ApplyChargeLine(NearXY(125f, 0f), Near(new Vec2(-1f, 0f)), Arg.Any<int>());
        commands.DidNotReceive().IssueStop();
    }

    [TestMethod]
    public void Tick_ChargingBeyondContact_StaysCharging()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(50f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueMoveTo(Near(new Vec2(125f, 0f)), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_ChargingUsesLiveTargetPosition_NotSnapshot()
    {
        // The order named x=100, but the enemy has advanced to x=60. Contact is against x=60.
        var commands = MakeCommands(liveTargetPosition: new Vec2(60f, 0f));
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(52f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueMoveTo(Near(new Vec2(85f, 0f)), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_ChargingFlankOffset_ContactUsesChargePlaneAndKeepsLateralOffset()
    {
        // 30 m off the enemy centre but 5 m short of its plane: that is contact for a flank
        // charge, and the riders ride straight on rather than converging on the centre.
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(95f, 30f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueMoveTo(Near(new Vec2(125f, 30f)), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_ChargingAndTargetDead_RetargetsAndReissuesChargeToTarget()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, target) = DriveToCharging(commands, battlefield);
        var replacement = new object();
        commands.IsTargetAlive(target).Returns(false);
        commands.IsTargetAlive(replacement).Returns(true);
        var battlefieldWithEnemy = MakeBattlefield(nearestEnemy: replacement, nearestEnemyPosition: new Vec2(150f, 0f));

        _sut.Tick(Relocate(cav, new Vec2(50f, 0f)), commands, battlefieldWithEnemy, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(replacement);
        commands.DidNotReceive().IssueCharge();
    }

    [TestMethod]
    public void Tick_ChargingAndTargetDeadNoEnemyLeft_HandsBackToVanillaCharge()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, target) = DriveToCharging(commands, battlefield);
        commands.IsTargetAlive(target).Returns(false);

        _sut.Tick(Relocate(cav, new Vec2(50f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void Tick_ChargingReformMoveRefused_HandsBackToVanillaCharge()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        commands.IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>()).Returns(false);

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void Tick_ChargingContact_ReadsReformDistanceAfterCharge()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        _settings.ClearReceivedCalls();

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        _ = _settings.Received().ReformDistanceAfterCharge;
    }

    [TestMethod]
    public void Tick_ChargingContact_UsesReformDistanceAfterChargeSetting()
    {
        _settings.ReformDistanceAfterCharge.Returns(50f);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        commands.Received(1).IssueMoveTo(Near(new Vec2(150f, 0f)), Arg.Any<float>());
    }

    // ============ PassingThrough ============

    [TestMethod]
    public void Tick_PassingThroughArrivedAtReformPoint_TransitionsToReforming_NoStop()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToPassingThrough(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(124f, 0f)), commands, battlefield, 0.1f, 3f);

        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueStop();
    }

    [TestMethod]
    public void Tick_PassingThroughNotArrivedBeforeTimeout_StaysPassingThrough()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToPassingThrough(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(105f, 0f)), commands, battlefield, 0.1f, 7f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueCharge();
    }

    [TestMethod]
    public void Tick_PassingThroughTimedOut_HandsBackToVanillaCharge()
    {
        // Bogged down inside the enemy for 10 s: let vanilla have them.
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToPassingThrough(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(105f, 0f)), commands, battlefield, 0.1f, 12f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    // ============ Reforming ============

    [TestMethod]
    public void Tick_ReformingAligned_StartsNextCycleTowardNearestEnemy()
    {
        var commands = MakeCommands();
        var next = new object();
        var battlefield = MakeBattlefield(nearestEnemy: next, nearestEnemyPosition: new Vec2(200f, 0f));
        var (cav, _) = DriveToReforming(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(125f, 0f), isAligned: true), commands, battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.Received(1).ApplyChargeLine(NearXY(130f, 0f), Near(East), Arg.Any<int>());
        commands.Received(1).IssueMoveTo(Near(new Vec2(130f, 0f)), Arg.Any<float>());

        // And the new cycle charges the NEW target once the line forms.
        _sut.Tick(Relocate(cav, new Vec2(130f, 0f), isAligned: true), commands, battlefield, 0.1f, 5f);
        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(next);
    }

    [TestMethod]
    public void Tick_ReformingNotAlignedBeforeTimeout_StaysReforming()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield(nearestEnemy: new object(), nearestEnemyPosition: new Vec2(200f, 0f));
        var (cav, _) = DriveToReforming(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(125f, 0f), isAligned: false), commands, battlefield, 0.1f, 5f);

        Assert.AreEqual(CavalryState.Reforming, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void Tick_ReformingTimedOut_StartsNextCycle()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield(nearestEnemy: new object(), nearestEnemyPosition: new Vec2(200f, 0f));
        var (cav, _) = DriveToReforming(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(125f, 0f), isAligned: false), commands, battlefield, 0.1f, 7f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void Tick_ReformingNoEnemyLeft_HandsBackToVanillaCharge()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToReforming(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(125f, 0f), isAligned: true), commands, battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void Tick_ReformingUsesChargeFormationStrictness()
    {
        // The port's decompile baseline hardcoded 0.5f here; the setting must be what is read.
        _settings.ChargeFormationStrictness.Returns(0.9f);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield(nearestEnemy: new object(), nearestEnemyPosition: new Vec2(200f, 0f));
        var (cav, _) = DriveToReforming(commands, battlefield);
        var reforming = Relocate(cav, new Vec2(125f, 0f));
        reforming.IsAligned(0.9f).Returns(true);
        reforming.IsAligned(Arg.Is<float>(f => f != 0.9f)).Returns(false);

        _sut.Tick(reforming, commands, battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        reforming.Received().IsAligned(0.9f);
    }

    // ============ Rerouting ============

    [TestMethod]
    public void Tick_ReroutingWaypointReached_TargetAlive_InitiatesLineChargeTowardLiveTarget()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, Target3, 0f);

        _sut.Tick(Relocate(cav, new Vec2(50f, -10f)), commands, battlefield, 0.1f, 1f);

        // The reroute leads INTO the line charge (Forming), not straight to a bare ChargeToTarget.
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());

        // Once the line forms, the charge goes at the ORIGINAL target token.
        _sut.Tick(Relocate(cav, new Vec2(55f, -9f), isAligned: true), commands, battlefield, 0.1f, 2f);
        commands.Received(1).IssueChargeToTarget(target);
    }

    [TestMethod]
    public void Tick_ReroutingWaypointReached_TargetDeadNoEnemyLeft_HandsBackToVanillaCharge()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands(targetAlive: false);
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(Relocate(cav, new Vec2(50f, -10f)), commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_ReroutingWaypointReached_TargetDead_RetargetsNearestEnemy()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands(targetAlive: false);
        var battlefield = MakeBattlefield(nearestEnemy: new object(), nearestEnemyPosition: new Vec2(50f, 90f));
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(Relocate(cav, new Vec2(50f, -10f)), commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Near(new Vec2(0f, 1f)), Arg.Any<int>());
    }

    [TestMethod]
    public void Tick_ReroutingWaypointNotReached_StaysRerouting()
    {
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(Relocate(cav, new Vec2(20f, -3f)), commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void Tick_ReroutingTimedOut_InitiatesLineChargeAnyway()
    {
        // A waypoint the engine clamped out of reach must not hold the formation: 12 s, then go.
        StubPlannerReroute(new Vec2(50f, -10f));
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);

        _sut.Tick(Relocate(cav, new Vec2(20f, -3f)), commands, battlefield, 0.1f, 12f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
    }

    // ============ Cancel and AI control ============

    [TestMethod]
    public void CancelCharge_ActiveState_ResetsToIdleWithoutOrders()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        commands.ClearReceivedCalls();

        _sut.CancelCharge(cav.FormationKey);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceiveWithAnyArgs().IssueCharge();
        commands.DidNotReceiveWithAnyArgs().IssueStop();
        commands.DidNotReceiveWithAnyArgs().IssueMoveTo(default, default);
        commands.DidNotReceiveWithAnyArgs().IssueChargeToTarget(default!);
    }

    [TestMethod]
    public void CancelCharge_UnknownKey_DoesNothing()
    {
        _sut.CancelCharge(new object());
        _sut.CancelCharge(null!);
    }

    [TestMethod]
    public void CancelCharge_ThenTick_DoesNotResumeTheMachine()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        _sut.CancelCharge(cav.FormationKey);
        commands.ClearReceivedCalls();

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceiveWithAnyArgs().IssueMoveTo(default, default);
    }

    [TestMethod]
    public void Tick_AIControlledFormationWithState_CancelsWithoutOrders()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        commands.ClearReceivedCalls();
        var delegated = MakeCav(new Vec2(92f, 0f), isAligned: true, formationKey: cav.FormationKey, isAIControlled: true);

        _sut.Tick(delegated, commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceiveWithAnyArgs().IssueMoveTo(default, default);
        commands.DidNotReceiveWithAnyArgs().IssueCharge();
    }

    // ============ Toggle flipped off mid-cycle ============

    [TestMethod]
    public void Tick_FeatureDisabledMidCycle_HandsBackToVanillaCharge()
    {
        // The behavior keeps ticking while HasActiveCycles is true, so a Move this machine issued
        // is never left standing after the player turns the feature off.
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.HandleChargeOrder(cav, commands, battlefield, new object(), Target3, 0f);
        Assert.IsTrue(_sut.HasActiveCycles);
        _settings.IsEnabled.Returns(false);

        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        Assert.IsFalse(_sut.HasActiveCycles);
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void HasActiveCycles_NoStateOrIdleOnly_IsFalse()
    {
        Assert.IsFalse(_sut.HasActiveCycles);
        var commands = MakeCommands();
        var (cav, _) = DriveToCharging(commands, MakeBattlefield());
        _sut.CancelCharge(cav.FormationKey);
        Assert.IsFalse(_sut.HasActiveCycles);
    }

    // ============ Engine re-entry and geometry pins ============

    [TestMethod]
    public void HandleChargeOrder_WhileCharging_RetargetsWithoutRelining()
    {
        // Formation.Tick substitutes a plain Charge when a ChargeToTarget's target empties; the
        // postfix routes it here as a charge order mid-cycle. It must re-target, not re-line.
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        var replacement = new object();

        _sut.HandleChargeOrder(Relocate(cav, new Vec2(40f, 0f)), commands, battlefield, replacement, Target3, 1.5f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(replacement);
        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    [TestMethod]
    public void HandleChargeOrder_NaNTargetPosition_DoesNotEnterFormingOrIssueCommands()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(),
            new Vec3(float.NaN, 0f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
        commands.DidNotReceive().IssueMoveTo(Arg.Any<Vec2>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_ChargingTargetMovedLaterally_ReformPointClearsTheLiveEnemyPlane()
    {
        // The charge direction is frozen at line time. An enemy that slides 40 m north during the
        // charge is chased by vanilla's own ChargeToTarget steering, so the centroid follows it;
        // the reform point then sits 25 m past the plane through the LIVE enemy centre along the
        // frozen axis, at the centroid's own lateral offset. Pinned as the documented trade-off.
        var commands = MakeCommands(liveTargetPosition: new Vec2(100f, 40f));
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(92f, 38f)), commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.PassingThrough, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueMoveTo(Near(new Vec2(125f, 38f)), Arg.Any<float>());
    }

    // ============ The player's targeted charge (Patch31b -> RetargetCycle) ============

    [TestMethod]
    public void RetargetCycle_WhileForming_RedrawsTheLineTowardTheNamedTarget()
    {
        // Vanilla's targeted charge is a plain Charge (handled at the nearest enemy, A) followed by
        // SetTargetFormation(B). The Forming line must be redrawn at B and the charge go to B.
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var commands = MakeCommands(liveTargetPosition: new Vec2(0f, 100f));
        var battlefield = MakeBattlefield();
        var nearest = new object();
        var named = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, nearest, Target3, 0f);

        _sut.RetargetCycle(cav, commands, battlefield, named, new Vec3(0f, 100f, 0f, -1f), 0f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.Received(1).ApplyChargeLine(NearXY(0f, 5f), Near(new Vec2(0f, 1f)), Arg.Any<int>());
        _sut.Tick(cav, commands, battlefield, 0.1f, 1f);
        commands.Received(1).IssueChargeToTarget(named);
        commands.DidNotReceive().IssueChargeToTarget(nearest);
    }

    [TestMethod]
    public void RetargetCycle_WhileCharging_ReissuesTheChargeAtTheNamedTarget()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, nearest) = DriveToCharging(commands, battlefield);
        var named = new object();

        _sut.RetargetCycle(Relocate(cav, new Vec2(20f, 0f)), commands, battlefield, named, new Vec3(100f, 60f, 0f, -1f), 1.5f);

        Assert.AreEqual(CavalryState.Charging, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueChargeToTarget(named);
        commands.Received(1).IssueChargeToTarget(nearest);
        commands.Received(1).ApplyChargeLine(Arg.Any<Vec3>(), Arg.Any<Vec2>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RetargetCycle_IdleOrSameTarget_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        _sut.RetargetCycle(cav, commands, battlefield, new object(), Target3, 0f);
        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));

        var target = new object();
        _sut.HandleChargeOrder(cav, commands, battlefield, target, Target3, 0f);
        commands.ClearReceivedCalls();
        _sut.RetargetCycle(cav, commands, battlefield, target, Target3, 0.1f);
        commands.DidNotReceiveWithAnyArgs().ApplyChargeLine(default, default, default);
    }

    // ============ Reform point clears the enemy's depth ============

    [TestMethod]
    public void Tick_ChargingContact_ReformPointClearsTheTargetDepthAlongTheChargeAxis()
    {
        // A 40 m deep column past its centre: the reform point is 25 m past its far edge, not 25 m
        // past its centre plane (92 + 8 + 40 + 25 = 165).
        var commands = MakeCommands();
        commands.GetTargetDepthAlong(Arg.Any<object>(), Arg.Any<Vec2>()).Returns(40f);
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        commands.Received(1).IssueMoveTo(Near(new Vec2(165f, 0f)), Arg.Any<float>());
    }

    [TestMethod]
    public void Tick_ChargingContact_NaNTargetDepthAddsNothing()
    {
        var commands = MakeCommands();
        commands.GetTargetDepthAlong(Arg.Any<object>(), Arg.Any<Vec2>()).Returns(float.NaN);
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);

        _sut.Tick(Relocate(cav, new Vec2(92f, 0f)), commands, battlefield, 0.1f, 2f);

        commands.Received(1).IssueMoveTo(Near(new Vec2(125f, 0f)), Arg.Any<float>());
    }

    // ============ Repeat cycles reroute like the first ============

    [TestMethod]
    public void Tick_ReformingAligned_FriendlyOnTheNextChargeLine_Reroutes()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield(nearestEnemy: new object(), nearestEnemyPosition: new Vec2(200f, 0f));
        var (cav, _) = DriveToReforming(commands, battlefield);
        StubPlannerReroute(new Vec2(160f, -12f));

        _sut.Tick(Relocate(cav, new Vec2(125f, 0f), isAligned: true), commands, battlefield, 0.1f, 4f);

        Assert.AreEqual(CavalryState.Rerouting, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueMoveTo(Near(new Vec2(160f, -12f)), Arg.Any<float>());
    }

    // ============ Tick ownership order ============

    [TestMethod]
    public void Tick_FormationNoLongerCavalry_HandsBackToVanillaCharge()
    {
        // Riders dismounted mid-cycle: the machine gives up the cycle with a charge, never a Cancel
        // that would leave the Move it issued standing.
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        var dismounted = MakeCav(new Vec2(40f, 0f), formationKey: cav.FormationKey);
        dismounted.RepresentativeIsCavalry.Returns(false);

        _sut.Tick(dismounted, commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.Received(1).IssueCharge();
    }

    [TestMethod]
    public void Tick_AIControlledAndDisabled_CancelsWithoutIssuingAnOrder()
    {
        // Ownership wins over the toggle: a formation the team AI now commands gets no order from us.
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();
        var (cav, _) = DriveToCharging(commands, battlefield);
        commands.ClearReceivedCalls();
        _settings.IsEnabled.Returns(false);
        var delegated = MakeCav(new Vec2(40f, 0f), formationKey: cav.FormationKey, isAIControlled: true);

        _sut.Tick(delegated, commands, battlefield, 0.1f, 2f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceiveWithAnyArgs().IssueCharge();
    }

    // ============ Idle, mission end, lookups ============

    [TestMethod]
    public void Tick_IdleState_DoesNothing()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();

        _sut.Tick(cav, commands, MakeBattlefield(), 0.1f, 1f);

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
        commands.DidNotReceiveWithAnyArgs().IssueStop();
        commands.DidNotReceiveWithAnyArgs().IssueMoveTo(default, default);
        commands.DidNotReceiveWithAnyArgs().IssueChargeToTarget(default!);
        commands.DidNotReceiveWithAnyArgs().IssueCharge();
        commands.DidNotReceiveWithAnyArgs().ApplyChargeLine(default, default, default);
    }

    [TestMethod]
    public void Tick_NullFormationKey_DoesNotThrow()
    {
        var cav = Substitute.For<IFormationAdapter>();
        cav.FormationKey.Returns((object)null!);
        cav.RepresentativeIsCavalry.Returns(true);
        cav.CurrentPosition.Returns(Vec2.Zero);

        _sut.Tick(cav, MakeCommands(), MakeBattlefield(), 0.1f, 1f);
    }

    [TestMethod]
    public void Tick_NotFieldBattle_DoesNotDriveStateMachine()
    {
        var cav = MakeCav(Vec2.Zero, isAligned: true);
        var commands = MakeCommands();
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        _sut.Tick(cav, commands, MakeBattlefield(isFieldBattle: false), 0.1f, 1f);

        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));
        commands.DidNotReceive().IssueChargeToTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void OnMissionEnd_ClearsAllPerFormationState()
    {
        var cav = MakeCav(Vec2.Zero);
        _sut.HandleChargeOrder(cav, MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);
        Assert.AreEqual(CavalryState.Forming, _sut.GetState(cav.FormationKey));

        _sut.OnMissionEnd();

        Assert.AreEqual(CavalryState.Idle, _sut.GetState(cav.FormationKey));
    }

    [TestMethod]
    public void GetState_UnknownFormationKey_ReturnsIdle()
    {
        Assert.AreEqual(CavalryState.Idle, _sut.GetState(new object()));
    }

    // ============ Settings consumption (dead-promise gate) ============

    [TestMethod]
    public void HandleChargeOrder_ReadsIsEnabled()
    {
        _sut.HandleChargeOrder(MakeCav(Vec2.Zero), MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);
        _ = _settings.Received().IsEnabled;
    }

    [TestMethod]
    public void HandleChargeOrder_ReadsAvoidFriendlies()
    {
        _sut.HandleChargeOrder(MakeCav(Vec2.Zero), MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);
        _ = _settings.Received().AvoidFriendlies;
    }

    [TestMethod]
    public void HandleChargeOrder_ReadsChargeLineSpacing_WhenForming()
    {
        _sut.HandleChargeOrder(MakeCav(Vec2.Zero), MakeCommands(), MakeBattlefield(), new object(), Target3, 0f);
        _ = _settings.Received().ChargeLineSpacing;
    }

    [TestMethod]
    public void Tick_FormingState_ReadsChargeFormationStrictness()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);
        _settings.ClearReceivedCalls();

        _sut.Tick(cav, commands, MakeBattlefield(), 0.1f, 1f);

        _ = _settings.Received().ChargeFormationStrictness;
    }

    [TestMethod]
    public void Tick_FormingState_ReadsMaxLineUpSeconds()
    {
        var cav = MakeCav(Vec2.Zero);
        var commands = MakeCommands();
        _sut.HandleChargeOrder(cav, commands, MakeBattlefield(), new object(), Target3, 0f);
        _settings.ClearReceivedCalls();

        _sut.Tick(cav, commands, MakeBattlefield(), 0.1f, 1f);

        _ = _settings.Received().MaxLineUpSeconds;
    }

    // ============ Observability ============

    [TestMethod]
    public void Tick_Transition_LogsTheStateChange()
    {
        var commands = MakeCommands();
        var battlefield = MakeBattlefield();

        DriveToCharging(commands, battlefield);

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("[SmartCavalryAI]") && s.Contains("Charging")));
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
            Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed,
                "Counter must not clear after inner dispose; outer scope still active.");
        }
        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed);
    }

    [TestMethod]
    public void RecursionGuard_Reset_ClearsStuckFlag()
    {
        SmartCavalryRecursionGuard.Enter();  // never disposed: simulates abnormal termination
        Assert.IsTrue(SmartCavalryRecursionGuard.IsSuppressed);

        SmartCavalryRecursionGuard.Reset();

        Assert.IsFalse(SmartCavalryRecursionGuard.IsSuppressed,
            "Reset() must clear stuck flag from abnormal mission termination.");
    }
}

// Note: SmartCavalryAISettingsProvider has no direct unit tests. It reads TaomSettings.Instance,
// which inherits from MCMv5's AttributeGlobalSettings and triggers an MCMv5 assembly load that is
// unavailable in the test host. Its clamps go through the shared SettingClamp, which is tested.
