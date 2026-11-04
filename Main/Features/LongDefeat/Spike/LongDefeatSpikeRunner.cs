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
    private const float SpawnLevelTolerance = 10f;
    private const float FramePointRadius = 1f;
    private const int FramePointLimit = 6;
    private const float StaleSeconds = 60f;
    private const float StaleAnyCountSeconds = 180f;
    private const float StrandedSeconds = 90f;
    private const int StragglerShare = 10;

    private enum Phase { Boot, Spawning, Fighting, Intermission, Finished }

    private readonly Mission _mission;
    private readonly LongDefeatSpikeOptions _options;
    private readonly LongDefeatSpikeFortress _fortress;
    private readonly IModLogger _logger;
    private readonly string _startedAt = DateTime.Now.ToString("o");
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private readonly Queue<(BasicCharacterObject Troop, FormationClass Class, int Count, int Index)> _queue = new();
    private readonly List<(FormationClass Class, Vec3 At)> _batch = new();

    private Phase _phase = Phase.Boot;
    private int _wave;
    private int _spawnedInWave;
    private float _intermissionEnds;
    private float _gateRepairAt = -1f;
    private float _nextLog;
    private double _tickMaxMs;
    private double _tickTotalMs;
    private long _ticks;
    private float _frameMax;
    private float _frameTotal;
    private long _frames;
    private float _highZ;
    private int _deaths;
    private bool _mainWasAlive;
    private int _standing;
    private float _lastLossAt;
    private bool _charging;

    // A destroyed gate, tower or ram leaves ActiveMissionObjects (Mission.OnObjectDisabled), so these lists are taken once, in Boot.
    private List<CastleGate> _gates = new();
    private List<SiegeLadder> _ladders = new();
    private List<SiegeTower> _towers = new();
    private List<BatteringRam> _rams = new();
    private SiegeWeapon? _bought;

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
        foreach (var hidden in allGates.Where(g => !_gates.Contains(g)))
            BreakHiddenGate(hidden);
        Info($"[S1] scene: gates {_gates.Count} visible of {allGates.Count}, first {(gate != null ? At(gate.GameEntity.GlobalPosition) : "none")} outer {gate?.GameEntity.HasTag("outer_gate")}; "
            + $"walls {_mission.ActiveMissionObjects.FindAllWithType<WallSegment>().Count()}, high line z {Num(_highZ)}; "
            + $"deployment points {Points().Count} (attacker {Points().Count(p => p.Side == BattleSideEnum.Attacker)}, defender {Points().Count(p => p.Side == BattleSideEnum.Defender)})");
        if (_options.Waves < 2)
            Info("[S1] one wave only: S4, S5 and S7 run between waves, so they do not run");

        if (!ChooseFrames(attackers, defenders, gate))
        {
            Finish("no_spawn_frames", now);
            return;
        }

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

    // The engine's InnerGate ignores visibility (TeamAISiegeComponent.cs:54-57), and a hidden gate never ticks (CastleGate.cs:619-622), so it never opens. Helm's Deep hides one at height 32.5
    // (scene.xscene:5971): the Middle lane stays closed (SiegeLane.cs:355-364), and BehaviorAssaultWalls (:162) never leaves AttackEntity. A null InnerGate throws in that behavior, so the gate is broken, not removed.
    private void BreakHiddenGate(CastleGate gate)
    {
        var component = gate.GameEntity.GetFirstScriptOfType<DestructableComponent>();
        var broke = component != null && TryBreak(gate, component);
        Info($"[S1] hidden gate at {At(gate.GameEntity.GlobalPosition)}: {(broke ? "broken" : "NOT broken")}, destroyed {gate.IsDestroyed} open {gate.IsGateOpen}");
    }

    private float HighLine(CastleGate? gate)
    {
        var walls = _mission.ActiveMissionObjects.FindAllWithType<WallSegment>().Where(w => w.DefenseSide != FormationAI.BehaviorSide.BehaviorSideNotSet).ToList();
        if (walls.Count > 0)
            return walls.Max(w => w.GameEntity.GlobalPosition.z) - WallMargin;
        return (gate != null ? gate.GameEntity.GlobalPosition.z : 0f) + GateToWallFallback;
    }

    // Vanilla gives the plan its troop counts first, because they set each formation's planned width and depth (DefaultBattleMissionAgentSpawnLogic.cs:675, :690-694).
    private bool ChooseFrames(Team attackers, Team defenders, CastleGate? gate)
    {
        try
        {
            // Mission.cs:1804-1807: with no MissionDeploymentPlanningLogic in the list, the mission builds this type.
            if (!(_mission.DeploymentPlan is DefaultMissionDeploymentPlan plan))
            {
                _logger.LogWarning($"{Tag}[S1] the deployment plan is a {_mission.DeploymentPlan?.GetType().Name ?? "null"}");
                return false;
            }

            var archers = _options.Garrison / 3;
            plan.AddTroops(defenders, FormationClass.Infantry, _options.Garrison - archers + 1);
            plan.AddTroops(defenders, FormationClass.Ranged, archers);
            plan.AddTroops(attackers, FormationClass.Infantry, _options.WaveSize - _options.WaveSize / 4, 0, true);
            plan.AddTroops(attackers, FormationClass.Ranged, _options.WaveSize / 4, 0, true);
            plan.MakeDefaultDeploymentPlans();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag}[S1] the deployment plan threw {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        var attackerFoot = PlanFrame(attackers, FormationClass.Infantry, true);
        var attackerArchers = PlanFrame(attackers, FormationClass.Ranged, true);
        var defenderFoot = PlanFrame(defenders, FormationClass.Infantry, false);
        var defenderArchers = PlanFrame(defenders, FormationClass.Ranged, false);
        var extraFoot = PlanFrame(defenders, FormationClass.Infantry, true);
        var extraArchers = PlanFrame(defenders, FormationClass.Ranged, true);
        Info($"[S1] frames: attackers foot {Describe(attackerFoot)}, archers {Describe(attackerArchers)}; defenders foot {Describe(defenderFoot)}, archers {Describe(defenderArchers)}; "
            + $"reinforcements foot {Describe(extraFoot)}, archers {Describe(extraArchers)}; gate {(gate != null ? At(gate.GameEntity.GlobalPosition) : "none")}");
        return attackerFoot != null && attackerArchers != null && defenderFoot != null && defenderArchers != null && extraFoot != null && extraArchers != null;
    }

    private Vec3? PlanFrame(Team team, FormationClass cls, bool reinforcement)
    {
        try
        {
            _mission.GetFormationSpawnFrame(team, cls, reinforcement, out var position, out _);
            var at = position.GetGroundVec3();
            if (!at.IsValid || at.LengthSquared < 1f)
                return null;
            return at;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag}[S1] GetFormationSpawnFrame {team.Side} threw {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static string Describe(Vec3? frame) => frame.HasValue ? At(frame.Value) : "none";

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
        var archers = _options.Garrison / 3;
        var foot = _options.Garrison - archers + 1;
        PlaceFormation(defenders, FormationClass.Infantry, foot, false);
        PlaceFormation(defenders, FormationClass.Ranged, archers, false);
        var player = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.Player);
        SpawnDefenders(defenders, _options.Garrison, false, 1);
        Spawn(player, defenders, true, FormationClass.Infantry, false, foot, 0);
        defenders.GetFormation(FormationClass.Infantry).EndSpawn();
        defenders.GetFormation(FormationClass.Ranged).EndSpawn();
        Info($"[S1] garrison spawned: active {defenders.ActiveAgents.Count}");
        LogSpawnLevel(defenders, false, "defenders");
    }

    private void SpawnDefenders(Team defenders, int count, bool reinforcement, int firstFootIndex)
    {
        var infantry = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.DefenderInfantry);
        var ranged = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.DefenderRanged);
        var archers = count / 3;
        var foot = count - archers;
        for (var i = 0; i < archers; i++)
            Spawn(ranged, defenders, true, FormationClass.Ranged, reinforcement, archers, i);
        for (var i = 0; i < foot; i++)
            Spawn(infantry, defenders, true, FormationClass.Infantry, reinforcement, firstFootIndex + foot, firstFootIndex + i);
    }

    // Vanilla's order, MissionBattleSideSpawnContext.cs:371-372. EndSpawn comes after the last soldier of the batch. Vanilla does this only for a formation with no place yet
    // (:369), so Reinforce skips it. A new wave does it again on purpose: the last wave's formation is empty, and the frame is the place the wave must start from.
    private void PlaceFormation(Team team, FormationClass cls, int count, bool reinforcement)
    {
        var formation = team.GetFormation(cls);
        formation.BeginSpawn(count, false);
        _mission.SetFormationPositioningFromDeploymentPlan(formation, reinforcement);
    }

    // No position here. The engine then takes the place from the formation's layout, which offers only slots that native accepts around the frame (Formation.cs:669-671,
    // LineFormation.cs:495-497), and Mission.GetTroopSpawnFrameWithIndex (:3983-3995) handles a slot with no navmesh. A position computed here skips all of that:
    // at Helm's Deep it put most of the garrison in the caves under the fortress on the first in-game run.
    private void Spawn(BasicCharacterObject troop, Team team, bool playerSide, FormationClass cls, bool reinforcement, int count, int index)
    {
        var data = _mission.GetAgentBuildDataToSpawnTroop(new BasicBattleAgentOrigin(troop), playerSide, true, false, reinforcement, count, index, null, null, formationIndex: cls)
            .Banner(team.Banner);
        var agent = _mission.SpawnTroopWithAgentBuildData(data, true, true);
        _batch.Add((cls, agent.Position));
    }

    // Reads back where the engine put this batch, so a wrong floor is a failed line and needs no person to see it. "ok" means near the height the scene authored for that class and
    // on a navmesh. It does not mean on the walls: a scene can author a class's frame on low ground. A pile is its own line: when the layout runs out of slots, the remaining soldiers
    // all stand on the frame point (Mission.cs:3992-3995). The limit stays above a healthy formation: a shield wall at spacing 0 can put 5 soldiers within 1 m of the frame.
    private void LogSpawnLevel(Team team, bool reinforcement, string who)
    {
        var parts = new List<string>();
        var piles = new List<string>();
        var failed = _batch.Count == 0;
        foreach (var group in _batch.GroupBy(soldier => soldier.Class))
        {
            _mission.GetFormationSpawnFrame(team, group.Key, reinforcement, out var position, out _);
            var frame = position.GetGroundVec3();
            var far = 0;
            var offMesh = 0;
            var onPoint = 0;
            foreach (var soldier in group)
            {
                var at = soldier.At;
                if (!(Math.Abs(at.z - frame.z) <= SpawnLevelTolerance))
                    far++;
                if (_mission.Scene.GetNavigationMeshForPosition(in at) == UIntPtr.Zero)
                    offMesh++;
                if (at.AsVec2.Distance(frame.AsVec2) <= FramePointRadius)
                    onPoint++;
            }

            failed |= far > 0 || offMesh > 0;
            if (onPoint > FramePointLimit)
                piles.Add($"{group.Key} {onPoint} of {group.Count()}");
            parts.Add($"{group.Key} {group.Count()} at frame {At(frame)}: far {far}, off the navmesh {offMesh}, on the frame point {onPoint}, z {Spread(group.Select(soldier => soldier.At.z))}");
        }

        Info((failed ? "[S1] spawn level WRONG: " : "[S1] spawn level ok: ") + $"{who} {_batch.Count}; {string.Join("; ", parts)}");
        if (piles.Count > 0)
            Info($"[S1] spawn pile: {who} {string.Join(", ", piles)} on the frame point");
        _batch.Clear();
    }

    private void StartWave(float now)
    {
        _wave++;
        _spawnedInWave = 0;
        if (_wave > 1)
            Reinforce();
        var ranged = MBObjectManager.Instance.GetObject<BasicCharacterObject>(_fortress.AttackerRanged);
        var infantry = _fortress.AttackerInfantry.Select(id => MBObjectManager.Instance.GetObject<BasicCharacterObject>(id)).ToArray();
        var rangedCount = _options.WaveSize / 4;
        var infantryCount = _options.WaveSize - rangedCount;
        PlaceFormation(_mission.AttackerTeam, FormationClass.Infantry, infantryCount, true);
        PlaceFormation(_mission.AttackerTeam, FormationClass.Ranged, rangedCount, true);

        // One class after the other, as vanilla does (MissionBattleSideSpawnContext.cs:318-394): each change of class rebuilds the layout (Formation.cs:3109-3112).
        for (var i = 0; i < rangedCount; i++)
            _queue.Enqueue((ranged, FormationClass.Ranged, rangedCount, i));
        for (var i = 0; i < infantryCount; i++)
            _queue.Enqueue((infantry[i % infantry.Length], FormationClass.Infantry, infantryCount, i));

        _phase = Phase.Spawning;
        Info($"[S2] wave {_wave} starts at {Num(now)} s: {_options.WaveSize} attackers");
    }

    private void Reinforce()
    {
        var defenders = _mission.DefenderTeam;
        var missing = _options.Garrison - Count(defenders, false);
        if (missing <= 0)
            return;
        SpawnDefenders(defenders, missing, true, 0);
        Info($"[S2] wave {_wave}: {missing} reinforcements; defenders active {Count(defenders, false)}");
        LogSpawnLevel(defenders, true, "reinforcements");
    }

    private void SpawnSome()
    {
        var team = _mission.AttackerTeam;
        for (var i = 0; i < SpawnsPerTick && _queue.Count > 0; i++)
        {
            var next = _queue.Dequeue();
            Spawn(next.Troop, team, false, next.Class, true, next.Count, next.Index);
            _spawnedInWave++;
        }

        if (_queue.Count > 0)
            return;
        team.GetFormation(FormationClass.Infantry).EndSpawn();
        team.GetFormation(FormationClass.Ranged).EndSpawn();
        LogSpawnLevel(team, true, "attackers");

        // A new tactic per wave: TacticBreachWalls latched its shock assault when it counted the first spawn batch, and ResetTactic keeps the same object. That latch also cleared
        // ForcedUse on every engine, and nothing sets it back for towers and rams. This comes before the reset, so a wave of 100 or fewer still gets vanilla's stop from the new tactic.
        foreach (var engine in _towers.Cast<SiegeWeapon>().Concat(_rams).Concat(_bought != null ? new[] { _bought } : Array.Empty<SiegeWeapon>()).Where(w => !w.IsDisabled))
            engine.SetForcedUse(true);
        team.ClearTacticOptions();
        team.AddTacticOption(new TacticBreachWalls(team));
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
        _standing = -1;
        _lastLossAt = _mission.CurrentTime;
        _phase = Phase.Fighting;
        Info($"[S2] wave {_wave}: all {_spawnedInWave} spawned; tactics {Tactics()}");
    }

    private void CheckCleared(float now)
    {
        var attackers = Count(_mission.AttackerTeam, false);
        if (attackers > 0)
        {
            WatchStalemate(now, attackers);
            return;
        }

        Info($"[S2] wave {_wave} CLEARED at {Num(now)} s: attackers on the field {_mission.AttackerTeam.ActiveAgents.Count}, fleeing {Count(_mission.AttackerTeam, true)}; "
            + $"defenders active {Count(_mission.DefenderTeam, false)}; deaths of the player so far {_deaths}");
        LogEngines("S3 wave cleared");
        EndCharge();
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

    // No wave retreats, and only BattleEndLogic ends a vanilla siege (BattleEndLogic.cs:198-222, 279-361), which this mission leaves out. On the first unattended run the last 7 attackers, then 6,
    // and 31 defenders stood apart until the limit at 1,500 s. So both teams get TacticCharge after a quiet spell, and the stragglers that nobody reaches after that fall.
    private void WatchStalemate(float now, int attackers)
    {
        var defenders = Count(_mission.DefenderTeam, false);
        if (attackers + defenders != _standing)
        {
            _standing = attackers + defenders;
            _lastLossAt = now;
            return;
        }

        var quiet = now - _lastLossAt;
        if (_charging)
        {
            if (quiet >= StrandedSeconds)
            {
                // A clear must be earned: at Minas Tirith this rule once felled 35 attackers that had all but won. Only stragglers fall; more than that and the fight has no result.
                var left = _mission.AttackerTeam.ActiveAgents.Where(a => a.IsHuman && !a.IsRunningAway).ToList();
                if (attackers * StragglerShare > _options.WaveSize || attackers >= defenders)
                {
                    Info($"[S2] wave {_wave}: no loss for {Num(quiet)} s after the charge, with {attackers} attackers at {Places(left)} and {defenders} defenders; no result; {DescribeFormations(_mission.AttackerTeam)} | {DescribeFormations(_mission.DefenderTeam)}");
                    Finish("stalemate", now);
                    return;
                }

                Info($"[S2] wave {_wave}: nobody reached the last {left.Count} attackers at {Num(now)} s, at {Places(left)}; they fall; {DescribeFormations(_mission.AttackerTeam)} | {DescribeFormations(_mission.DefenderTeam)}");
                foreach (var agent in left)
                    _mission.KillAgentCheat(agent);
                _lastLossAt = now;
            }

            return;
        }

        if (quiet < (attackers * 2 <= _options.WaveSize ? StaleSeconds : StaleAnyCountSeconds))
            return;

        Info($"[S2] wave {_wave} stale at {Num(now)} s after {Num(quiet)} s with no loss: attackers {attackers} at {Places(_mission.AttackerTeam.ActiveAgents.Where(a => a.IsHuman))}; "
            + $"defenders {defenders} z {Spread(_mission.DefenderTeam)}; both teams get TacticCharge, and a formation under a player's orders does not move; {DescribeFormations(_mission.AttackerTeam)} | {DescribeFormations(_mission.DefenderTeam)}");
        Charge(_mission.AttackerTeam);
        Charge(_mission.DefenderTeam);
        _charging = true;
        _lastLossAt = now;
    }

    private static void Charge(Team team)
    {
        team.ClearTacticOptions();
        team.AddTacticOption(new TacticCharge(team));
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
    }

    // The next wave's own tactic comes when its queue empties (SpawnSome), so only the defenders need theirs back here.
    private void EndCharge()
    {
        if (!_charging)
            return;
        _charging = false;
        var team = _mission.DefenderTeam;
        team.ClearTacticOptions();
        team.AddTacticOption(new TacticDefendCastle(team));
        team.TeamAI.ResetTactic(keepCurrentTactic: false);
        Info($"[S2] wave {_wave}: the defenders return to their siege tactic; tactics {Tactics()}");
    }

    private static string Places(IEnumerable<Agent> agents) => string.Join(" ", agents.Take(8).Select(a => At(a.Position)));

    // TacticCharge only weights BehaviorCharge, which returns 0 until the walls test passes (BehaviorCharge.cs:169-201), so this says what each formation really does.
    private static string DescribeFormations(Team team) => team.Side + ": " + string.Join(", ", team.FormationsIncludingEmpty.Where(f => f.CountOfUnits > 0)
        .Select(f => $"{f.FormationIndex} {f.CountOfUnits} {f.AI.ActiveBehavior?.GetType().Name ?? "no behavior"} side {f.AI.Side} order {f.GetReadonlyMovementOrderReference().OrderEnum} ai {f.IsAIControlled} inside {f.QuerySystem.InsideCastleUnitCountPositioned}"));

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
        if (!TryBreak(gate, component))
        {
            Info("[S4] no ram in the scene: an empty weapon deals 0 unless the attacker is a ram (DestructableComponent.OnHit), so the gate stays whole");
            return false;
        }

        Info($"[S4] broken: {DescribeGate(gate, component)}");
        return component.IsDestroyed;
    }

    // OnHit gives an empty weapon 0 damage unless the attacker is a ram, and a ram plays the gate's break animation.
    private bool TryBreak(CastleGate gate, DestructableComponent component)
    {
        var ram = _mission.MissionObjects.FindAllWithType<BatteringRam>().FirstOrDefault();
        if (ram == null)
            return false;
        var weapon = MissionWeapon.Invalid;
        component.TriggerOnHit(null, (int)component.HitPoint + 1, gate.GameEntity.GlobalPosition, Vec3.Forward, in weapon, -1, ram);
        return true;
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
        _bought = weapon;
        weapon?.SetForcedUse(true);
        var state = $"{type.Name} at {At(point.GameEntity.GlobalPosition)}: is deployed {point.IsDeployed}, weapon {weapon?.GetType().Name ?? "null"}, "
            + $"visible {weapon?.GameEntity.IsVisibleIncludeParents()}, standing points {weapon?.StandingPoints?.Count ?? 0}; {DescribeBought()}";
        Info(point.IsDeployed && weapon != null ? $"[S5] deployed {state}" : $"[S5] NOT deployed {state}");
    }

    // The first run read visible False for a deployed engine, and the visible flag lives on the engine or on a parent (DeploymentPoint.ToggleWeaponVisibility), so this names each level.
    private string DescribeBought()
    {
        if (_bought == null)
            return "bought none";
        var levels = new List<string>();
        for (var entity = _bought.GameEntity; entity.IsValid; entity = entity.Parent)
            levels.Add($"{entity.Name} {entity.GetVisibilityExcludeParents()}");
        return $"bought {_bought.GetType().Name} disabled {_bought.IsDisabled} side {_bought.Side} users {_bought.UserCountNotInStruckAction} forced {_bought.ForcedUse} user formations {_bought.UserFormations.Count} "
            + $"walking to it {_bought.StandingPoints.Count(p => p.HasAIMovingTo)} own visibility up the parents [{string.Join(", ", levels)}]";
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
            + $"gates [{string.Join("; ", gates)}] lanes [{string.Join("; ", lanes)}] defender engines [{string.Join(", ", mine)}]; {DescribeBought()}");
    }

    private string Tactics() =>
        $"attacker {TeamTacticProbe.CurrentTacticName(_mission.AttackerTeam)}, defender {TeamTacticProbe.CurrentTacticName(_mission.DefenderTeam)}";

    private int High(Team team) => team.ActiveAgents.Count(a => a.IsHuman && a.Position.z >= _highZ);

    private static string Spread(Team team) => Spread(team.ActiveAgents.Where(a => a.IsHuman).Select(a => a.Position.z));

    private static string Spread(IEnumerable<float> values)
    {
        var heights = values.OrderBy(z => z).ToList();
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
