using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.SiegePropDiagnostics.Models;

namespace TAOM.Features.SiegePropDiagnostics.Hooks;

/// <summary>
/// Thin boundary that reads every resupply prop in the live Mission into primitive snapshots and
/// hands them to <see cref="ISiegePropDiagnosticsService"/>. Converts at the boundary so no sealed
/// engine type reaches the service (ADR-007).
///
/// Runs the sweep a few seconds after mission start rather than in AfterStart: scene objects are
/// initialised by then, agents have spawned, and the player has usually moved, so the per-agent
/// probe reports something meaningful. Re-runs on a key-free cadence while the player is near a
/// prop, because the interesting state (ammo, occupancy, reach) changes during the fight.
/// </summary>
public class SiegePropDiagnosticsMissionBehavior : MissionBehavior
{
    private const float InitialSweepDelay = 5f;
    private const float ResweepInterval = 20f;

    private readonly ISiegePropDiagnosticsService _service;
    private readonly ISiegePropDiagnosticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private float _elapsed;
    private float _nextSweep = InitialSweepDelay;

    // BehaviorType=Other: inherits MissionBehavior (not MissionLogic) and overrides no
    // MissionEnded/OnMissionResultReady, so it has no business in Mission.MissionLogics.
    // Returning Logic makes vanilla AddMissionBehavior do `MissionLogics.Add(this as MissionLogic)`,
    // which evaluates to null and NREs the next CheckMissionEnded tick.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public SiegePropDiagnosticsMissionBehavior()
    {
        _service = IoC.Resolve<ISiegePropDiagnosticsService>();
        _settings = IoC.Resolve<ISiegePropDiagnosticsSettingsProvider>();
        _logger = IoC.Resolve<IModLogger>();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (!_settings.IsEnabled) return;

        _elapsed += dt;
        if (_elapsed < _nextSweep) return;
        _nextSweep = _elapsed + ResweepInterval;

        Sweep();
    }

    private void Sweep()
    {
        var mission = Mission.Current;
        if (mission == null) return;

        var snapshots = new List<SiegePropSnapshot>();
        // MissionObjects, NOT GetActiveEntitiesWithScriptComponentOfType — the active list omits
        // objects killed by SetDisabled, which is one of the causes we are trying to detect.
        foreach (var missionObject in mission.MissionObjects)
        {
            if (missionObject is StonePile pile)
                snapshots.Add(Capture(pile, SiegePropKind.RockPile));
            else if (missionObject is AmmoBarrelBase barrel)
                snapshots.Add(Capture(barrel, SiegePropKind.AmmoBarrel));
        }

        foreach (var line in _service.BuildReport(mission.SceneName, mission.IsSiegeBattle, snapshots))
            _logger.LogInfo(line);
    }

    private static SiegePropSnapshot Capture(UsableMachine machine, SiegePropKind kind)
    {
        var player = Agent.Main;
        var pile = machine as StonePile;

        var snapshot = new SiegePropSnapshot
        {
            Id = machine.Id.Id,
            Kind = kind,
            ScriptType = machine.GetType().Name,
            // WeakGameEntity is a struct wrapping a native pointer — probe IsValid, never null-check.
            EntityName = machine.GameEntity.IsValid ? machine.GameEntity.Name : "(no entity)",
            MachineIsDisabled = machine.IsDisabled,
            MachineIsDeactivated = machine.IsDeactivated,
            PlayerIsMounted = player?.MountAgent != null,
        };

        if (pile != null)
        {
            snapshot.GivenItemId = pile.GivenItemID;
            snapshot.AmmoCount = pile.AmmoCount;
            snapshot.StartingAmmoCount = pile.StartingAmmoCount;
            // Reproduces StonePile.OnInit's own lookup exactly. A null here is the silent
            // catastrophe: InitGivenWeapon(null) leaves every point disabled forever.
            snapshot.GivenItemResolves =
                !string.IsNullOrEmpty(pile.GivenItemID)
                && MBObjectManager.Instance?.GetObject<ItemObject>(pile.GivenItemID) != null;
        }

        var points = machine.StandingPoints;
        if (points == null) return snapshot;

        snapshot.StandingPointCount = points.Count;
        // AmmoPickUpPoints itself is protected internal, so recompute it from the public tag.
        snapshot.AmmoPickupPointCount =
            points.Count(p => p != null && p.GameEntity.IsValid && p.GameEntity.HasTag(machine.AmmoPickUpTag));

        foreach (var point in points)
        {
            if (point == null) continue;
            if (point.IsDeactivated) snapshot.DeactivatedPointCount++;
            if (point.HasUser) snapshot.OccupiedPointCount++;
            if (player != null && point.IsDisabledForAgent(player)) snapshot.DisabledForPlayerPointCount++;
        }

        if (player == null) return snapshot;

        // The decisive engine verdict: this is the exact call the interaction system makes to
        // decide whether to move focus from the machine root onto a usable point.
        snapshot.PlayerProbeValid = machine.GetValidVacantReachableStandingPointForAgent(player).IsValid;
        CaptureNearestPointGeometry(points, player, snapshot);

        return snapshot;
    }

    private static void CaptureNearestPointGeometry(
        IReadOnlyList<StandingPoint> points, Agent player, SiegePropSnapshot snapshot)
    {
        var playerPosition = player.Position;
        float? bestDistanceSquared = null;
        StandingPoint? nearest = null;

        foreach (var point in points)
        {
            if (point == null) continue;
            var origin = point.GetUserFrameForAgent(player).Origin;
            var distanceSquared = origin.AsVec2.DistanceSquared(playerPosition.AsVec2);
            if (bestDistanceSquared.HasValue && !(distanceSquared < bestDistanceSquared.Value)) continue;
            bestDistanceSquared = distanceSquared;
            nearest = point;
        }

        if (nearest == null) return;

        snapshot.NearestPointDistanceSquared = bestDistanceSquared;

        var reach = player.GetInteractionDistanceToUsable(nearest);
        snapshot.InteractionDistanceSquared = reach * reach;

        // Mirrors the engine's own height test: own position when the point opts out of world
        // position, otherwise the ground height under the resolved user frame.
        var groundZ = nearest.UseOwnPositionInsteadOfWorldPosition
            ? nearest.GameEntity.GlobalPosition.z
            : nearest.GetUserFrameForAgent(player).Origin.GetGroundVec3().z;

        snapshot.NearestGroundHeightDelta = System.Math.Abs(groundZ - playerPosition.z);
    }
}
