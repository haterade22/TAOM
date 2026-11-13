using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The siege engines a player destroys come back for the next wave (#1160). A destroyed ram or tower has been disabled ("destructionComponent.GameEntity.GetFirstScriptOfType&lt;UsableMachine&gt;()?.Disable();
/// destructionComponent?.SetAbilityOfFaces(enabled: false);", <c>Mission.OnObjectDisabled</c>), which leaves it off <c>ActiveMissionObjects</c>, and every tick <c>SiegeLane.RefreshLane</c> drops a disabled
/// weapon from its lane. The restore runs vanilla's own mission reset, found by name on each type, then sets back the tower state that a reset in the middle of a mission leaves unlike the boot, puts the
/// engine back into the lane of its side, and has the attackers' formations list their engines again. The engines are the ones the boot deployed, held since then because a destroyed one leaves the
/// active list.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    private const string BreakableWallTag = "breakable_wall";

    private MethodInfo? _towerReset;
    private MethodInfo? _ramReset;
    private AccessTools.FieldRef<SiegeTower, List<LadderQueueManager>>? _towerQueues;
    private AccessTools.FieldRef<UsableMachine, GameEntity>? _activeWait;
    private MethodInfo? _waitPoints;
    private bool _engineMembersFound;

    // DestructableComponent.Reset() shows the intact entity again, sets "_hitPoint = MaxHitPoint; _currentStateIndex = 0;" and keeps the maximum the Hammer last wrote, which the Hammer writes again after
    // this. The destructible's own SetAbilityOfFaces(true) undoes the destroy's SetAbilityOfFaces(false); it acts on the destructible's navmesh prefab, which the Native ram and tower prefabs leave empty, so
    // it does nothing there. The machine's reset is "protected internal override" on SiegeTower and BatteringRam, so only reflection reaches it: UsableMachine.OnMissionReset enables the disabled machine
    // ("SetEnabled(); SetScriptComponentToTick(GetTickRequirement());", which puts it back on the active list and, through MissionObject.SetEnabled, turns the machine's faces on) and resets each component,
    // so SiegeWeaponMovementComponent.OnMissionReset resets its path tracker and moves it to the start of its path. The tower's reset closes its gate, turns its gate navmesh off and wakes its push points.
    // It also shows the breakable wall it targets as broken ("_nonDestroyedWallEntity.SetVisibilityExcludeParents(visible: false);"), which no fall of a tower does, so every breakable wall's two states are
    // read before and written back after. Vanilla runs this reset only after it removes every agent (Mission.ResetMissionObjects); here the field still holds the last wave's dead and the defenders, which is
    // why SettleTower sets back what the boot sets and the reset does not. The barriers a tower's fall shows (SiegeTower.OnDestroyed) stay as vanilla left them.
    public LdEngineRestore RestoreEngines()
    {
        var destroyed = Engines().Where(e => e.Weapon.IsDestroyed).ToList();
        if (destroyed.Count == 0)
            return LdEngineRestore.None;

        FindEngineMembers();
        var walls = destroyed.Any(e => e.Weapon is SiegeTower) ? WallStates() : new List<(GameEntity Entity, bool Visible)>();
        var restored = new List<LdEngineState>();
        var problems = new List<string>();
        foreach (var (weapon, kind, index) in destroyed)
        {
            var reset = weapon is SiegeTower ? _towerReset : _ramReset;
            if (reset == null || weapon.DestructionComponent == null)
            {
                problems.Add(reset == null ? weapon.GetType().Name + ".OnMissionReset was not found" : kind + index + " has no destructible");
                continue;
            }

            var ran = false;
            try
            {
                weapon.DestructionComponent.Reset();
                weapon.DestructionComponent.SetAbilityOfFaces(true);
                reset.Invoke(weapon, null);
                ran = true;
            }
            catch (TargetInvocationException ex)
            {
                problems.Add(weapon.GetType().Name + ".OnMissionReset threw " + ex.InnerException?.GetType().Name);
                _logger.LogWarning($"[LongDefeat][Engines] {kind}{index}: {weapon.GetType().Name}.OnMissionReset threw {ex.InnerException?.GetType().Name}: {ex.InnerException?.Message}");
            }

            if (ran && weapon is SiegeTower tower)
                SettleTower(tower);
            Relane((IPrimarySiegeWeapon)weapon);
            restored.Add(Read(weapon, kind, index));
        }

        foreach (var (entity, visible) in walls)
            entity.SetVisibilityExcludeParents(visible);
        if (restored.Count > 0)
            _logger.LogInfo($"[LongDefeat][Engines] {RelistEngines()} attacker formation(s) listed their siege engines again");
        return new LdEngineRestore(restored, problems.Count == 0 ? null : string.Join("; ", problems));
    }

    public IReadOnlyList<LdEngineState> ReadEngines()
    {
        FindEngineMembers();
        return Engines().Select(e => Read(e.Weapon, e.Kind, e.Index)).ToList();
    }

    // As TryBreak breaks a hidden gate: OnHit gives an empty weapon 0 damage unless the attacker is a ram ("if (weapon.IsEmpty && !(attackerScriptComponentBehavior is BatteringRam))"). A destructible that
    // only stones break then reads the empty weapon's usage item, which an empty weapon has none of, so the flag is cleared for this one hit. The hit runs the whole destroy path, OnObjectDisabled included.
    // Every kill needs a living owner, as Fell's do: a tower's fall kills whoever stands on it (SiegeTower.OnDestroyed, "KillAgentsOnEntity(CurrentState, destroyerAgent)"), and
    // CustomBattleMoraleModel.CalculateMaxMoraleChangeDueToAgentIncapacitated reads "affectorAgent.Formation" with no null test, so the hit is owned by the player's body or a standing defender.
    public int BreakEngines()
    {
        var ram = _mission.MissionObjects.FindAllWithType<BatteringRam>().FirstOrDefault();
        if (ram == null)
        {
            _logger.LogWarning("[LongDefeat][Engines] break_engines: the scene has no ram to strike with, so no engine was broken");
            return 0;
        }

        var destroyer = LivingOwner();
        if (destroyer == null)
        {
            _logger.LogWarning("[LongDefeat][Engines] break_engines: no defender stands to own the hit, so no engine was broken");
            return 0;
        }

        var broken = 0;
        var weapon = MissionWeapon.Invalid;
        foreach (var (engine, _, _) in Engines().Where(e => !e.Weapon.IsDestroyed).ToList())
        {
            var component = engine.DestructionComponent;
            if (component == null)
                continue;

            var stoneOnly = component.DestroyedByStoneOnly;
            component.DestroyedByStoneOnly = false;
            try
            {
                component.TriggerOnHit(destroyer, (int)component.HitPoint + 1, engine.GameEntity.GlobalPosition, Vec3.Forward, in weapon, -1, ram);
            }
            finally
            {
                component.DestroyedByStoneOnly = stoneOnly;
            }

            if (component.IsDestroyed)
                broken++;
        }

        return broken;
    }

    private IEnumerable<(SiegeWeapon Weapon, string Kind, int Index)> Engines() =>
        _towers.Select((t, i) => ((SiegeWeapon)t, "tower", i)).Concat(_rams.Select((r, i) => ((SiegeWeapon)r, "ram", i)));

    private void FindEngineMembers()
    {
        if (_engineMembersFound)
            return;

        _engineMembersFound = true;
        _towerReset = AccessTools.DeclaredMethod(typeof(SiegeTower), "OnMissionReset");
        _ramReset = AccessTools.DeclaredMethod(typeof(BatteringRam), "OnMissionReset");
        _towerQueues = FieldRef<SiegeTower, List<LadderQueueManager>>("_queueManagers");
        _activeWait = FieldRef<UsableMachine, GameEntity>("ActiveWaitStandingPoint");
        _waitPoints = AccessTools.PropertyGetter(typeof(UsableMachine), "WaitStandingPoints");
        var missing = new[]
        {
            _towerReset == null ? "SiegeTower.OnMissionReset" : null,
            _ramReset == null ? "BatteringRam.OnMissionReset" : null,
            _towerQueues == null ? "SiegeTower._queueManagers" : null,
            _activeWait == null ? "UsableMachine.ActiveWaitStandingPoint" : null,
            _waitPoints == null ? "UsableMachine.WaitStandingPoints" : null,
        }.Where(m => m != null).ToList();
        if (missing.Count > 0)
            _logger.LogWarning($"[LongDefeat][Engines] not found: {string.Join(", ", missing)}; a destroyed engine of a type with no reset cannot come back, and a tower with no queues or wait point reads unsettled");
    }

    private static AccessTools.FieldRef<T, F>? FieldRef<T, F>(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<T, F>(name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // What a reset in the middle of a mission leaves unlike the boot. UsableMachine.OnMissionReset's SetEnabled calls the tower's SetAbilityOfFaces(true) override, which turns on every ladder queue's face and
    // activates the queue (SiegeTower.cs:514-535); the boot turns both off (:759-763) until the tower arrives (:211-215). The tower's arrival sets "ActiveWaitStandingPoint = base.WaitStandingPoints[1];" (:208)
    // and nothing sets it back; the boot sends the waiting soldiers to WaitStandingPoints[0] (UsableMachine.cs:400), the lower point after the tower's swap (SiegeTower.cs:720-728).
    private void SettleTower(SiegeTower tower)
    {
        foreach (var queue in (_towerQueues == null ? null : _towerQueues(tower)) ?? new List<LadderQueueManager>())
        {
            _mission.Scene.SetAbilityOfFacesWithId(queue.ManagedNavigationFaceId, false);
            queue.DeactivateImmediate();
        }

        var waits = _waitPoints?.Invoke(tower, null) as List<GameEntity>;
        if (_activeWait != null && waits is { Count: > 0 })
            _activeWait(tower) = waits[0];
    }

    // Each attacker formation's BehaviorUseSiegeMachines keeps its own list of engines, built once from the active list when the behavior is made (TeamAISiegeAttacker.OnUnitAddedToFormationForTheFirstTime),
    // and its TickOccasionally drops a destroyed one. The formations last across waves, so a restored engine stays off that list until the public OnValidBehaviorSideChanged builds it again from the active
    // list ("!((SiegeWeapon)activeMissionObject).IsDeactivated") and picks the engine to follow again.
    private int RelistEngines()
    {
        var listed = 0;
        foreach (var formation in _mission.AttackerTeam?.FormationsIncludingEmpty ?? Enumerable.Empty<Formation>())
        {
            var behavior = formation.AI?.GetBehavior<BehaviorUseSiegeMachines>();
            if (behavior == null)
                continue;
            behavior.OnValidBehaviorSideChanged();
            listed++;
        }

        return listed;
    }

    private List<(GameEntity Entity, bool Visible)> WallStates() =>
        _mission.Scene.FindEntitiesWithTag(BreakableWallTag)
            .SelectMany(w => w.CollectChildrenEntitiesWithTag("destroyed").Concat(w.CollectChildrenEntitiesWithTag("non_destroyed")))
            .Select(e => (e, e.GetVisibilityExcludeParents()))
            .ToList();

    // The lanes list is static and holds the machine's interface; TeamAISiegeComponent builds each lane from "um.WeaponSide == (FormationAI.BehaviorSide)i", so the engine goes back into the lane of its side.
    private static void Relane(IPrimarySiegeWeapon weapon)
    {
        if (InLane(weapon))
            return;
        TeamAISiegeComponent.SiegeLanes?.FirstOrDefault(l => l.LaneSide == weapon.WeaponSide)?.PrimarySiegeWeapons.Add(weapon);
    }

    private static bool InLane(IPrimarySiegeWeapon weapon) => TeamAISiegeComponent.SiegeLanes?.Any(l => l.PrimarySiegeWeapons.Contains(weapon)) == true;

    // The machine's own arrival, not the movement component's: that one answers true for a weapon with no path.
    private LdEngineState Read(SiegeWeapon weapon, string kind, int index)
    {
        var component = weapon.DestructionComponent;
        var (movement, arrived) = weapon switch
        {
            SiegeTower tower => (tower.MovementComponent, tower.HasArrivedAtTarget),
            BatteringRam ram => (ram.MovementComponent, ram.HasArrivedAtTarget),
            _ => ((SiegeWeaponMovementComponent?)null, false),
        };
        var (queuesOff, waitAtStart) = weapon is SiegeTower t ? TowerState(t) : (true, true);
        var primary = (IPrimarySiegeWeapon)weapon;
        return new LdEngineState(kind, index, weapon.IsDestroyed, weapon.IsDisabled, component?.HitPoint ?? float.NaN, component?.MaxHitPoint ?? float.NaN,
            movement?.GetTotalDistanceTraveledForPathTracker() ?? float.NaN, arrived, InLane(primary), primary.HasCompletedAction(), queuesOff, waitAtStart);
    }

    // A member that could not be read reads as unsettled, so the restore check fails on it.
    private (bool QueuesOff, bool WaitAtStart) TowerState(SiegeTower tower)
    {
        var queues = _towerQueues == null ? null : _towerQueues(tower);
        var waits = _waitPoints?.Invoke(tower, null) as List<GameEntity>;
        var active = _activeWait == null ? null : _activeWait(tower);
        return (queues != null && queues.All(q => q.IsDeactivated), _activeWait != null && waits != null && (waits.Count == 0 || active == waits[0]));
    }
}
