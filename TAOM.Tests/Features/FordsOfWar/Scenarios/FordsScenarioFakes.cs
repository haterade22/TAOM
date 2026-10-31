using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Scenarios;

/// <summary>
/// A battlefield that behaves the way the real adapter does for the things the verdict rules read: a spawn lands in the lane's
/// formation at the spot it was given, units walk toward their formation's order at a fixed speed (each keeping its place in the
/// block it spawned in), melee units strike the nearest enemy, and a killed unit is reported through the engine's removal callback.
/// Every fault the scenarios inject is a public field set before the match starts.
/// </summary>
internal sealed class FaithfulBattlefield : IFordsBattlefieldAdapter
{
    internal enum OrderKind
    {
        Idle,
        Move,
        Charge,
        AttackTower,
    }

    internal sealed class Unit
    {
        public AgentKey Key;
        public object Handle = new();
        public MatchSide Side;
        public int Lane;
        public string Troop = "";
        public Vec3f Position;
        public Vec3f Facing;
        public float Forward;
        public float Across;
        public float HitPoints = 30f;
        public double StrikeCooldown;
        public bool Alive = true;

        /// <summary>True once the real adapter's registry would hold the unit: every spawn registers it, and only registered units are sampled.</summary>
        public bool Registered;
    }

    private sealed class Formation
    {
        public OrderKind Kind;
        public Vec3f Target;
        public string TowerId = "";
        public Vec3f? Anchor;
    }

    internal const float Speed = 4f;
    internal const float RunSpeed = 12f;
    internal const float CatchUpGap = 15f;
    internal const float MeleeRange = 4f;
    internal const float MeleePerSecond = 10f;
    internal const float TowerReach = 8f;

    private readonly Dictionary<AgentKey, Unit> _units = new();
    private readonly Dictionary<object, Unit> _byHandle = new();
    private readonly Dictionary<(MatchSide, int), Formation> _formations = new();
    private readonly Dictionary<object, MatchSide> _gunners = new();
    private int _nextKey;

    public Func<string, MatchSide, int, bool>? SpawnFails;
    public Func<string, bool>? MissingCharacter;
    public Func<MatchSide, int, Vec3f, bool>? RefusesMove;
    public bool Frozen;
    public bool KillerUnknown;
    public bool DoubleSpawns;
    public int ReportedMaxAgents;

    /// <summary>Set by the scenario: the engine's removal callback, which the mission logic forwards to the director.</summary>
    public Action<object, object?, bool>? OnRemoved;

    /// <summary>Set by the scenario: a melee unit struck a tower entity (the engine's hit callback).</summary>
    public Action<string, object, int>? OnTowerStruck;

    /// <summary>Set by the scenario: where a tower entity stands, null when it is gone.</summary>
    public Func<string, Vec3f?>? TowerPosition;

    public int MeleeRawDamage = 6;
    public int Deaths;

    public int MaxAgents => ReportedMaxAgents;

    public int LiveUnits => _units.Values.Count(u => u.Alive);

    public int GunnerCount => _gunners.Count;

    public IEnumerable<Unit> Alive => _units.Values.Where(u => u.Alive);

    public Unit? Find(AgentKey key) => _units.TryGetValue(key, out var unit) && unit.Alive ? unit : null;

    public Unit? FirstHero(string troopId) => Alive.FirstOrDefault(u => u.Troop == troopId);

    public void RegisterGunner(object gunner, MatchSide side) => _gunners[gunner] = side;

    public void ForgetGunners(IEnumerable<object> gunners)
    {
        foreach (var gunner in gunners)
            _gunners.Remove(gunner);
    }

    public bool Prepare(int laneCount) => true;

    public void SetCorpseFadeOutSeconds(float seconds)
    {
    }

    public void CompleteDeployment()
    {
    }

    public FordsSpawnedAgent? Spawn(string troopId, MatchSide side, int lane, Vec3f position, Vec3f facing)
    {
        if (SpawnFails?.Invoke(troopId, side, lane) == true)
            return null;

        var formation = FormationOf(side, lane);
        formation.Anchor ??= position;
        var anchor = formation.Anchor.Value;
        var across = new Vec3f(-facing.Y, facing.X, 0f);
        var shift = position - anchor;
        var unit = Create(troopId, side, lane, position, facing, shift.X * facing.X + shift.Y * facing.Y, shift.X * across.X + shift.Y * across.Y);
        if (DoubleSpawns)
            Create(troopId, side, lane, position, facing, unit.Forward, unit.Across);
        return new FordsSpawnedAgent(unit.Key, 10, false);
    }

    private Unit Create(string troopId, MatchSide side, int lane, Vec3f position, Vec3f facing, float forward, float across)
    {
        var unit = new Unit
        {
            Key = new AgentKey(++_nextKey), Side = side, Lane = lane, Troop = troopId, Position = position, Facing = facing,
            Forward = forward, Across = across, Registered = true,
        };
        _units[unit.Key] = unit;
        _byHandle[unit.Handle] = unit;
        return unit;
    }

    public IReadOnlyList<FordsUnitSample> SampleUnits() =>
        Alive.Where(u => u.Registered).Select(u => new FordsUnitSample(u.Key, u.Side, u.Lane, u.Position)).ToList();

    public Vec3f? PositionOf(AgentKey key) => Find(key)?.Position;

    public bool TryGetKey(object? agent, out AgentKey key)
    {
        key = default;
        if (agent == null || !_byHandle.TryGetValue(agent, out var unit) || !unit.Alive)
            return false;

        key = unit.Key;
        return true;
    }

    public MatchSide? SideOf(object? agent)
    {
        if (agent == null)
            return null;
        if (_byHandle.TryGetValue(agent, out var unit))
            return unit.Side;
        return _gunners.TryGetValue(agent, out var side) ? side : null;
    }

    public bool TryRemoveUnit(object affected, object? affector, bool killed, out AgentKey victim, out AgentKey? killer)
    {
        victim = default;
        killer = null;
        if (!_byHandle.TryGetValue(affected, out var unit))
            return false;

        victim = unit.Key;
        if (killed && !KillerUnknown && affector != null && _byHandle.TryGetValue(affector, out var by))
            killer = by.Key;
        _byHandle.Remove(affected);
        return true;
    }

    public bool Move(MatchSide side, int lane, Vec3f target)
    {
        if (RefusesMove?.Invoke(side, lane, target) == true)
            return false;

        var formation = FormationOf(side, lane);
        formation.Kind = OrderKind.Move;
        formation.Target = target;
        return true;
    }

    public void Charge(MatchSide side, int lane) => FormationOf(side, lane).Kind = OrderKind.Charge;

    public bool AttackTower(MatchSide side, int lane, string towerId)
    {
        if (TowerPosition?.Invoke(towerId) == null)
            return false;

        var formation = FormationOf(side, lane);
        formation.Kind = OrderKind.AttackTower;
        formation.TowerId = towerId;
        return true;
    }

    public string CharacterName(string characterId) => characterId;

    public bool CharacterExists(string characterId) => MissingCharacter?.Invoke(characterId) != true;

    public void Clear()
    {
        _units.Clear();
        _byHandle.Clear();
        _formations.Clear();
        _gunners.Clear();
    }

    private Formation FormationOf(MatchSide side, int lane)
    {
        if (!_formations.TryGetValue((side, lane), out var formation))
            _formations[(side, lane)] = formation = new Formation();
        return formation;
    }

    /// <summary>One engine step: every unit walks, melee units strike, the dead are reported.</summary>
    public void Advance(float dt)
    {
        if (!(dt > 0f))
            return;

        var living = Alive.OrderBy(u => u.Key.Value).ToList();
        if (!Frozen)
        {
            foreach (var unit in living)
                Walk(unit, dt, living);
        }

        var damage = new Dictionary<Unit, float>();
        var credit = new Dictionary<Unit, Unit>();
        foreach (var unit in living)
        {
            var enemy = Nearest(unit, living);
            if (enemy == null || Distance(unit, enemy) > MeleeRange)
                continue;

            damage[enemy] = (damage.TryGetValue(enemy, out var sum) ? sum : 0f) + MeleePerSecond * dt;
            if (!credit.ContainsKey(enemy))
                credit[enemy] = unit;
        }

        foreach (var pair in damage)
        {
            pair.Key.HitPoints -= pair.Value;
            if (pair.Key.HitPoints <= 0f)
                Kill(pair.Key, credit[pair.Key]);
        }

        StrikeTowers(dt, living);
    }

    /// <summary>Kills a unit the way the engine does: the unit leaves the mission and the removal callback runs.</summary>
    public void Kill(Unit victim, Unit? by)
    {
        if (!victim.Alive)
            return;

        victim.Alive = false;
        Deaths++;
        OnRemoved?.Invoke(victim.Handle, by?.Handle, true);
    }

    private void Walk(Unit unit, float dt, List<Unit> living)
    {
        var formation = FormationOf(unit.Side, unit.Lane);
        Vec3f? destination = null;
        var stop = 0.2f;
        switch (formation.Kind)
        {
            case OrderKind.Move:
                var across = new Vec3f(-unit.Facing.Y, unit.Facing.X, 0f);
                destination = formation.Target - unit.Facing * unit.Forward + across * unit.Across;
                break;
            case OrderKind.Charge:
                destination = Nearest(unit, living)?.Position;
                stop = 1.5f;
                break;
            case OrderKind.AttackTower:
                destination = TowerPosition?.Invoke(formation.TowerId);
                stop = TowerReach - 2f;
                break;
        }

        if (destination is not { } to)
            return;

        var gap = unit.Position.Distance2D(to);
        if (gap <= stop)
            return;

        var step = Math.Min((gap > CatchUpGap ? RunSpeed : Speed) * dt, gap - stop);
        var dx = (to.X - unit.Position.X) / gap;
        var dy = (to.Y - unit.Position.Y) / gap;
        unit.Position = new Vec3f(unit.Position.X + dx * step, unit.Position.Y + dy * step, unit.Position.Z);
    }

    private void StrikeTowers(float dt, List<Unit> living)
    {
        foreach (var unit in living.Where(u => u.Alive))
        {
            var formation = FormationOf(unit.Side, unit.Lane);
            if (formation.Kind != OrderKind.AttackTower || TowerPosition?.Invoke(formation.TowerId) is not { } at)
                continue;

            unit.StrikeCooldown -= dt;
            if (unit.StrikeCooldown > 0.0 || unit.Position.Distance2D(at) > TowerReach)
                continue;

            unit.StrikeCooldown = 1.5;
            OnTowerStruck?.Invoke(formation.TowerId, unit.Handle, MeleeRawDamage);
        }
    }

    private static Unit? Nearest(Unit unit, List<Unit> living)
    {
        Unit? best = null;
        var bestSquared = float.MaxValue;
        foreach (var other in living)
        {
            if (!other.Alive || other.Side == unit.Side || other.Lane != unit.Lane)
                continue;

            var dx = other.Position.X - unit.Position.X;
            var dy = other.Position.Y - unit.Position.Y;
            var squared = dx * dx + dy * dy;
            if (squared < bestSquared)
            {
                bestSquared = squared;
                best = other;
            }
        }

        return best;
    }

    private static float Distance(Unit a, Unit b) => a.Position.Distance2D(b.Position);
}

/// <summary>
/// The towers as the engine holds them: each entity has maximum and current hit points that only the damage the rules applied (and
/// the heals they asked for) change, falls at zero through the destroyed handler, and a gunner garrison that fires when it lives.
/// </summary>
internal sealed class FaithfulTowers : IFordsTowerAdapter
{
    internal sealed class Entity
    {
        public TowerSlot Slot = null!;
        public float Max;
        public float Hp;
        public bool Destroyed;
        public object Component = new();
        public List<object> Gunners = new();
    }

    private readonly Dictionary<string, Entity> _entities = new();
    private readonly FaithfulBattlefield _field;
    private Action<string>? _destroyed;

    public Func<TowerSlot, bool>? BuildFails;
    public FordsVolleyItemInfo? Item = new("western_javelin_2_t3", 30, 40f, 80f);
    public bool NeverFires;
    public bool TakesRawDamage;
    public int Missiles;

    /// <summary>Hits the engine would report for the missiles fired since the last drain.</summary>
    public List<FordsVolleyHit> PendingHits { get; } = new();

    public FaithfulTowers(FaithfulBattlefield field) => _field = field;

    public IEnumerable<Entity> Entities => _entities.Values;

    public Entity Of(string id) => _entities[id];

    public Vec3f? PositionOf(string id) => _entities.TryGetValue(id, out var e) && !e.Destroyed ? e.Slot.Position : null;

    public bool SpawnTower(TowerSlot slot, TowerPrefab prefab, int hitPoints)
    {
        if (BuildFails?.Invoke(slot) == true)
            return false;

        _entities[slot.Id] = new Entity { Slot = slot, Max = hitPoints, Hp = hitPoints };
        return true;
    }

    public void Heal(string towerId, int amount)
    {
        if (_entities.TryGetValue(towerId, out var e) && !e.Destroyed)
            e.Hp = Math.Min(e.Max, e.Hp + amount);
    }

    public void SetDestroyedHandler(Action<string> onDestroyed) => _destroyed = onDestroyed;

    public int SpawnGunners(TowerSlot slot, string troopId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var gunner = new object();
            _entities[slot.Id].Gunners.Add(gunner);
            _field.RegisterGunner(gunner, slot.Owner);
        }

        return count;
    }

    public void KillGunners(string towerId)
    {
        if (!_entities.TryGetValue(towerId, out var e))
            return;

        _field.ForgetGunners(e.Gunners);
        e.Gunners.Clear();
    }

    public int Fire(string towerId, Vec3f target, int missiles)
    {
        if (NeverFires || Item == null || !_entities.TryGetValue(towerId, out var e) || e.Gunners.Count == 0)
            return 0;

        Missiles += missiles;
        for (var i = 0; i < missiles; i++)
            PendingHits.Add(HitFor(Item));
        return missiles;
    }

    /// <summary>A hit that agrees with the item and the damage formula N x multiplier x (impact speed / S) squared.</summary>
    private static FordsVolleyHit HitFor(FordsVolleyItemInfo item)
    {
        const float multiplier = 1f;
        var impact = item.Speed * 0.9f;
        var magnitude = item.Thrust * multiplier * 0.9f * 0.9f;
        return new FordsVolleyHit(magnitude, (int)(magnitude * 0.8f), magnitude * 0.2f, item.Thrust, item.Speed, impact, multiplier, "Torso",
            false, false, true);
    }

    public string? TowerIdOf(object? component)
    {
        if (component == null)
            return null;
        foreach (var pair in _entities)
        {
            if (ReferenceEquals(pair.Value.Component, component))
                return pair.Key;
        }

        return null;
    }

    public FordsVolleyItemInfo? VolleyItemInfo() => Item;

    public float? HitPoints(string towerId) => _entities.TryGetValue(towerId, out var e) ? e.Hp : null;

    public void Clear() => _entities.Clear();

    /// <summary>The engine applied <paramref name="applied"/> damage the hook returned; a tower at zero falls and reports itself once.</summary>
    public void Damage(string towerId, int applied, int raw)
    {
        if (!_entities.TryGetValue(towerId, out var e) || e.Destroyed)
            return;

        e.Hp -= TakesRawDamage ? raw : applied;
        if (e.Hp > 0f)
            return;

        e.Hp = 0f;
        e.Destroyed = true;
        _destroyed?.Invoke(towerId);
    }
}

/// <summary>
/// The scene: bases at (0,0) and (1000,0), a rectangular boundary, and the real layout fitter over a walkable test, so a tower
/// stands on ground the fitter would drop when <see cref="Blocked"/> covers it.
/// </summary>
internal sealed class FaithfulScene : IFordsSceneAdapter
{
    public bool HasBoundary = true;
    public List<Vec3f> Blocked { get; } = new();

    public IReadOnlyList<Vec3f> Polygon { get; } = new List<Vec3f>
    {
        new(0f, -150f, 0f), new(1000f, -150f, 0f), new(1000f, 150f, 0f), new(0f, 150f, 0f),
    };

    public bool TryGetBasePoints(float insetFraction, out Vec3f westBase, out Vec3f eastBase)
    {
        westBase = new Vec3f(0f, 0f, 0f);
        eastBase = new Vec3f(1000f, 0f, 0f);
        return HasBoundary;
    }

    public IReadOnlyList<Vec3f> BoundaryPolygon() => Polygon;

    public FordsLayoutFit Fit(MapLayout layout) =>
        FordsLayoutFitter.Fit(layout, p => p, p => !Blocked.Any(b => p.Distance2D(b) < 60f));

    public bool TryReadTaggedLayout(string sceneName, CampDefaults camp, out MapLayout layout, out string problem)
    {
        layout = null!;
        problem = "no tags in the test scene";
        return false;
    }
}

/// <summary>
/// A warg rider and the formation it belongs to, for the warg rage log's seam: the rider walks back to the formation's rear rank
/// (<see cref="Depth"/> behind the front-middle the distance is measured to) once it has the formation again, unless <see cref="Stuck"/>.
/// The rider comes from behind the formation, so it never passes the front-middle on the way, and the depth is wider than the log's 15 m base margin, so a return check that drops the depth term fails here.
/// </summary>
internal sealed class FaithfulRider
{
    public object Formation = new();
    public bool Active = true;
    public bool HasMount = true;
    public bool HasFormation = true;
    public bool Stuck;
    public float X = -200f;
    public float FormationX;
    public float Width = 20f;
    public float Depth = 20f;

    public float Distance => Math.Abs(X - FormationX);

    public void Advance(float dt)
    {
        if (!Active || !HasFormation || Stuck)
            return;

        var gap = FormationX - Depth - X;
        X += Math.Sign(gap) * Math.Min(6f * dt, Math.Abs(gap));
    }
}
