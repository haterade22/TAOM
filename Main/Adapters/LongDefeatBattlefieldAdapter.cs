using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.AI;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// All engine work on the Long Defeat field (#1079): the deployment, the siege engines, spawning, measuring, ordering and the
/// player's body. Main thread, except <see cref="Classify"/> and <see cref="IsAttacker"/>.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter : ILongDefeatBattlefieldAdapter
{
    private const float SpawnLevelTolerance = 10f;
    private const float FramePointRadius = 1f;
    private const float WallMargin = 3f;
    private const float GateToWallFallback = 4f;
    private const int SampleSize = 8;

    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private readonly LongDefeatAgentRegistry _registry;

    // The lord's body, the body a remount faded and the callbacks that reached them; by object reference, any thread.
    private readonly LongDefeatLordWatch _lordWatch = new();

    private readonly Dictionary<(bool Attackers, FormationSlot Slot), int> _open = new();

    // The recorded muster place each defender class spawns at while its batch is open (SetMusterFrame to EndBatch). A class with no entry spawns at the scene's frame.
    private readonly Dictionary<FormationSlot, Vec3> _muster = new();

    // A destroyed gate, tower or ram leaves ActiveMissionObjects (Mission.OnObjectDisabled), so these lists are taken once, at deployment.
    private List<CastleGate> _gates = new();
    private List<SiegeLadder> _ladders = new();
    private List<SiegeTower> _towers = new();
    private List<BatteringRam> _rams = new();
    private Agent? _lastMain;
    private volatile Agent? _knownMain;
    private int _bodySequence;
    private float _highZ = float.MaxValue;

    /// <param name="registry">Where each spawned attacker's role is kept; the effects adapter reads the same one. A fresh one when none is given.</param>
    public LongDefeatBattlefieldAdapter(Mission mission, IModLogger logger, LongDefeatAgentRegistry? registry = null)
    {
        _mission = mission;
        _logger = logger;
        _registry = registry ?? new LongDefeatAgentRegistry();
    }

    private Team Side(bool attackers) => attackers ? _mission.AttackerTeam : _mission.DefenderTeam;

    private static FormationClass Class(FormationSlot slot) => slot == FormationSlot.Ranged ? FormationClass.Ranged : FormationClass.Infantry;

    private static LdPoint Point(Vec3 v) => new(v.x, v.y, v.z);

    public bool Boot(LdPlanSizes sizes, out string problem)
    {
        var attackers = _mission.AttackerTeam;
        var defenders = _mission.DefenderTeam;
        if (attackers == null || defenders == null)
        {
            problem = "the mission has no attacker or defender team";
            return false;
        }

        // Vanilla gives the plan its troop counts first, because they set each formation's planned width and depth (DefaultBattleMissionAgentSpawnLogic.cs:675, :690-694).
        // Mission.cs:1804-1807: with no MissionDeploymentPlanningLogic in the list, the mission builds this type.
        if (!(_mission.DeploymentPlan is DefaultMissionDeploymentPlan plan))
        {
            problem = $"the deployment plan is a {_mission.DeploymentPlan?.GetType().Name ?? "null"}";
            return false;
        }

        try
        {
            plan.AddTroops(defenders, FormationClass.Infantry, sizes.DefenderFoot);
            plan.AddTroops(defenders, FormationClass.Ranged, sizes.DefenderRanged);
            plan.AddTroops(attackers, FormationClass.Infantry, sizes.AttackerFoot, 0, true);
            plan.AddTroops(attackers, FormationClass.Ranged, sizes.AttackerRanged, 0, true);
            plan.MakeDefaultDeploymentPlans();
        }
        catch (Exception ex)
        {
            problem = $"the deployment plan threw {ex.GetType().Name}: {ex.Message}";
            return false;
        }

        var missing = new List<string>();
        foreach (var (team, cls, reinforcement, label) in Frames(attackers, defenders))
        {
            if (FrameOf(team, cls, reinforcement) == null)
                missing.Add(label);
        }

        problem = missing.Count == 0 ? "" : "no spawn frame for " + string.Join(", ", missing);
        return missing.Count == 0;
    }

    private static IEnumerable<(Team Team, FormationClass Class, bool Reinforcement, string Label)> Frames(Team attackers, Team defenders)
    {
        yield return (attackers, FormationClass.Infantry, true, "attacker foot");
        yield return (attackers, FormationClass.Ranged, true, "attacker archers");
        yield return (defenders, FormationClass.Infantry, false, "defender foot");
        yield return (defenders, FormationClass.Ranged, false, "defender archers");
        yield return (defenders, FormationClass.Infantry, true, "reinforcement foot");
        yield return (defenders, FormationClass.Ranged, true, "reinforcement archers");
    }

    private Vec3? FrameOf(Team team, FormationClass cls, bool reinforcement)
    {
        try
        {
            _mission.GetFormationSpawnFrame(team, cls, reinforcement, out var position, out _);
            var at = position.GetGroundVec3();
            return !at.IsValid || at.LengthSquared < 1f ? null : at;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[LongDefeat] GetFormationSpawnFrame {team.Side} threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // The engine's InnerGate ignores visibility (TeamAISiegeComponent.cs:54-57), and a hidden gate never ticks (CastleGate.cs:619-622), so it never opens. Helm's Deep hides one at height 32.5
    // (scene.xscene:5971): the Middle lane stays closed (SiegeLane.cs:355-364), and BehaviorAssaultWalls (:162) never leaves AttackEntity. A null InnerGate throws in that behavior, so the gate is broken, not removed.
    public int BreakHiddenGates()
    {
        var all = _mission.ActiveMissionObjects.FindAllWithType<CastleGate>().ToList();
        _gates = all.Where(g => g.GameEntity.IsVisibleIncludeParents()).OrderByDescending(g => g.GameEntity.HasTag("outer_gate")).ToList();
        var walls = _mission.ActiveMissionObjects.FindAllWithType<WallSegment>().Where(w => w.DefenseSide != FormationAI.BehaviorSide.BehaviorSideNotSet).ToList();
        _highZ = HighLine(_gates.FirstOrDefault(), walls);
        var broken = 0;
        foreach (var hidden in all.Where(g => !_gates.Contains(g)))
        {
            var component = hidden.GameEntity.GetFirstScriptOfType<DestructableComponent>();
            var done = component != null && TryBreak(hidden, component);
            _logger.LogInfo($"[LongDefeat][Boot] hidden gate at {At(hidden.GameEntity.GlobalPosition)}: {(done ? "broken" : "NOT broken")}, destroyed {hidden.IsDestroyed} open {hidden.IsGateOpen}");
            if (done)
                broken++;
        }

        return broken;
    }

    // OnHit gives an empty weapon 0 damage unless the attacker is a ram, and a ram plays the gate's break animation. MissionObjects, not the active list: only the type matters.
    private bool TryBreak(CastleGate gate, DestructableComponent component)
    {
        var ram = _mission.MissionObjects.FindAllWithType<BatteringRam>().FirstOrDefault();
        if (ram == null)
            return false;
        var weapon = MissionWeapon.Invalid;
        component.TriggerOnHit(null, (int)component.HitPoint + 1, gate.GameEntity.GlobalPosition, Vec3.Forward, in weapon, -1, ram);
        return true;
    }

    public void DeployEngines()
    {
        var engines = _mission.GetMissionBehavior<MissionSiegeEnginesLogic>();
        var attacker = Points().Where(p => p.Side == BattleSideEnum.Attacker).ToList();
        new SiegeWeaponAutoDeployer(attacker, engines.GetSiegeWeaponsController(BattleSideEnum.Attacker)).DeployAll(BattleSideEnum.Attacker);
        foreach (var point in attacker)
        {
            foreach (var weapon in point.DeployableWeapons.ToArray())
            {
                if ((point.DeployedWeapon == null || !weapon.GameEntity.IsVisibleIncludeParents()) && weapon is SiegeWeapon siege)
                    siege.SetDisabledSynched();
            }

            SetVanillaHitPoints(point.DeployedWeapon as SiegeWeapon);
            point.SetDisabledSynched();
        }

        // A deployed tower hides the ladders on its side, and vanilla then disables them (SiegeDeploymentMissionController.BeforeDeploymentFinished).
        foreach (var ladder in _mission.ActiveMissionObjects.FindAllWithType<SiegeLadder>().Where(l => !l.GameEntity.IsVisibleIncludeParents()).ToList())
            ladder.SetDisabledSynched();

        // SiegeDeploymentHandler.RemoveDeploymentPoints disables every point at the end of deployment, and that handler is absent here.
        foreach (var point in Points().Where(p => p.Side == BattleSideEnum.Defender))
        {
            foreach (var weapon in point.DeployableWeapons.ToArray())
            {
                if (weapon is SiegeWeapon siege)
                    siege.SetDisabledSynched();
            }

            point.SetDisabledSynched();
        }

        _ladders = Live<SiegeLadder>();
        _towers = Live<SiegeTower>();
        _rams = Live<BatteringRam>();
    }

    private List<T> Live<T>() where T : SiegeWeapon =>
        _mission.ActiveMissionObjects.FindAllWithType<T>().Where(w => !w.IsDisabled && w.GameEntity.IsVisibleIncludeParents()).ToList();

    // SiegeDeploymentHandler sets these through OnWeaponDeployed. It is absent, and that call throws for a type outside the launcher's list, so the prefab's hit points would stay (a tower has 20000 there, 5000 in vanilla).
    private static void SetVanillaHitPoints(SiegeWeapon? weapon)
    {
        var component = weapon?.GameEntity.GetFirstScriptOfType<DestructableComponent>();
        if (component == null)
            return;
        component.MaxHitPoint = weapon!.GetSiegeEngineType().BaseHitPoints;
        component.Reset();
    }

    private List<DeploymentPoint> Points() => _mission.ActiveMissionObjects.FindAllWithType<DeploymentPoint>().ToList();

    public LdPoint? KeepFromScene() =>
        _mission.DefenderTeam is { } team && FrameOf(team, FormationClass.Infantry, true) is { } at ? Point(at) : null;

    // Vanilla's order, MissionBattleSideSpawnContext.cs:371-372. Vanilla does this only for a formation with no place yet (:369), so the run asks for it once per
    // defender formation and once per class per wave: the last wave's formation is empty, and the frame is the place the wave must start from.
    public void PlaceFormation(FormationSlot slot, int count, bool attackers)
    {
        var formation = Side(attackers).GetFormation(Class(slot));
        formation.BeginSpawn(count, false);
        _mission.SetFormationPositioningFromDeploymentPlan(formation, attackers);
        _open[(attackers, slot)] = count;
    }

    public void BeginBatch(FormationSlot slot, int count, bool attackers)
    {
        Side(attackers).GetFormation(Class(slot)).BeginSpawn(count, false);
        _open[(attackers, slot)] = count;
    }

    // Moves the formation to the recorded place and makes SpawnBatch ask for SpawnsIntoOwnFormation. Mission.cs:3950-3953 then takes the spawn frame from the formation's own order
    // position, and :4243-4247 takes the slot from CountOfUnits, so the next free native slot around the place applies, as it does around the scene's frame. A Move order takes the
    // order position back on the next Formation.Tick (Formation.cs:2484-2490), so standing soldiers stay at their posts and each batch sets it again.
    public bool SetMusterFrame(FormationSlot slot, MusterFrame frame)
    {
        var at = new Vec3(frame.X, frame.Y, frame.Z);
        if (_mission.Scene.GetNavigationMeshForPosition(in at) == UIntPtr.Zero)
            return false;

        Side(false).GetFormation(Class(slot)).SetPositioning(new WorldPosition(_mission.Scene, at), new Vec2(frame.Fx, frame.Fy));
        _muster[slot] = at;
        return true;
    }

    // No position here. The engine then takes the place from the formation's layout, which offers only slots that native accepts around the frame (Formation.cs:669-671,
    // LineFormation.cs:495-497), and Mission.GetTroopSpawnFrameWithIndex (:3983-3995) handles a slot with no navmesh. A position computed here skips all of that.
    public LdSpawnReport SpawnBatch(SoldierBatch batch, bool attackers)
    {
        var team = Side(attackers);
        var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(batch.TroopId);
        var points = new List<LdPoint>();
        if (troop == null)
        {
            _logger.LogWarning($"[LongDefeat] spawn: no troop '{batch.TroopId}'");
            return new LdSpawnReport(points, 0, 0, 0);
        }

        var reinforcement = attackers || batch.Reinforcement;
        var open = _open.TryGetValue((attackers, batch.Slot), out var started) ? started : batch.BatchTotal;
        Vec3 musterAt = default;
        var mustered = !attackers && _muster.TryGetValue(batch.Slot, out musterAt);
        var frame = mustered ? musterAt : FrameOf(team, Class(batch.Slot), reinforcement);
        int far = 0, offMesh = 0, onPoint = 0;
        for (var i = 0; i < batch.Count; i++)
        {
            var agent = Spawn(troop, team, !attackers, Class(batch.Slot), reinforcement, open, batch.FirstIndex + i, false, mustered);
            ScaleHealth(agent, batch.HealthFactor);
            if (attackers)
                _registry.Register(agent, batch.Role);
            var at = agent.Position;
            points.Add(Point(at));
            if (frame is not { } f)
                continue;
            if (!(Math.Abs(at.z - f.z) <= SpawnLevelTolerance))
                far++;
            if (_mission.Scene.GetNavigationMeshForPosition(in at) == UIntPtr.Zero)
                offMesh++;
            if (at.AsVec2.Distance(f.AsVec2) <= FramePointRadius)
                onPoint++;
        }

        return new LdSpawnReport(points, far, offMesh, onPoint);
    }

    // BaseHealthLimit and HealthLimit have public setters (Agent.cs:911, :915) and the engine's own horse spawn writes the same three values
    // (Mission.cs:4607-4609). In a Custom Game spawn the only other writer is the Character setter (Agent.cs:1438-1439), which ran before SpawnAgent returned,
    // so this write stands. Written to the base as well, so nothing that recomputes the limit from it undoes the factor.
    private static void ScaleHealth(Agent agent, float factor)
    {
        if (factor == 1f)
            return;

        agent.BaseHealthLimit *= factor;
        agent.HealthLimit = agent.BaseHealthLimit;
        agent.Health = agent.HealthLimit;
    }

    // A kit goes on the build data, and the engine reads it before anything else (Mission.cs:4108). The engine still edits the copy it takes (a hero's banner, NoHorses, NoArmor and
    // CannotBePickedUp weapons, Mission.cs:4109-4164), which is why the check reads the body back. GetAgentBuildDataToSpawnTroop sets NoHorses(true) for spawnWithHorse: false (Mission.cs:4468),
    // so a mounted class keeps passing false there and takes the horse from the kit instead: NoHorses(!mounted) below leaves the kit's Horse slot in place and the engine builds the horse from it
    // (Mission.cs:4311). spawnWithHorse true is not used: it would read a mount key from the troop's own Horse slot (Mission.cs:4500-4503), the lord's horse and not the kit's. With no key the horse is
    // still built, only the engine's visual picks a random coat: "if (string.IsNullOrEmpty(mountCreationKeyStr)) mountCreationKeyStr = MountCreationKey.GetRandomMountKeyString(mountItem, MBRandom.RandomInt());"
    // (MountVisualCreator.cs:41-46), so risk 8 is closed and a new horse after a remount is a new coat.
    // A place goes in as a position with its direction: the engine reads the direction of a given position at once (Mission.cs:4498), and a given position skips the formation layout (Mission.cs:4227).
    private Agent Spawn(BasicCharacterObject troop, Team team, bool playerSide, FormationClass cls, bool reinforcement, int count, int index, bool forceReinforcement,
        bool intoOwnFormation = false, Equipment? kit = null, LdPlace? place = null, bool mounted = false)
    {
        Vec3? initialPosition = place == null ? null : new Vec3(place.X, place.Y, place.Z);
        Vec2? initialDirection = place == null ? null : FacingOf(place);
        var data = _mission.GetAgentBuildDataToSpawnTroop(new BasicBattleAgentOrigin(troop), playerSide, true, false, reinforcement, count, index, initialPosition, initialDirection, formationIndex: cls)
            .Banner(team.Banner);
        if (kit != null)
            data.Equipment(kit).NoHorses(!mounted);

        // GetAgentBuildDataToSpawnTroop skips the reinforcement flag for the player troop (Mission.cs:4477-4480), and without it a respawned lord appears at the foot frame.
        if (forceReinforcement)
            data.IsReinforcement(true);
        if (intoOwnFormation)
            data.SpawnsIntoOwnFormation(true);
        return _mission.SpawnTroopWithAgentBuildData(data, true, true);
    }

    public IReadOnlyList<LdPoint> StandingDefenderPoints() =>
        _mission.DefenderTeam.ActiveAgents.Where(a => a.IsHuman && a.IsActive()).Select(a => Point(a.Position)).ToList();

    public void EndBatch(FormationSlot slot, bool attackers)
    {
        Side(attackers).GetFormation(Class(slot)).EndSpawn();
        _open.Remove((attackers, slot));
        if (!attackers)
            _muster.Remove(slot);
    }

    public void FinishInitialSpawn() => _mission.OnInitialSpawnCompleted();

    // A new tactic per wave: TacticBreachWalls latched its shock assault when it counted the first spawn batch, and ResetTactic keeps the same object. That latch also cleared
    // ForcedUse on every engine, and nothing sets it back for towers and rams. This comes before the reset, so a wave of 100 or fewer still gets vanilla's stop from the new tactic.
    public void RefreshAttackerTactic()
    {
        foreach (var engine in _towers.Cast<SiegeWeapon>().Concat(_rams).Where(w => !w.IsDisabled))
            engine.SetForcedUse(true);
        var team = _mission.AttackerTeam;
        SetOnlyTactic(team, new TacticBreachWalls(team));
    }

    public void OrderCharge()
    {
        _mission.DefenderTeam.DelegateCommandToAI();
        var attackers = _mission.AttackerTeam;
        var defenders = _mission.DefenderTeam;
        SetOnlyTactic(attackers, new TacticCharge(attackers));
        SetOnlyTactic(defenders, new TacticCharge(defenders));
    }

    // The order the engine needs: drop the old options, add the one, then reset so the team picks it up.
    private static void SetOnlyTactic(Team team, TacticComponent tactic)
    {
        team.ClearTacticOptions();
        team.AddTacticOption(tactic);
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
    }

    public void RestoreDefenderTactic()
    {
        var team = _mission.DefenderTeam;
        SetOnlyTactic(team, new TacticDefendCastle(team));
    }

    public LdRest RestDefenders(float share)
    {
        var count = 0;
        var before = 0f;
        var after = 0f;
        foreach (var agent in _mission.DefenderTeam.ActiveAgents)
        {
            var limit = agent.HealthLimit;
            if (!agent.IsHuman || !agent.IsActive() || !(limit > 0f))
                continue;

            var health = agent.Health;
            agent.Health = Math.Min(limit, health + share * (limit - health));
            before += health / limit;
            after += agent.Health / limit;
            count++;
        }

        return count == 0 ? default : new LdRest(count, before / count, after / count);
    }

    private static string DescribeStraggler(Agent agent)
    {
        var formation = agent.Formation;
        var at = agent.Position;
        return $"[LongDefeat][Wave] straggler {agent.Character?.StringId} ranged={agent.IsRangedCached} formation={formation?.RepresentativeClass} "
            + $"behavior={formation?.AI?.ActiveBehavior?.GetType().Name} speed={agent.MovementVelocity.Length:F1} at=({at.x:F1}, {at.y:F1}, {at.z:F1})";
    }

    // KillAgentCheat inflicts 2000 ("blow.InflictedDamage = 2000;", Mission.cs:6228) and HandleBlow kills only when "if (Health < 1f)" holds afterwards (Agent.cs:5529). A commander of
    // wave 36 or later stands above 2000 (rank health times the endless factor), so a felled soldier is first brought under half the blow: it is then lethal whatever his limit.
    private const float FellBlowDamage = 2000f;

    internal static float FellHealth(float health) => Math.Min(health, FellBlowDamage / 2f);

    // Every kill needs a living owner (phase2-plan.md section 4): CustomBattleMoraleModel reads affectorAgent.Formation with no null test, and KillAgentCheat takes "MainAgent ?? agent" as the owner
    // (Mission.cs:6220-6221), so with the player dead the victim would own his own death, or Agent.Die would swap in a stale last hitter (Agent.cs:4689-4693). While the player's body stands
    // KillAgentCheat is used as before. Without it the same blow is built here (a blunt 2000, its debug collision data, RegisterBlow, as KillAgentCheat does at :6216-6254) owned by a
    // standing defender, who gains the kill and the defenders the morale a felled straggler would give the player's body anyway (decision D13). With no defender standing nobody is felled: the run ends on that tick.
    public int Fell(int count)
    {
        if (count <= 0)
            return 0;

        var main = _mission.MainAgent;
        var mainStands = main != null && main.IsActive();
        var owner = mainStands ? main : StandingDefenders().FirstOrDefault(a => a.Formation != null) ?? StandingDefenders().FirstOrDefault();
        if (owner == null)
        {
            _logger.LogWarning("[LongDefeat][Wave] no defender stands to own a felled straggler's death; nobody was felled");
            return 0;
        }

        var left = _mission.AttackerTeam.ActiveAgents.Where(a => a.IsHuman && a.IsActive() && !a.IsRunningAway).Take(count).ToList();
        _logger.LogInfo($"[LongDefeat][Wave] felling {left.Count} straggler(s), kills owned by {(mainStands ? "the player's body" : "defender " + owner.Character?.StringId)}");
        foreach (var agent in left)
        {
            _logger.LogInfo(DescribeStraggler(agent));

            // Marked before the kill, so the removal the engine raises for him is booked as felled by identity (the run keeps no shared counter), whatever order the callbacks come in.
            if (!_registry.MarkFelled(agent))
                _logger.LogWarning($"[LongDefeat][Wave] a felled attacker ({agent.Character?.StringId}) was not in the registry; his removal will be booked as a kill");
            agent.Health = FellHealth(agent.Health);
            if (mainStands)
            {
                _mission.KillAgentCheat(agent);
                continue;
            }

            var blow = new Blow(owner.Index)
            {
                DamageType = DamageTypes.Blunt,
                BoneIndex = agent.Monster.HeadLookDirectionBoneIndex,
                GlobalPosition = agent.Position,
                BaseMagnitude = FellBlowDamage,
                InflictedDamage = (int)FellBlowDamage,
                SwingDirection = agent.LookDirection,
                DamageCalculated = true,
            };
            blow.GlobalPosition.z += agent.GetEyeGlobalHeight();
            blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
            blow.Direction = blow.SwingDirection;
            var collision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(false, false, false, true, false, false, false, false, false, false, false, false, CombatCollisionResult.StrikeAgent,
                -1, 0, 2, blow.BoneIndex, BoneBodyPartType.Head, owner.Monster.MainHandItemBoneIndex, Agent.UsageDirection.AttackLeft, -1, CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Up, blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, agent.Velocity, Vec3.Up);
            agent.RegisterBlow(blow, in collision);
        }

        return left.Count;
    }

    private IEnumerable<Agent> StandingDefenders() => _mission.DefenderTeam.ActiveAgents.Where(a => a.IsHuman && a.IsActive());

    public void AdoptMain(bool aiDriven)
    {
        var main = _mission.MainAgent;
        var team = _mission.DefenderTeam;
        if (main == null || team == null)
            return;

        team.GeneralAgent = main;
        team.PlayerOrderController.Owner = main;
        foreach (var formation in team.FormationsIncludingEmpty)
            formation.PlayerOwner = main;

        // PlayerOwner's setter turns AI off (Formation.cs:292), so the hand-back to AI comes after it.
        team.DelegateCommandToAI();
        if (aiDriven)
            main.Controller = AgentControllerType.AI;
    }

    private static string At(Vec3 v) => $"({Num(v.x)}, {Num(v.y)}, {Num(v.z)})";

    private static string Num(float value) => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    public void Clear()
    {
        _gates = new List<CastleGate>();
        _ladders = new List<SiegeLadder>();
        _towers = new List<SiegeTower>();
        _rams = new List<BatteringRam>();
        _open.Clear();
        _muster.Clear();
        _registry.Clear();
        _lastMain = null;
        _knownMain = null;
        _kitFacts = NoLord;
        _kitSkipped = new List<string>();
        _lordWatch.Clear();
    }
}
