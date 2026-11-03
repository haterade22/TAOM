using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.CultureDoctrine.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.AI;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>The Phase 0 spike (#1079, docs/features/long-defeat.md): one run on the main thread answers S1 to S5 and S7 through tagged log lines.</summary>
internal sealed class LongDefeatSpikeRunner
{
    internal const string DoneFile = "long_defeat_done.txt";
    internal const string StatusFile = "long_defeat_status.txt";

    private const string Tag = "[LongDefeat]";
    private const float LogSeconds = 5f;
    private const int SpawnsPerTick = 8;
    private const float WallMargin = 3f;
    private const float GateToWallFallback = 4f;
    private const float GateRepairDelay = 5f;

    private enum Phase { Boot, Spawning, Fighting, Intermission, Finished }

    private readonly Mission _mission;
    private readonly LongDefeatSpikeOptions _options;
    private readonly LongDefeatSpikeFortress _fortress;
    private readonly IModLogger _logger;
    private readonly string _startedAt = DateTime.Now.ToString("o");
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly Queue<(BasicCharacterObject Troop, FormationClass Class)> _queue = new();

    private Phase _phase = Phase.Boot;
    private int _wave;
    private int _spawnedInWave;
    private int _rowIndex;
    private float _intermissionEnds;
    private float _gateRepairAt = -1f;
    private float _nextLog;
    private double _tickMaxMs;
    private double _tickTotalMs;
    private long _ticks;
    private float _frameMax;
    private float _frameTotal;
    private long _frames;
    private Vec3 _attackerAt;
    private Vec2 _attackerDir;
    private Vec3 _defenderAt;
    private Vec2 _defenderDir;
    private float _highZ;
    private int _deaths;
    private bool _mainWasAlive;

    // A destroyed gate, tower or ram leaves ActiveMissionObjects (Mission.OnObjectDisabled), so these lists are taken once, in Boot.
    private List<CastleGate> _gates = new();
    private List<SiegeLadder> _ladders = new();
    private List<SiegeTower> _towers = new();
    private List<BatteringRam> _rams = new();

    public LongDefeatSpikeRunner(Mission mission, LongDefeatSpikeOptions options, LongDefeatSpikeFortress fortress, IModLogger logger)
    {
        _mission = mission;
        _options = options;
        _fortress = fortress;
        _logger = logger;
    }

    private void Info(string text) => _logger.LogInfo(Tag + text);

    private static string Num(float value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private static string Ms(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string At(Vec3 v) => $"({Num(v.x)}, {Num(v.y)}, {Num(v.z)})";

    public void Tick(float dt)
    {
        if (_phase == Phase.Finished)
            return;
        var start = Stopwatch.GetTimestamp();
        var logged = false;
        try
        {
            _frameMax = Math.Max(_frameMax, dt);
            _frameTotal += dt;
            _frames++;
            var now = _mission.CurrentTime;
            if (_phase == Phase.Boot)
            {
                if (now >= 0.5f)
                    Boot(now);
                return;
            }

            if (_phase == Phase.Spawning)
                SpawnSome();
            else if (_phase == Phase.Fighting)
                CheckCleared(now);
            else if (now >= _intermissionEnds)
                StartWave(now);

            if (_phase == Phase.Finished)
                return;

            if (_gateRepairAt >= 0f && now >= _gateRepairAt)
            {
                _gateRepairAt = -1f;
                RepairGate();
            }

            WatchMain();
            if (now >= _nextLog)
            {
                logged = true;
                _nextLog = now + LogSeconds;
                LogState(now);
                WriteStatus(now);
            }

            if (now >= _options.LiveLimitSeconds)
                Finish("live_limit", now);
            else if (_phase != Phase.Spawning && Count(_mission.DefenderTeam, false) == 0)
                Finish("defenders_dead", now);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} fault in phase {_phase}: {ex}");
            Finish("fault", _mission.CurrentTime);
        }
        finally
        {
            if (!logged)
            {
                var ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                _tickMaxMs = Math.Max(_tickMaxMs, ms);
                _tickTotalMs += ms;
                _ticks++;
            }
        }
    }

    private void Boot(float now)
    {
        var attackers = _mission.AttackerTeam;
        var defenders = _mission.DefenderTeam;
        Info($"[S1] teams attacker {attackers?.GetType().Name ?? "null"} ai {attackers?.TeamAI?.GetType().Name ?? "null"}; "
            + $"defender {defenders?.GetType().Name ?? "null"} ai {defenders?.TeamAI?.GetType().Name ?? "null"}; player team side {_mission.PlayerTeam?.Side}; "
            + $"mode {_mission.Mode}; siege battle {_mission.IsSiegeBattle}; deployment finished {_mission.IsDeploymentFinished}");
        if (attackers == null || defenders == null)
        {
            Finish("no_teams", now);
            return;
        }

        // A scene can hold a hidden second gate, so only visible gates count, and the outer gate comes first for S4.
        var allGates = _mission.ActiveMissionObjects.FindAllWithType<CastleGate>().ToList();
        _gates = allGates.Where(g => g.GameEntity.IsVisibleIncludeParents()).OrderByDescending(g => g.GameEntity.HasTag("outer_gate")).ToList();
        var gate = _gates.FirstOrDefault();
        _highZ = HighLine(gate);
        Info($"[S1] scene: gates {_gates.Count} visible of {allGates.Count}, first {(gate != null ? At(gate.GameEntity.GlobalPosition) : "none")} outer {gate?.GameEntity.HasTag("outer_gate")}; "
            + $"walls {_mission.ActiveMissionObjects.FindAllWithType<WallSegment>().Count()}, high line z {Num(_highZ)}; "
            + $"deployment points {Points().Count} (attacker {Points().Count(p => p.Side == BattleSideEnum.Attacker)}, defender {Points().Count(p => p.Side == BattleSideEnum.Defender)})");
        if (_options.Waves < 2)
            Info("[S1] one wave only: S4, S5 and S7 run between waves, so they do not run");

        ChooseFrames(attackers, defenders, gate);
        DeployEngines();
        SpawnGarrison(defenders);
        _mission.OnInitialSpawnCompleted();
        if (_options.DefendersAi)
            defenders.DelegateCommandToAI();
        Info($"[S1] after initial spawn: deployment finished {_mission.IsDeploymentFinished}; mode {_mission.Mode}; main agent {_mission.MainAgent?.Name?.ToString() ?? "null"} "
            + $"controller {_mission.MainAgent?.Controller}; player general {defenders.IsPlayerGeneral}; "
            + $"order owner {defenders.PlayerOrderController?.Owner?.Name?.ToString() ?? "null"}; high {High(defenders)}; z {Spread(defenders)}");
        LogEngines("S1 boot");
        StartWave(now);
    }

    private float HighLine(CastleGate? gate)
    {
        var walls = _mission.ActiveMissionObjects.FindAllWithType<WallSegment>().Where(w => w.DefenseSide != FormationAI.BehaviorSide.BehaviorSideNotSet).ToList();
        if (walls.Count > 0)
            return walls.Max(w => w.GameEntity.GlobalPosition.z) - WallMargin;
        return (gate != null ? gate.GameEntity.GlobalPosition.z : 0f) + GateToWallFallback;
    }

    private void ChooseFrames(Team attackers, Team defenders, CastleGate? gate)
    {
        var plan = true;
        try
        {
            _mission.DeploymentPlan.MakeDefaultDeploymentPlans();
        }
        catch (Exception ex)
        {
            plan = false;
            _logger.LogWarning($"{Tag}[S1] MakeDefaultDeploymentPlans threw {ex.GetType().Name}: {ex.Message}");
        }

        var attackerPoints = Points().Where(p => p.Side == BattleSideEnum.Attacker).Select(p => p.GameEntity.GlobalPosition).ToList();
        var gateAt = gate != null ? gate.GameEntity.GlobalPosition : Vec3.Zero;
        var centre = attackerPoints.Count > 0 ? Centre(attackerPoints) : gateAt;
        var outward = new Vec2(centre.x - gateAt.x, centre.y - gateAt.y);
        if (!(outward.Normalize() > 0.1f))
            outward = new Vec2(1f, 0f);

        var fallbackAttacker = centre + new Vec3(outward.x * 30f, outward.y * 30f);
        var fallbackDefender = gateAt - new Vec3(outward.x * 25f, outward.y * 25f);
        var planAttacker = plan ? PlanFrame(attackers, true) : null;
        var planDefender = plan ? PlanFrame(defenders, false) : null;
        Info($"[S1] frames: plan attacker {Describe(planAttacker)}, plan defender {Describe(planDefender)}, "
            + $"fallback attacker {At(fallbackAttacker)}, fallback defender {At(fallbackDefender)}; gate {At(gateAt)}");

        _attackerAt = planAttacker?.At ?? fallbackAttacker;
        _attackerDir = planAttacker?.Dir ?? -outward;
        _defenderAt = planDefender?.At ?? fallbackDefender;
        _defenderDir = planDefender?.Dir ?? outward;
    }

    private (Vec3 At, Vec2 Dir)? PlanFrame(Team team, bool reinforcement)
    {
        try
        {
            _mission.GetFormationSpawnFrame(team, FormationClass.Infantry, reinforcement, out var position, out var direction);
            var at = position.GetGroundVec3();
            if (!at.IsValid || at.LengthSquared < 1f)
                return null;
            return (at, direction);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag}[S1] GetFormationSpawnFrame {team.Side} threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string Describe((Vec3 At, Vec2 Dir)? frame) => frame.HasValue ? At(frame.Value.At) : "none";

    private static Vec3 Centre(List<Vec3> points)
    {
        var sum = Vec3.Zero;
        foreach (var point in points)
            sum += point;
        return sum * (1f / points.Count);
    }

    private void DeployEngines()
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

        // SiegeDeploymentHandler.RemoveDeploymentPoints disables every point at the end of deployment. Here the first defender point stays live for S5,
        // because the player buys engines later. DeploymentHandler is absent, so that point's own placeholder stays visible until S5 deploys on it.
        var defender = Points().Where(p => p.Side == BattleSideEnum.Defender).ToList();
        var buyPoint = defender.FirstOrDefault(p => p.DeployableWeapons.Any());
        foreach (var point in defender.Where(p => p != buyPoint))
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
        Info($"[S3] attacker engines deployed {attacker.Count(p => p.IsDeployed)} of {attacker.Count} points: ladders {_ladders.Count}, towers {_towers.Count}, rams {_rams.Count}; "
            + $"defender buy point {(buyPoint != null ? string.Join("/", buyPoint.DeployableWeaponTypes.Select(t => t.Name)) : "none")}");
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

    private void SpawnGarrison(Team defenders)
    {
        var player = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.Player);
        Spawn(player, defenders, true, FormationClass.Infantry, _defenderAt, _defenderDir, 0);
        SpawnDefenders(defenders, _options.Garrison);
        foreach (var cls in new[] { FormationClass.Infantry, FormationClass.Ranged })
            defenders.GetFormation(cls).SetPositioning(new WorldPosition(_mission.Scene, UIntPtr.Zero, _defenderAt, false), _defenderDir);
        Info($"[S1] garrison spawned: active {defenders.ActiveAgents.Count} at {At(_defenderAt)}");
    }

    private void SpawnDefenders(Team defenders, int count)
    {
        var infantry = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.DefenderInfantry);
        var ranged = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.DefenderRanged);
        var archers = count / 3;
        for (var i = 0; i < count; i++)
        {
            Spawn(i < archers ? ranged : infantry, defenders, true, i < archers ? FormationClass.Ranged : FormationClass.Infantry,
                _defenderAt, _defenderDir, i + 1);
        }
    }

    private Agent Spawn(BasicCharacterObject troop, Team team, bool playerSide, FormationClass cls, Vec3 at, Vec2 dir, int index)
    {
        var side = new Vec2(-dir.y, dir.x);
        var column = index % 10 - 4.5f;
        var row = index / 10;
        var x = at.x + side.x * column * 1.6f - dir.x * row * 1.6f;
        var y = at.y + side.y * column * 1.6f - dir.y * row * 1.6f;
        var ground = _mission.Scene.GetGroundHeightAtPosition(new Vec3(x, y, at.z + 2f), BodyFlags.CommonCollisionExcludeFlags);
        var z = !float.IsNaN(ground) && !float.IsInfinity(ground) && Math.Abs(ground - at.z) < 8f ? ground : at.z;
        var data = _mission.GetAgentBuildDataToSpawnTroop(new BasicBattleAgentOrigin(troop), playerSide, false, false, false, 0, -1, new Vec3(x, y, z), dir)
            .Formation(team.GetFormation(cls)).Banner(team.Banner);
        return _mission.SpawnTroopWithAgentBuildData(data, true, true);
    }

    private void StartWave(float now)
    {
        _wave++;
        _spawnedInWave = 0;
        _rowIndex = 0;
        if (_wave > 1)
            Reinforce();
        var ranged = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.AttackerRanged);
        var infantry = _fortress.AttackerInfantry.Select(id => MBObjectManager.Instance.GetObject<BasicCharacterObject>(id)).ToArray();
        for (var i = 0; i < _options.WaveSize; i++)
        {
            if (i % 4 == 3)
                _queue.Enqueue((ranged, FormationClass.Ranged));
            else
                _queue.Enqueue((infantry[i % infantry.Length], FormationClass.Infantry));
        }

        _phase = Phase.Spawning;
        Info($"[S2] wave {_wave} starts at {Num(now)} s: {_options.WaveSize} attackers at {At(_attackerAt)}");
    }

    private void Reinforce()
    {
        var defenders = _mission.DefenderTeam;
        var missing = _options.Garrison - Count(defenders, false);
        if (missing <= 0)
            return;
        SpawnDefenders(defenders, missing);
        Info($"[S2] wave {_wave}: {missing} reinforcements at {At(_defenderAt)}; defenders active {Count(defenders, false)}");
    }

    private void SpawnSome()
    {
        var team = _mission.AttackerTeam;
        for (var i = 0; i < SpawnsPerTick && _queue.Count > 0; i++)
        {
            var next = _queue.Dequeue();
            Spawn(next.Troop, team, false, next.Class, _attackerAt, _attackerDir, _rowIndex++);
            _spawnedInWave++;
        }

        if (_queue.Count > 0)
            return;
        foreach (var cls in new[] { FormationClass.Infantry, FormationClass.Ranged })
            team.GetFormation(cls).SetPositioning(new WorldPosition(_mission.Scene, UIntPtr.Zero, _attackerAt, false), _attackerDir);

        // A new tactic per wave: TacticBreachWalls latched its shock assault when it counted the first spawn batch, and ResetTactic keeps the same object. That latch also cleared
        // ForcedUse on every engine, and nothing sets it back for towers and rams. This comes before the reset, so a wave of 100 or fewer still gets vanilla's stop from the new tactic.
        foreach (var engine in _towers.Cast<SiegeWeapon>().Concat(_rams).Where(w => !w.IsDisabled))
            engine.SetForcedUse(true);
        team.ClearTacticOptions();
        team.AddTacticOption(new TacticBreachWalls(team));
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
        _phase = Phase.Fighting;
        Info($"[S2] wave {_wave}: all {_spawnedInWave} spawned; tactics {Tactics()}");
    }

    private void CheckCleared(float now)
    {
        if (Count(_mission.AttackerTeam, false) > 0)
            return;

        Info($"[S2] wave {_wave} CLEARED at {Num(now)} s: attackers on the field {_mission.AttackerTeam.ActiveAgents.Count}, fleeing {Count(_mission.AttackerTeam, true)}; "
            + $"defenders active {Count(_mission.DefenderTeam, false)}; deaths of the player so far {_deaths}");
        LogEngines("S3 wave cleared");
        if (_wave >= _options.Waves)
        {
            Finish("final", now);
            return;
        }

        _phase = Phase.Intermission;
        _intermissionEnds = now + _options.IntermissionSeconds;
        if (_wave == 1)
            RunTests(now);
    }

    private void RunTests(float now)
    {
        BuyEngine();
        ChangeGear();
        if (BreakGate())
            _gateRepairAt = now + GateRepairDelay;
    }

    /// <summary>True when the gate is broken now, by this call or by the wave.</summary>
    private bool BreakGate()
    {
        var gate = _gates.FirstOrDefault();
        var component = gate?.GameEntity.GetFirstScriptOfType<DestructableComponent>();
        if (gate == null || component == null)
        {
            Info("[S4] no gate with a destructable component");
            return false;
        }

        Info($"[S4] before: {DescribeGate(gate, component)}");
        if (component.IsDestroyed)
        {
            Info("[S4] the gate is already broken (by the wave), so the repair tests a real break");
            return true;
        }

        // MissionObjects, not the active list: only the type matters here, and a destroyed ram has left the active list.
        var ram = _mission.MissionObjects.FindAllWithType<BatteringRam>().FirstOrDefault();
        if (ram == null)
        {
            Info("[S4] no ram in the scene: an empty weapon deals 0 unless the attacker is a ram (DestructableComponent.OnHit), so the gate stays whole");
            return false;
        }

        // OnHit gives an empty weapon 0 damage unless the attacker is a ram, and a ram plays the gate's break animation.
        var weapon = MissionWeapon.Invalid;
        component.TriggerOnHit(null, (int)component.HitPoint + 1, gate.GameEntity.GlobalPosition, Vec3.Forward, in weapon, -1, ram);
        Info($"[S4] broken: {DescribeGate(gate, component)}");
        return component.IsDestroyed;
    }

    private void RepairGate()
    {
        var gate = _gates.FirstOrDefault();
        var component = gate?.GameEntity.GetFirstScriptOfType<DestructableComponent>();
        if (gate == null || component == null)
            return;

        // Mission.OnObjectDisabled disabled the gate and took it off the active list and its faces away, so the repair is vanilla's own CastleGate.OnMissionReset
        // (:341-348). OnDestroyed opened the navmesh and that reset leaves it open, so SetAutoOpenState closes it. CastleGate.ServerTick restores the plank and the standing points.
        AccessTools.Method(typeof(CastleGate), "OnMissionReset")?.Invoke(gate, null);
        component.SetAbilityOfFaces(true);
        gate.SetAutoOpenState(false);
        var state = DescribeGate(gate, component);
        Info(!component.IsDestroyed && !gate.IsDisabled ? $"[S4] repaired: {state}" : $"[S4] NOT repaired: {state}");
    }

    private string DescribeGate(CastleGate gate, DestructableComponent component) =>
        $"at {At(gate.GameEntity.GlobalPosition)} hit points {Num(component.HitPoint)}/{Num(component.MaxHitPoint)} destroyed {component.IsDestroyed} gate destroyed {gate.IsDestroyed} disabled {gate.IsDisabled} "
        + $"deactivated {gate.IsDeactivated} state {gate.State} open {gate.IsGateOpen} visible {gate.GameEntity.IsVisibleIncludeParents()} "
        + $"defender detachment {_mission.DefenderTeam.DetachmentManager.ContainsDetachment(gate)}";

    private void BuyEngine()
    {
        var point = Points().FirstOrDefault(p => p.Side == BattleSideEnum.Defender && !p.IsDisabled && !p.IsDeployed && p.DeployableWeapons.Any());
        if (point == null)
        {
            Info("[S5] no defender deployment point can take an engine");
            return;
        }

        var types = point.DeployableWeaponTypes.ToList();
        var type = types.Contains(typeof(Mangonel)) ? typeof(Mangonel) : types[0];
        point.Deploy(type);
        var weapon = point.DeployedWeapon as SiegeWeapon;
        SetVanillaHitPoints(weapon);
        weapon?.TickAuxForInit();
        var state = $"{type.Name} at {At(point.GameEntity.GlobalPosition)}: is deployed {point.IsDeployed}, weapon {weapon?.GetType().Name ?? "null"}, "
            + $"visible {weapon?.GameEntity.IsVisibleIncludeParents()}, standing points {weapon?.StandingPoints?.Count ?? 0}";
        Info(point.IsDeployed && weapon != null ? $"[S5] deployed {state}" : $"[S5] NOT deployed {state}");
    }

    private void ChangeGear()
    {
        var main = _mission.MainAgent;
        var agent = main != null && main.IsActive() ? main : _mission.DefenderTeam.ActiveAgents.FirstOrDefault(a => a.IsHuman);
        if (agent == null)
        {
            Info("[S7] no live defender");
            return;
        }

        var donor = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.AttackerInfantry[_fortress.AttackerInfantry.Length - 1]);
        var gear = donor.Equipment.Clone();
        Info($"[S7] target {(agent == main ? "the main agent" : "a soldier, because the main agent is gone")} {agent.Name}, race {agent.Character?.Race}");
        Info($"[S7] before: {DescribeGear(agent)}");
        agent.UpdateSpawnEquipmentAndRefreshVisuals(gear);
        Info($"[S7] after the swap, gear of {donor.StringId}: {DescribeGear(agent)}");

        // The swap recomputes only UpdateAgentStats. The armour values are set once, at build (CustomBattleAgentStatCalculateModel.InitializeAgentStats).
        var driven = agent.AgentDrivenProperties;
        driven.ArmorHead = gear.GetHeadArmorSum();
        driven.ArmorTorso = gear.GetHumanBodyArmorSum();
        driven.ArmorLegs = gear.GetLegArmorSum();
        driven.ArmorArms = gear.GetArmArmorSum();
        agent.UpdateCustomDrivenProperties();
        Info($"[S7] after the armour write: {DescribeGear(agent)}");
    }

    // The sums read the equipment object just assigned; the driven values are what combat uses.
    private static string DescribeGear(Agent agent) =>
        $"gear head {Num(agent.SpawnEquipment.GetHeadArmorSum())} body {Num(agent.SpawnEquipment.GetHumanBodyArmorSum())}; "
        + $"driven head {Num(agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Head))} chest {Num(agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest))} "
        + $"legs {Num(agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Legs))} arms {Num(agent.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.ArmLeft))}; "
        + $"weapon0 {agent.Equipment[EquipmentIndex.Weapon0].Item?.StringId ?? "none"} wielded {agent.WieldedWeapon.Item?.StringId ?? "none"}";

    private void WatchMain()
    {
        var alive = _mission.MainAgent != null && _mission.MainAgent.IsActive();
        if (_mainWasAlive && !alive)
        {
            _deaths++;
            Info($"[S2] the main agent is gone at {Num(_mission.CurrentTime)} s (loss {_deaths}); main agent now {_mission.MainAgent?.Name?.ToString() ?? "null"}");
        }

        _mainWasAlive = alive;
    }

    private void LogState(float now)
    {
        var attackers = _mission.AttackerTeam;
        var defenders = _mission.DefenderTeam;
        var tickMean = _ticks > 0 ? _tickTotalMs / _ticks : 0.0;
        var frameMean = _frames > 0 ? _frameTotal / _frames : 0f;
        Info($"[S2] t {Num(now)} wave {_wave} {_phase}: attackers active {Count(attackers, false)} fleeing {Count(attackers, true)} high {High(attackers)} z {Spread(attackers)}; "
            + $"defenders active {Count(defenders, false)} fleeing {Count(defenders, true)} high {High(defenders)} z {Spread(defenders)}; tactics {Tactics()}; "
            + $"run tick mean {Ms(tickMean)} ms max {Ms(_tickMaxMs)} ms; frame mean {Ms(frameMean * 1000f)} ms max {Ms(_frameMax * 1000f)} ms");
        LogEngines("S3");
        _tickMaxMs = 0.0;
        _tickTotalMs = 0.0;
        _ticks = 0;
        _frameMax = 0f;
        _frameTotal = 0f;
        _frames = 0;
    }

    private void LogEngines(string label)
    {
        var ladders = _ladders.Select(l => l.State.ToString());
        var towers = _towers.Select(t => $"{t.HasArrivedAtTarget}/{t.IsDestroyed}/{t.ForcedUse}");
        var rams = _rams.Select(r => $"{r.HasArrivedAtTarget}/{r.IsDestroyed}/{r.HasCompletedAction()}/{r.ForcedUse}");
        var gates = _gates.Select(g =>
        {
            var c = g.GameEntity.GetFirstScriptOfType<DestructableComponent>();
            return $"{Num(c?.HitPoint ?? -1f)} {g.State} destroyed {g.IsDestroyed} disabled {g.IsDisabled}";
        });
        var mine = _mission.ActiveMissionObjects.FindAllWithType<SiegeWeapon>()
            .Where(w => !w.IsDisabled && w.Side == BattleSideEnum.Defender && w.GameEntity.IsVisibleIncludeParents())
            .Select(w => $"{w.GetType().Name} users {w.UserCountNotInStruckAction}");
        var lanes = TeamAISiegeComponent.SiegeLanes?.Select(l => $"{l.LaneSide} open {l.IsOpen} breach {l.IsBreach}") ?? Enumerable.Empty<string>();
        Info($"[{label}] ladders [{string.Join(",", ladders)}] towers arrived/destroyed/forced [{string.Join(",", towers)}] rams arrived/destroyed/done/forced [{string.Join(",", rams)}] "
            + $"gates [{string.Join("; ", gates)}] lanes [{string.Join("; ", lanes)}] defender engines [{string.Join(", ", mine)}]");
    }

    private string Tactics() =>
        $"attacker {TeamTacticProbe.CurrentTacticName(_mission.AttackerTeam)}, defender {TeamTacticProbe.CurrentTacticName(_mission.DefenderTeam)}";

    private int High(Team team) => team.ActiveAgents.Count(a => a.IsHuman && a.Position.z >= _highZ);

    private static string Spread(Team team)
    {
        var heights = team.ActiveAgents.Where(a => a.IsHuman).Select(a => a.Position.z).OrderBy(z => z).ToList();
        return heights.Count == 0 ? "none" : $"{Num(heights[0])}/{Num(heights[heights.Count / 2])}/{Num(heights[heights.Count - 1])}";
    }

    private static int Count(Team team, bool fleeing)
    {
        var count = 0;
        foreach (var agent in team.ActiveAgents)
        {
            if (agent.IsHuman && agent.IsActive() && agent.IsRunningAway == fleeing)
                count++;
        }

        return count;
    }

    private List<DeploymentPoint> Points() => _mission.ActiveMissionObjects.FindAllWithType<DeploymentPoint>().ToList();

    private void WriteStatus(float now)
    {
        if (_options.OutFolder == null)
            return;
        WriteFile(StatusFile, $"phase={_phase}\nwave={_wave}\nmt={Num(now)}\nwall={Num((float)_wall.Elapsed.TotalSeconds)}\n");
    }

    private void WriteFile(string name, string text)
    {
        Directory.CreateDirectory(_options.OutFolder!);
        File.WriteAllText(System.IO.Path.Combine(_options.OutFolder!, name), text);
    }

    private void Finish(string reason, float now)
    {
        _phase = Phase.Finished;
        Info($"[S2] run over: {reason} at {Num(now)} s; waves started {_wave}; player losses {_deaths}; wall {Num((float)_wall.Elapsed.TotalSeconds)} s");
        if (_options.OutFolder == null)
            return;

        var logPath = _logger.LogFilePath;
        WriteFile(DoneFile, $"reason={reason}\nrun=1\nreport=none\n"
            + $"log={(string.IsNullOrEmpty(logPath) ? "none" : System.IO.Path.GetFullPath(logPath!))}\nlive_seconds={Num(now)}\nwall_seconds={Num((float)_wall.Elapsed.TotalSeconds)}\n"
            + $"started={_startedAt}\nended={DateTime.Now:o}\n");
        if (_options.Quit)
            TaleWorlds.Engine.Utilities.QuitGame();
    }
}
