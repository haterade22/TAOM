using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Collections;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Spider;
using BehaviorTreeWrapper;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TAOM.Features.CreatureBandits.Diagnostics.CreatureDiagFormat;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// The main-thread half of the Creature Bandits diagnostics (#692), driven from the diagnostics behavior's
/// OnMissionTick. Drains the callback queue into lines; every second samples who targets each creature (native
/// reads, so main thread only, each held handle gated by <see cref="AgentSlotIdentity.IsCurrentOccupant"/>), how
/// many hostile soldiers stand near it and what they aim at, its nearest enemy, speed, travel, contact stall and tree
/// branch; every five seconds checks stuck, writes state transitions and a 30-second vitals heartbeat per creature,
/// one aggregate snap line, the battle-end accounting per side and the hostile formations' view of the creatures,
/// each only on change. The summary at mission end always prints once any creature was attempted: zero counts are
/// data. Temporary, strip after sign-off.
/// </summary>
internal sealed class CreatureBanditDiagTicker
{
    private const float SampleSeconds = 1f;
    private const float SnapSeconds = 5f;
    private const float HeartbeatSeconds = 30f;
    private const float AttackWindowSeconds = 1.5f;
    private const float NearSoldierMeters = 10f;
    private const float SideStallSeconds = 10f;
    private const float RecentRemovalSeconds = 10f;
    private const int FormationLineCap = 60;

    private readonly Dictionary<Agent, int> _live = new(ReferenceIdentity.Instance);
    private readonly Dictionary<int, int> _targetedBy = new();
    private readonly Dictionary<int, int> _immediateBy = new();

    // The sample's creature table, as parallel lists: the census loop does no per-pair dictionary lookup.
    private readonly List<Agent> _creatures = new();
    private readonly List<CreatureDiagRecord> _records = new();
    private readonly List<Vec3> _positions = new();
    private readonly List<int> _teamSlot = new();
    private readonly List<float> _nearest = new();
    private readonly List<Team> _creatureTeams = new();
    private bool[] _hostile = new bool[4];

    private readonly Dictionary<string, string> _formationKeys = new();
    private readonly float[] _sideZeroSince = { float.NaN, float.NaN };
    private float _nextSample;
    private float _nextSnap;
    private int _lastSnapTargeted;
    private float _lastRemovalTime = float.NaN;
    private string _lastSidesKey = "-";
    private bool _stallWarned;
    private int _formationLines;

    internal string Result = "-";

    internal void Tick(Mission mission)
    {
        var ledger = CreatureBanditDiag.Ledger;
        if (ledger.Count == 0 && CreatureBanditDiag.Events.IsEmpty) return;
        MissionThreadGuard.MarkMainThread();

        float now = mission.CurrentTime;
        if (Drain(writeLines: true))
        {
            _lastRemovalTime = now;
            WriteSides(mission, now);
        }
        if (now >= _nextSample)
        {
            _nextSample = now + SampleSeconds;
            Sample(mission, now);
        }
        if (now >= _nextSnap)
        {
            _nextSnap = now + SnapSeconds;
            Snap(mission, now);
        }
    }

    /// <summary>
    /// Applies every queued removal to its record (fate, end time, killer), and writes the lines when asked. Returns
    /// whether a removal was drained. The summary runs it state-only after a ticker failure.
    /// </summary>
    private static bool Drain(bool writeLines)
    {
        bool removal = false;
        int budget = CreatureBanditDiag.Events.Count;
        while (budget-- > 0 && CreatureBanditDiag.Events.TryDequeue(out var e))
        {
            var record = CreatureBanditDiag.Ledger.Get(e.Serial);
            if (e.Kind == CreatureDiagEventKind.Removed)
            {
                removal = true;
                if (record != null) ApplyRemoval(record, e);
            }
            if (e.Kind == CreatureDiagEventKind.HitTaken && !e.ByPlayer && record != null && float.IsNaN(record.FirstHitTime))
            {
                record.FirstHitTime = e.Time;
                record.FirstHitKind = e.Missile ? "missile" : e.Charge ? "charge" : "melee";
            }
            if (writeLines) WriteEvent(e, record);
        }
        return removal;
    }

    private static void ApplyRemoval(CreatureDiagRecord record, in CreatureDiagEvent e)
    {
        record.Fate = e.Detail;
        record.EndTime = e.Time;
        record.Killer = e.OtherTroop;
        // Close an open stuck episode at death: the snap that would close it skips inactive creatures.
        if (record.Stuck && e.Time >= record.StuckSince) record.StuckSeconds += e.Time - record.StuckSince;
        record.Stuck = false;
    }

    private static void WriteEvent(in CreatureDiagEvent e, CreatureDiagRecord? record)
    {
        string thread = I(e.ThreadId) + (e.OnMain ? "/main" : "/off");
        switch (e.Kind)
        {
            case CreatureDiagEventKind.HitTaken:
                CreatureBanditDiag.Write(e.Serial, "hit-taken", Line("hit-taken", e.Time, e.Serial,
                    "by", e.OtherTroop, "byName", Name(e.OtherName), "byIdx", I(e.OtherIndex),
                    "byPlayer", B(e.ByPlayer), "byMount", B(e.OtherIsMount), "aimed", B(e.Aimed), "missile", B(e.Missile),
                    "charge", B(e.Charge), "weaponClass", I(e.WeaponClass), "dmg", I(e.Damage),
                    "blocked", B(e.Blocked), "hpAfter", F(e.HealthAfter), "thread", thread));
                break;
            case CreatureDiagEventKind.BiteLanded:
                CreatureBanditDiag.Write(e.Serial, "bite", Line(e.Detail == "native" ? "native-hit" : "bite", e.Time, e.Serial,
                    "on", e.OtherTroop, "onName", Name(e.OtherName), "onIdx", I(e.OtherIndex),
                    "onPlayer", B(e.ByPlayer), "onMount", B(e.OtherIsMount), "dmg", I(e.Damage), "charge", B(e.Charge),
                    "blocked", B(e.Blocked), "victimHpAfter", F(e.HealthAfter), "thread", thread));
                break;
            case CreatureDiagEventKind.KilledByCreature:
                CreatureBanditDiag.Write(e.Serial, "kill", Line("kill", e.Time, e.Serial,
                    "victim", e.OtherTroop, "victimName", Name(e.OtherName), "state", e.Detail, "thread", thread));
                break;
            case CreatureDiagEventKind.Removed:
                CreatureBanditDiag.Write(e.Serial, "removed", Line("removed", e.Time, e.Serial,
                    "state", e.Detail, "by", e.OtherTroop, "byName", Name(e.OtherName), "byPlayer", B(e.ByPlayer),
                    "blowDmg", I(e.Damage), "missile", B(e.Missile), "weaponClass", I(e.WeaponClass),
                    "hp", F(e.HealthAfter), "lifetime", F(record == null ? float.NaN : e.Time - record.SpawnTime),
                    "thread", thread));
                break;
            case CreatureDiagEventKind.Backstopped:
                CreatureBanditDiag.Write(e.Serial, "backstop", Line("backstop", e.Time, e.Serial,
                    "note", "routed with no attacker; counted via Origin.SetRouted", "thread", thread), warning: true);
                break;
            case CreatureDiagEventKind.Mounted:
                CreatureBanditDiag.Write(e.Serial, "outcome", Line("mounted", e.Time, e.Serial,
                    "rider", e.OtherTroop, "riderName", Name(e.OtherName), "riderIdx", I(e.OtherIndex),
                    "riderIsPlayer", B(e.ByPlayer), "note", "a soldier rides the creature; the creature-bandit rules stop applying",
                    "thread", thread), warning: true);
                break;
            case CreatureDiagEventKind.Panicked:
            case CreatureDiagEventKind.Fled:
            case CreatureDiagEventKind.Deleted:
                CreatureBanditDiag.Write(e.Serial, "outcome", Line(e.Kind.ToString().ToLowerInvariant(), e.Time, e.Serial,
                    "detail", e.Detail, "thread", thread), warning: e.Kind != CreatureDiagEventKind.Deleted);
                break;
            default:
                CreatureBanditDiag.Write(e.Serial, "event", Line(e.Kind.ToString().ToLowerInvariant(), e.Time, e.Serial,
                    "detail", e.Detail, "thread", thread));
                break;
        }
    }

    private void Sample(Mission mission, float now)
    {
        _live.Clear();
        _targetedBy.Clear();
        _immediateBy.Clear();
        _creatures.Clear();
        _records.Clear();
        _positions.Clear();
        _teamSlot.Clear();
        _nearest.Clear();
        _creatureTeams.Clear();
        CreatureBanditDiag.SoldiersAimingAtCreatures.Clear();

        foreach (var pair in CreatureBanditDiag.Creatures)
        {
            var agent = pair.Key;
            var record = CreatureBanditDiag.Ledger.Get(pair.Value);
            if (record == null || !agent.IsActive()) continue;
            if (!AgentSlotIdentity.IsCurrentOccupant(agent))
            {
                // Tripwire: an active handle whose slot changed hands. Once per creature; it drops out of sampling.
                if (record.OccupantLost) continue;
                record.OccupantLost = true;
                CreatureBanditDiag.Write(record.Serial, "outcome", Line("occupant-lost", now, record.Serial,
                    "idx", I(record.AgentIndex), "note", "handle active but its slot has a new occupant; sampling stops"),
                    warning: true);
                continue;
            }
            _live[agent] = pair.Value;
            _creatures.Add(agent);
            _records.Add(record);
            _positions.Add(agent.Position);
            _nearest.Add(float.MaxValue);
            _teamSlot.Add(TeamSlot(agent.Team));
        }
        if (_creatures.Count == 0) return;

        foreach (Team team in mission.Teams)
        {
            if (!ResolveHostility(team)) continue;
            foreach (Agent soldier in team.ActiveAgents)
            {
                if (!soldier.IsHuman || !soldier.IsActive()) continue;
                Agent? target = soldier.GetTargetAgent();
                Agent? immediate = soldier.ImmediateEnemy;
                bool aims = false;
                if (target != null && _live.TryGetValue(target, out int targeted))
                {
                    Bump(_targetedBy, targeted);
                    aims = true;
                    NoteFirstTargeted(targeted, soldier, target, now);
                }
                if (immediate != null && _live.TryGetValue(immediate, out int engaged)) { Bump(_immediateBy, engaged); aims = true; }
                if (aims) CreatureBanditDiag.SoldiersAimingAtCreatures[soldier] = 0;

                Vec3 position = soldier.Position;
                for (int i = 0; i < _creatures.Count; i++)
                {
                    int slot = _teamSlot[i];
                    if (slot < 0 || !_hostile[slot]) continue;
                    float distance = position.Distance(_positions[i]);
                    if (distance < _nearest[i]) _nearest[i] = distance;
                    if (distance > NearSoldierMeters) continue;

                    // The census denominator: of the hostile soldiers near this creature, how many aim at it.
                    var record = _records[i];
                    var creature = _creatures[i];
                    record.NearSoldierSamples++;
                    if (target == creature || immediate == creature) record.NearTargetingIt++;
                    else if (target != null || immediate != null) record.NearTargetingOther++;
                    else record.NearTargetNull++;
                }
            }
        }

        for (int i = 0; i < _creatures.Count; i++)
        {
            var record = _records[i];
            var agent = _creatures[i];
            _targetedBy.TryGetValue(record.Serial, out int targetedCount);
            record.TargetedBySamples++;
            record.TargetedBySum += targetedCount;
            if (targetedCount > record.TargetedByMax) record.TargetedByMax = targetedCount;
            _immediateBy.TryGetValue(record.Serial, out int immediateCount);
            record.ImmediateBySum += immediateCount;
            float nearest = _nearest[i] == float.MaxValue ? float.NaN : _nearest[i];
            if (nearest < record.MinEnemyDistance) record.MinEnemyDistance = nearest;
            record.LastNearestEnemy = nearest;

            var position = _positions[i];
            if (!float.IsNaN(record.LastSampleX))
                record.DistanceTravelled += (float)Math.Sqrt((position.x - record.LastSampleX) * (position.x - record.LastSampleX)
                                                            + (position.y - record.LastSampleY) * (position.y - record.LastSampleY));
            record.LastSampleX = position.x;
            record.LastSampleY = position.y;
            float speed = agent.MovementVelocity.Length;
            if (speed > record.MaxSpeed) record.MaxSpeed = speed;

            bool attacking = now - record.LastAttackTime < AttackWindowSeconds;
            if (CreatureDiagLedger.IsStalledInContact(speed, record.LastTargetDistance, SpiderConfig.StrikeRadius, attacking))
            {
                record.StalledInContactSeconds += SampleSeconds;
                if (record.LastTargetDistance < record.MinContactDistance) record.MinContactDistance = record.LastTargetDistance;
            }

            string node = NodeName(agent);
            record.SecondsInNode.TryGetValue(node, out float seconds);
            record.SecondsInNode[node] = seconds + SampleSeconds;
            record.LastNode = node;
        }
    }

    /// <summary>Route A's first proof per creature: a soldier's own native target selection picked it.</summary>
    private static void NoteFirstTargeted(int serial, Agent soldier, Agent creature, float now)
    {
        var record = CreatureBanditDiag.Ledger.Get(serial);
        if (record == null || record.FirstTargetedLogged) return;
        record.FirstTargetedLogged = true;
        CreatureBanditDiag.Write(serial, "outcome", Line("first-targeted", now, serial,
            "by", soldier.Character?.StringId ?? "-", "byName", Name(soldier.Name), "ranged", B(soldier.IsRangedCached),
            "dist", F(soldier.Position.Distance(creature.Position)),
            "sinceUnmount", F(float.IsNaN(record.UnmountTime) ? float.NaN : now - record.UnmountTime),
            "routeA", record.RouteA));
    }

    /// <summary>
    /// Route A's survival breadcrumbs, per creature: alive 5 s after the unmount, and alive 1 s after its first hit from
    /// a soldier (the native hit reaction and the managed blow pass both ran without faulting).
    /// </summary>
    private static void CheckRouteAMilestones(Agent agent, CreatureDiagRecord record, float now, int targeted)
    {
        if (!record.AliveLogged && !float.IsNaN(record.UnmountTime) && now - record.UnmountTime >= 5f)
        {
            record.AliveLogged = true;
            CreatureBanditDiag.Write(record.Serial, "outcome", Line("routeA-alive", now, record.Serial,
                "sinceUnmount", F(now - record.UnmountTime), "isMount", B(agent.IsMount),
                "nearestEnemy", F(record.LastNearestEnemy), "targetedBy", I(targeted), "hp", F(agent.Health)));
        }
        if (!record.FirstHitSurvivedLogged && !float.IsNaN(record.FirstHitTime) && now - record.FirstHitTime >= 1f)
        {
            record.FirstHitSurvivedLogged = true;
            CreatureBanditDiag.Write(record.Serial, "outcome", Line("first-hit-survived", now, record.Serial,
                "hitKind", record.FirstHitKind, "sinceHit", F(now - record.FirstHitTime), "hp", F(agent.Health),
                "hpMax", F(agent.HealthLimit), "routeA", record.RouteA));
        }
    }

    /// <summary>The creature team's slot in this sample's table; -1 for no team.</summary>
    private int TeamSlot(Team? team)
    {
        if (team == null) return -1;
        for (int i = 0; i < _creatureTeams.Count; i++)
            if (ReferenceEquals(_creatureTeams[i], team)) return i;
        _creatureTeams.Add(team);
        return _creatureTeams.Count - 1;
    }

    /// <summary>
    /// Fills <see cref="_hostile"/> for <paramref name="team"/> against each creature team: one native IsEnemyOf per
    /// team pair, never per soldier. True when the team is hostile to any creature.
    /// </summary>
    private bool ResolveHostility(Team team)
    {
        if (_hostile.Length < _creatureTeams.Count) _hostile = new bool[_creatureTeams.Count];
        bool any = false;
        for (int k = 0; k < _creatureTeams.Count; k++)
        {
            _hostile[k] = !ReferenceEquals(team, _creatureTeams[k]) && team.IsEnemyOf(_creatureTeams[k]);
            any |= _hostile[k];
        }
        return any;
    }

    private void Snap(Mission mission, float now)
    {
        int alive = 0, targetedNow = 0;
        long dealt = 0, taken = 0;
        foreach (var creature in _live)
        {
            var record = CreatureBanditDiag.Ledger.Get(creature.Value);
            var agent = creature.Key;
            if (record == null || !agent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(agent)) continue;
            alive++;
            _targetedBy.TryGetValue(record.Serial, out int targeted);
            targetedNow += targeted;
            CheckState(mission, agent, record, now, targeted);
            CheckRouteAMilestones(agent, record, now, targeted);
        }
        foreach (var record in CreatureBanditDiag.Ledger.Records)
        {
            dealt += record.DamageDealt;
            taken += record.DamageTaken;
        }

        bool recentRemoval = now - _lastRemovalTime < RecentRemovalSeconds;
        if (alive > 0 || recentRemoval)
        {
            WriteSides(mission, now);
            WriteFormations(mission, now);
        }

        if (alive == 0 && _lastSnapTargeted == 0 && targetedNow == 0 && CreatureBanditDiag.Ledger.Records.All(r => r.Fate != "alive")) return;
        _lastSnapTargeted = targetedNow;
        CreatureBanditDiag.Write(0, "snap", Line("snap", now, 0, "alive", I(alive), "known", I(CreatureBanditDiag.Ledger.Count),
            "targetedByNow", I(targetedNow), "aimingSoldiers", I(CreatureBanditDiag.SoldiersAimingAtCreatures.Count),
            "dmgDealt", I(dealt), "dmgTaken", I(taken),
            "queue", I(CreatureBanditDiag.Events.Count), "lines", I(CreatureBanditDiag.Ledger.MissionLines),
            "suppressed", I(CreatureBanditDiag.Ledger.MissionSuppressed)));
    }

    private void CheckState(Mission mission, Agent agent, CreatureDiagRecord record, float now, int targeted)
    {
        bool attacking = now - record.LastAttackTime < AttackWindowSeconds;
        float moved = float.IsNaN(record.SnapX) ? float.NaN
            : (float)Math.Sqrt((agent.Position.x - record.SnapX) * (agent.Position.x - record.SnapX)
                               + (agent.Position.y - record.SnapY) * (agent.Position.y - record.SnapY));
        record.SnapX = agent.Position.x;
        record.SnapY = agent.Position.y;
        // Stuck is judged against the engage gate's reach (what must be reached for a bite to start), not the strike.
        bool stuck = CreatureDiagLedger.IsStuck(!float.IsNaN(record.LastTargetDistance), record.LastTargetDistance,
            SpiderConfig.BiteAttackRange, attacking, moved);
        if (stuck && !record.Stuck) { record.StuckEpisodes++; record.StuckSince = now; }
        if (!stuck && record.Stuck && !float.IsNaN(record.StuckSince)) record.StuckSeconds += now - record.StuckSince;
        record.Stuck = stuck;

        var ai = agent.CommonAIComponent;
        var tree = agent.GetBehaviorTree();
        int band = CreatureDiagLedger.HpBand(agent.Health, agent.HealthLimit);
        bool aiControlled = agent.IsAIControlled;
        bool fingerprint = Hooks.CreatureBanditAgents.Is(agent);
        bool rider = agent.RiderAgent != null;
        var scripted = agent.GetScriptedFlags();
        bool proximity = mission.IsAgentInProximityMap(agent);
        string key = string.Join("|", I(band), B(agent.IsRetreating()), B(agent.IsRunningAway), B(agent.IsFadingOut()),
            agent.CurrentWatchState.ToString(), I((long)agent.GetAgentFlags()), B(tree?.IsRunning ?? false),
            B(ai?.IsPanicked ?? false), B(ai?.IsRetreating ?? false), B(stuck), B(agent.Team != null),
            B(aiControlled), B(fingerprint), B(rider), I((long)scripted), B(proximity));
        bool heartbeat = now - record.LastHeartbeat >= HeartbeatSeconds;
        if (key == record.LastStateKey && !heartbeat) return;

        string kind = key == record.LastStateKey ? "vitals" : "state";
        record.LastStateKey = key;
        record.LastHeartbeat = now;
        record.HpBand = band;
        bool alarming = agent.IsRunningAway || (ai?.IsPanicked ?? false) || !fingerprint || rider || !aiControlled;
        CreatureBanditDiag.Write(record.Serial, kind, Line(kind, now, record.Serial,
            "hp", F(agent.Health), "hpMax", F(agent.HealthLimit), "hpBand", I(band),
            "pos", F(agent.Position.x) + "," + F(agent.Position.y), "speed", F(agent.MovementVelocity.Length),
            "node", record.LastNode, "treeRunning", B(tree?.IsRunning ?? false),
            "targetDist", F(record.LastTargetDistance), "nearestEnemy", F(record.LastNearestEnemy),
            "targetedBy", I(targeted), "nearTargetingIt", I(record.NearTargetingIt) + "/" + I(record.NearSoldierSamples),
            "stuck", B(stuck), "moved5s", F(moved), "stalledInContact", F(record.StalledInContactSeconds),
            "retreating", B(agent.IsRetreating()), "runningAway", B(agent.IsRunningAway), "fading", B(agent.IsFadingOut()),
            "panicked", B(ai?.IsPanicked ?? false), "aiRetreating", B(ai?.IsRetreating ?? false),
            "morale", F(ai?.Morale ?? float.NaN), "watch", agent.CurrentWatchState.ToString(),
            "aiControlled", B(aiControlled), "fingerprint", B(fingerprint), "rider", B(rider),
            "scripted", scripted.ToString().Replace(", ", "+"), "proximityMap", B(proximity),
            "flags", agent.GetAgentFlags().ToString().Replace(", ", "+"), "team", agent.Team?.Side.ToString() ?? "none",
            "action0", agent.GetCurrentAction(0).GetName() ?? "-"), warning: kind == "state" && alarming);
    }

    /// <summary>
    /// Battle-end accounting (main thread): per side, the live humans and creatures and whether the spawn logic calls
    /// the side depleted, which is what ends the battle. Written only when the side's depletion, emptiness or creature
    /// count changes, so its volume is bounded by the creature count. One WARNING per mission when a side stays empty
    /// but undepleted, which is a battle that cannot end (or reinforcements still pending).
    /// </summary>
    private void WriteSides(Mission mission, float now)
    {
        var spawnLogic = mission.GetMissionBehavior<IMissionAgentSpawnLogic>();
        CountSide(mission, BattleSideEnum.Attacker, out int attackerLive, out int attackerCreatures);
        CountSide(mission, BattleSideEnum.Defender, out int defenderLive, out int defenderCreatures);
        bool attackerDepleted = spawnLogic?.IsSideDepleted(BattleSideEnum.Attacker) ?? false;
        bool defenderDepleted = spawnLogic?.IsSideDepleted(BattleSideEnum.Defender) ?? false;

        CheckStall(0, "Attacker", attackerLive, attackerDepleted, now);
        CheckStall(1, "Defender", defenderLive, defenderDepleted, now);

        string key = string.Join("|", B(attackerDepleted), B(attackerLive == 0), I(attackerCreatures),
            B(defenderDepleted), B(defenderLive == 0), I(defenderCreatures));
        if (key == _lastSidesKey) return;
        _lastSidesKey = key;
        CreatureBanditDiag.Write(0, "sides", Line("sides", now, 0, "spawnLogic", spawnLogic?.GetType().Name ?? "none",
            "attackerLive", I(attackerLive), "attackerCreatures", I(attackerCreatures), "attackerDepleted", B(attackerDepleted),
            "defenderLive", I(defenderLive), "defenderCreatures", I(defenderCreatures), "defenderDepleted", B(defenderDepleted)));
    }

    /// <summary>Live humans plus live creature bandits on the side's teams (loose horses are not combatants).</summary>
    private void CountSide(Mission mission, BattleSideEnum side, out int live, out int creatures)
    {
        live = 0;
        creatures = 0;
        foreach (Team team in mission.Teams)
        {
            if (team.Side != side) continue;
            foreach (Agent agent in team.ActiveAgents)
            {
                if (!agent.IsActive()) continue;
                if (_live.ContainsKey(agent)) { creatures++; live++; }
                else if (agent.IsHuman) live++;
            }
        }
    }

    private void CheckStall(int index, string side, int live, bool depleted, float now)
    {
        if (live > 0 || depleted) { _sideZeroSince[index] = float.NaN; return; }
        if (float.IsNaN(_sideZeroSince[index])) _sideZeroSince[index] = now;
        if (_stallWarned || !CreatureDiagLedger.IsSideStalled(live, depleted, _sideZeroSince[index], now, SideStallSeconds)) return;
        _stallWarned = true;
        CreatureBanditDiag.Write(0, "sides", Line("side-stall", now, 0, "side", side,
            "emptyFor", F(now - _sideZeroSince[index]),
            "note", "no live agent but not depleted: the battle cannot end unless reinforcements are pending"), warning: true);
    }

    /// <summary>
    /// How each formation hostile to a creature sees the fight (main thread, every 5 s while a creature is relevant):
    /// its behavior, order, whether its closest enemy agent is a creature, whether it sees no large enemy formation
    /// (creatures join no formation), and its team's enemy unit count. One line per formation, only on change, capped.
    /// </summary>
    private void WriteFormations(Mission mission, float now)
    {
        if (_formationLines >= FormationLineCap || _creatureTeams.Count == 0) return;
        foreach (Team team in mission.Teams)
        {
            if (!ResolveHostility(team)) continue;
            int enemyUnits = team.QuerySystem?.EnemyUnitCount ?? -1;
            foreach (Formation formation in team.FormationsIncludingSpecialAndEmpty)
            {
                if (formation.CountOfUnits == 0) continue;
                var query = formation.QuerySystem;
                var closest = query?.ClosestEnemyAgentReadOnly;
                string behavior = formation.AI?.ActiveBehavior?.GetType().Name ?? "-";
                string order = formation.GetReadonlyMovementOrderReference().OrderType.ToString();
                bool closestIsCreature = closest != null && _live.ContainsKey(closest);
                bool noLargeEnemy = query?.ClosestSignificantlyLargeEnemyFormationReadOnly == null;
                string key = string.Join("|", behavior, order, B(formation.IsAIControlled), B(closestIsCreature),
                    B(noLargeEnemy), I(enemyUnits));
                string id = I(team.TeamIndex) + "/" + formation.FormationIndex;
                if (_formationKeys.TryGetValue(id, out var last) && last == key) continue;
                _formationKeys[id] = key;

                if (++_formationLines > FormationLineCap) return;
                CreatureBanditDiag.Write(0, "formation", Line("formation", now, 0, "team", team.Side.ToString(),
                    "teamIdx", I(team.TeamIndex), "formation", formation.FormationIndex.ToString(),
                    "units", I(formation.CountOfUnits), "aiControlled", B(formation.IsAIControlled), "behavior", behavior,
                    "order", order, "closestEnemyIsCreature", B(closestIsCreature), "noLargeEnemyFormation", B(noLargeEnemy),
                    "enemyUnits", I(enemyUnits), "lines", I(_formationLines) + "/" + I(FormationLineCap)));
            }
        }
    }

    private static string NodeName(Agent agent)
    {
        var tree = agent.GetBehaviorTree();
        if (tree == null) return "no-tree";
        if (!tree.IsRunning) return "tree-stopped";
        var node = tree.CurrentNode;
        if (node == null) return "-";
        // Control nodes (selectors, sequences) are named in the tree; a task is labelled by its branch and type.
        if (node is BehaviorTrees.Nodes.BTControlNode control) return control.Name;
        return ((node.Parent as BehaviorTrees.Nodes.BTControlNode)?.Name ?? "?") + "/" + node.GetType().Name;
    }

    private static void Bump(Dictionary<int, int> counts, int serial)
    {
        counts.TryGetValue(serial, out int value);
        counts[serial] = value + 1;
    }

    /// <summary>
    /// Main thread, when the battle result is decided and again at mission end (<paramref name="phase"/> says which),
    /// so a game closed at the victory screen still leaves the totals on disk. Applies the removals still queued first
    /// (the last frames, or everything since a ticker failure, when no line is written for them). Leaves the records
    /// as it found them, so the second call reports the same totals plus what happened in between. Reads no engine
    /// state beyond the mission clock. Always prints once a creature spawn was attempted.
    /// </summary>
    internal void WriteSummary(Mission mission, bool tickerFailed, string phase)
    {
        var logger = CreatureBanditDiag.Logger;
        var ledger = CreatureBanditDiag.Ledger;
        if (logger == null || (ledger.Count == 0 && CreatureBanditDiag.SpawnsAttempted == 0 && CreatureBanditDiag.SpawnsDeclined == 0)) return;
        float now = mission.CurrentTime;
        Drain(writeLines: !tickerFailed);

        foreach (var r in ledger.Records)
        {
            float end = float.IsNaN(r.EndTime) ? now : r.EndTime;
            float stuckSeconds = r.Stuck && !float.IsNaN(r.StuckSince) ? r.StuckSeconds + end - r.StuckSince : r.StuckSeconds;
            string nodes = string.Join(",", r.SecondsInNode.OrderByDescending(p => p.Value).Select(p => p.Key.Replace(' ', '_') + ":" + F(p.Value)));
            logger.LogInfo(Line("creature-final", now, r.Serial, "phase", phase, "routeA", r.RouteA, "troop", r.TroopId, "idx", I(r.AgentIndex), "fate", r.Fate,
                "killer", r.Killer, "lifetime", F(end - r.SpawnTime), "hitsTaken", I(r.HitsTaken), "dmgTaken", I(r.DamageTaken),
                "aimedHits", I(r.AimedHitsTaken), "missileHits", I(r.MissileHitsTaken), "meleeHits", I(r.MeleeHitsTaken),
                "chargeHits", I(r.ChargeHitsTaken), "playerHits", I(r.PlayerHitsTaken), "blockedHits", I(r.BlockedHitsTaken),
                "attacks", I(r.AttacksFired), "whiffs", I(r.Whiffs), "enemiesStruck", I(r.EnemiesStruck),
                "alliesInArc", I(r.AlliesInArc), "bitesLanded", I(r.BitesLanded), "dmgDealt", I(r.DamageDealt),
                "nativeHits", I(r.NativeHitsDealt), "nativeDmg", I(r.NativeDamageDealt), "kills", I(r.Kills),
                "onHitPanicBlocked", I(r.OnHitPanicBlocked), "moralePanicBlocked", I(r.MoralePanicBlocked),
                "routBlocked", I(r.RoutBlocked), "panicked", I(r.Panicked), "fled", I(r.Fled),
                "guardPrimary", I(r.GuardPrimary), "guardOffhand", I(r.GuardOffhand), "guardMissile", I(r.GuardMissileRange),
                "suppressedLines", I(r.SuppressedLines)));
            logger.LogInfo(Line("creature-final-ai", now, r.Serial, "phase", phase, "engageChecks", I(r.EngageChecks),
                "engagePasses", I(r.EngagePasses), "engageRejRange", I(r.EngageRejectRange), "engageRejCone", I(r.EngageRejectCone),
                "engageScanEmpty", I(r.EngageScanEmpty), "engageMidAttack", I(r.EngageMidAttack),
                "bestMissDist", F(r.EngageBestMissDistance), "bestMissAngle", F(r.EngageBestMissAngle),
                "facingSkew", F(r.EngageFacingSkew), "targetChanges", I(r.TargetChanges),
                "targetedByAvg", F(r.TargetedBySamples == 0 ? 0f : (float)r.TargetedBySum / r.TargetedBySamples),
                "targetedByMax", I(r.TargetedByMax), "immediateBySum", I(r.ImmediateBySum),
                "nearSoldierSamples", I(r.NearSoldierSamples), "nearTargetingIt", I(r.NearTargetingIt),
                "nearTargetingOther", I(r.NearTargetingOther), "nearTargetNull", I(r.NearTargetNull),
                "minEnemyDist", F(r.MinEnemyDistance == float.MaxValue ? float.NaN : r.MinEnemyDistance),
                "travelled", F(r.DistanceTravelled), "maxSpeed", F(r.MaxSpeed), "stuckEpisodes", I(r.StuckEpisodes),
                "stuckSeconds", F(stuckSeconds), "stalledInContactSeconds", F(r.StalledInContactSeconds),
                "minStalledDist", F(r.MinContactDistance == float.MaxValue ? float.NaN : r.MinContactDistance),
                "staleHandleExits", I(r.StaleHandleExits), "occupantLost", B(r.OccupantLost), "nodes", nodes));
        }

        string threads = string.Join(",", CreatureBanditDiag.CallbackThreads.OrderBy(p => p.Key)
            .Select(p => p.Key + ":" + I(p.Value[0]) + "main/" + I(p.Value[1]) + "off"));
        logger.LogInfo(Line("summary", now, 0, "phase", phase, "creatures", I(ledger.Count), "spawnsAttempted", I(CreatureBanditDiag.SpawnsAttempted),
            "spawnsBuilt", I(CreatureBanditDiag.SpawnsBuilt), "fallbacks", I(CreatureBanditDiag.SpawnFallbacks),
            "declined", I(CreatureBanditDiag.SpawnsDeclined), "backstops", I(CreatureBanditDiag.BackstopCount),
            "customBattleMountRefusals", I(CreatureBanditDiag.MountRefusals), "routeAOn", I(CreatureBanditDiag.RouteAOn),
            "routeASkipped", I(CreatureBanditDiag.RouteASkipped), "hookSites", I(CreatureBanditDiag.CreationHookSites),
            "lastSides", _lastSidesKey,
            "sideStall", B(_stallWarned), "formationLines", I(_formationLines), "lines", I(ledger.MissionLines),
            "suppressed", I(ledger.MissionSuppressed), "offThreadCallsTotal", I(MissionThreadGuard.OffThreadCalls),
            "tickerFailed", B(tickerFailed), "callbackThreads", threads.Length == 0 ? "-" : threads, "result", Result));
    }
}
