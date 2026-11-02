using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The towers and keeps of a Fords of War mission (<see cref="IFordsTowerAdapter"/>): the destructible entities, their garrisons
/// and their volleys. A tower is an instance of a prefab that carries a <see cref="DestructableComponent"/> (the scene's own
/// marked entity when a tagged scene placed one). The component's hit points mirror the rules; the tower hit patch decides the
/// damage before the component sees it. The component-to-id map is read from the hit patch's thread, so it takes a lock; every
/// other member runs on the main thread.
/// </summary>
internal sealed class FordsTowerAdapter : IFordsTowerAdapter
{
    /// <summary>
    /// The Lowland Javelin, a tier 3 <c>CraftedItem</c> (SandBoxCore weapons.xml, template <c>Javelin</c>). <c>Mission.AddCustomMissile</c>
    /// adds no damage bonus, so the missile hits with its own item's damage; a thrown weapon is the one kind the engine fires as the item
    /// itself with no bonus (<c>OnAgentShootMissile</c>), so a javelin hits as a thrown javelin does.
    /// </summary>
    internal const string VolleyItemId = "western_javelin_2_t3";
    internal const string TowerArrowItemId = "fow_tower_arrow";

    private const float VolleySpread = 1.5f;
    private const string FallParticle = "psys_game_wooden_barricade_destruction";
    private const string FallSound = "event:/mission/siege/siegetower/break";
    private const float DefaultFallHeight = 10f;

    private sealed class Tower
    {
        public Tower(TowerSlot slot, GameEntity entity, DestructableComponent component)
        {
            Slot = slot;
            Entity = entity;
            Component = component;
        }

        public TowerSlot Slot { get; }
        public GameEntity Entity { get; }
        public DestructableComponent Component { get; }
        public List<Agent> Gunners { get; } = new();
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private const float GarrisonHoldMetres = 1.5f;
    private const float GarrisonHoldHeight = 2f;

    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private readonly float _fireRange;
    private readonly object _gate = new();
    private readonly Dictionary<string, Tower> _towers = new();
    private readonly Dictionary<object, string> _byComponent = new(ReferenceComparer.Instance);
    private readonly Dictionary<object, string> _byGunner = new(ReferenceComparer.Instance);
    private readonly Random _spread = new(12345);
    private Action<string>? _onDestroyed;
    private ItemObject? _volleyItem;
    private float _volleySpeed;
    private ItemObject? _arrowItem;
    private bool _arrowLookedUp;
    private float _arrowSpeed;
    private int _volleyThrust;
    private bool _volleyLookedUp;

    public FordsTowerAdapter(Mission mission, IModLogger logger, float fireRange)
    {
        _mission = mission;
        _logger = logger;
        _fireRange = fireRange;
    }

    /// <summary>The tower's entity, for the battlefield adapter's attack order. Null for an unknown or a destroyed tower.</summary>
    internal object? EntityOf(string towerId)
    {
        lock (_gate)
            return _towers.TryGetValue(towerId, out var tower) && !tower.Component.IsDestroyed ? tower.Entity : null;
    }

    public void SetDestroyedHandler(Action<string> onDestroyed) => _onDestroyed = onDestroyed;

    public bool SpawnTower(TowerSlot slot, TowerPrefab prefab, int hitPoints)
    {
        try
        {
            // An authored tower is the scene's own entity: it gets the component or the match refuses it, never a barricade beside it.
            var entity = Adopt(slot) ?? (slot.Authored ? AttachAuthored(slot) : Instantiate(slot, prefab));
            var component = entity?.GetFirstScriptOfType<DestructableComponent>();
            if (entity == null || component == null)
            {
                _logger.LogWarning($"[FordsOfWar] prefab '{prefab.Prefab}' for {slot.Id} has no DestructableComponent");
                return false;
            }

            // OnHit would zero our damage (stone only) or force the tower down on any hit; the rules own the damage.
            component.DestroyedByStoneOnly = false;
            component.DestroyOnAnyHit = false;
            component.MaxHitPoint = hitPoints;
            component.HitPoint = hitPoints;
            // Regeneration raises the hit points, and the engine would then recompute a lower stage than the one on show.
            var stage = new FordsTowerStage();
            component.OnCalculateDestructionStateIndex = (index, _, _) => stage.Advance(index);
            component.OnDestroyed += HandleDestroyed;
            if (component is FordsTowerComponent)
            {
                SetFallEffects(entity, component);
                _logger.LogInfo("[FordsOfWar][Tower] " + FordsTowerLines.Attached(slot.Id, entity.Name, entity.ChildCount, hitPoints));
            }

            lock (_gate)
            {
                _towers[slot.Id] = new Tower(slot, entity, component);
                _byComponent[component] = slot.Id;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] tower {slot.Id} could not be built ({ex.GetType().Name}: {ex.Message})");
            return false;
        }
    }

    public void LogFall(string towerId)
    {
        var tower = Find(towerId);
        if (tower == null || tower.Component is not FordsTowerComponent)
            return;

        try
        {
            var root = tower.Entity;
            var child = root.ChildCount > 0 ? root.GetChild(0) : null;
            // Include parents: the engine hides only the root, and that hides the children. It also turns physics off with children, which no flag here reads.
            _logger.LogInfo("[FordsOfWar][Tower] " + FordsTowerLines.Fell(towerId, root.IsVisibleIncludeParents(), child?.IsVisibleIncludeParents() ?? false));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] the fall read-back of {towerId} failed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    public void Heal(string towerId, int amount)
    {
        var component = Find(towerId)?.Component;
        if (component == null || component.IsDestroyed || amount <= 0)
            return;

        component.HitPoint = Math.Min(component.MaxHitPoint, component.HitPoint + amount);
    }

    public int SpawnGunners(TowerSlot slot, string troopId, int count)
    {
        var tower = Find(slot.Id);
        var team = FordsEngine.TeamOf(_mission, slot.Owner);
        var character = FordsEngine.Character(troopId);
        if (tower == null || team == null || character == null)
        {
            _logger.LogWarning($"[FordsOfWar] garrison '{troopId}' for {slot.Id} cannot be built (tower, team or troop missing)");
            return 0;
        }

        var scene = _mission.Scene;
        var facing = FordsTowerGeometry.MarchDirection(slot);
        var direction = new Vec2(facing.X, facing.Y);
        var spots = slot.GunnerSpots;
        for (var i = 0; i < count; i++)
        {
            try
            {
                var spot = spots != null ? spots[i] : FordsTowerGeometry.GunnerPosition(slot, i, count);
                var position = new Vec3(spot.X, spot.Y, spots != null ? spot.Z : FordsEngine.TerrainAt(scene, spot.X, spot.Y), -1f);
                var data = new AgentBuildData(character)
                    .Team(team)
                    .Banner(team.Banner)
                    .ClothingColor1(team.Color)
                    .ClothingColor2(team.Color2)
                    .TroopOrigin(new BasicBattleAgentOrigin(character))
                    .NoHorses(true)
                    .InitialPosition(in position)
                    .InitialDirection(in direction);
                var agent = _mission.SpawnTroopWithAgentBuildData(data, false, true);
                agent.SetMortalityState(Agent.MortalityState.Invulnerable);
                var hold = new WorldPosition(scene, UIntPtr.Zero, position, false);
                var flags = Agent.AIScriptedFrameFlags.ConsiderRotation;
                if (spots != null)
                {
                    // A bowman must never walk off a small island. Once: the limit is a stat, and the engine re-reads it.
                    agent.SetMaximumSpeedLimit(0f, false);
                    flags |= Agent.AIScriptedFrameFlags.NoAttack;
                }

                agent.SetScriptedPositionAndDirection(ref hold, direction.RotationInRadians, false, flags);
                tower.Gunners.Add(agent);
                if (spots != null)
                    _logger.LogInfo("[FordsOfWar][Garrison] " + FordsTowerLines.GarrisonSpawned(slot.Id, i, spot, agent.Position.Z, agent.GetCurrentNavigationFaceId()));
                lock (_gate)
                    _byGunner[agent] = slot.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FordsOfWar] gunner {i} of {slot.Id} could not be spawned ({ex.GetType().Name}: {ex.Message})");
            }
        }

        return tower.Gunners.Count;
    }

    public void KillGunners(string towerId)
    {
        var tower = Find(towerId);
        if (tower == null)
            return;

        foreach (var gunner in tower.Gunners)
        {
            if (IsLive(gunner))
                gunner.FadeOut(true, true);
        }

        lock (_gate)
        {
            foreach (var gunner in tower.Gunners)
                _byGunner.Remove(gunner);
        }

        tower.Gunners.Clear();
    }

    public void LogGarrison()
    {
        List<Tower> towers;
        lock (_gate)
            towers = _towers.Values.Where(t => t.Slot.GunnerSpots != null).ToList();

        var total = 0;
        var held = 0;
        var maxDrift = 0f;
        var maxDz = 0f;
        var worst = "-";
        var faces = new SortedDictionary<int, int>();
        foreach (var tower in towers)
        {
            for (var i = 0; i < tower.Gunners.Count; i++)
            {
                var gunner = tower.Gunners[i];
                if (!IsLive(gunner) || i >= tower.Slot.GunnerSpots!.Count)
                    continue;

                total++;
                var spot = tower.Slot.GunnerSpots[i];
                var at = gunner.Position;
                var drift = new Vec2(at.x - spot.X, at.y - spot.Y).Length;
                var dz = Math.Abs(at.z - spot.Z);
                if (drift <= GarrisonHoldMetres && dz <= GarrisonHoldHeight)
                    held++;
                if (drift + dz > maxDrift + maxDz)
                    worst = tower.Slot.Id + "/" + i;
                maxDrift = Math.Max(maxDrift, drift);
                maxDz = Math.Max(maxDz, dz);
                var face = gunner.GetCurrentNavigationFaceId();
                faces[face] = faces.TryGetValue(face, out var n) ? n + 1 : 1;
            }
        }

        var byFace = string.Join(",", faces.Select(p => p.Key + ":" + p.Value));
        _logger.LogInfo("[FordsOfWar][Garrison] " + FordsTowerLines.GarrisonLook(held, total, maxDrift, maxDz, byFace, worst));
    }

    public int Fire(string towerId, Vec3f target, int missiles, float towerHalfWidth = 0f)
    {
        var tower = Find(towerId);
        var shooter = tower?.Gunners.Find(IsLive);
        var item = VolleyItem();
        if (tower == null || shooter == null || item == null || missiles <= 0)
            return 0;

        var origin = FordsTowerGeometry.VolleyOrigin(tower.Slot);
        if (tower.Slot.GunnerSpots is { Count: > 0 } spots)
        {
            if (FordsTowerGeometry.VolleyOriginFromSpot(spots[0], tower.Slot.Position, target, towerHalfWidth) is not { } clear)
                return 0;
            origin = clear;
        }

        var added = 0;
        for (var i = 0; i < missiles; i++)
        {
            var aimAt = target + new Vec3f(Spread(), Spread(), 0f);
            // Every second missile of a volley is an arrow, when the item loaded.
            var arrow = i % 2 == 1 ? TowerArrow() : null;
            var speed = arrow != null ? _arrowSpeed : _volleySpeed;
            if (FordsTowerGeometry.AimDirection(origin, aimAt, speed) is not { } aim)
                continue;

            var direction = FordsEngine.ToVec3(aim);
            var orientation = Mat3.Identity;
            orientation.f = direction;
            orientation.Orthonormalize();
            var weapon = new MissionWeapon(arrow ?? item, null, null, 1);
            _mission.AddCustomMissile(shooter, weapon, FordsEngine.ToVec3(origin), direction, orientation, speed, speed,
                false, tower.Component);
            added++;
        }

        return added;
    }

    public string? TowerIdOf(object? component)
    {
        if (component == null)
            return null;

        lock (_gate)
            return _byComponent.TryGetValue(component, out var id) ? id : null;
    }

    public string? TowerIdOfGunner(object? agent)
    {
        if (agent == null)
            return null;

        lock (_gate)
            return _byGunner.TryGetValue(agent, out var id) ? id : null;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _towers.Clear();
            _byComponent.Clear();
            _byGunner.Clear();
        }
    }

    private GameEntity? Adopt(TowerSlot slot)
    {
        var marked = _mission.Scene.FindEntityWithTag(TagOf(slot));
        return marked != null && marked.GetFirstScriptOfType<DestructableComponent>() != null ? marked : null;
    }

    private static string TagOf(TowerSlot slot)
    {
        var side = slot.Owner == MatchSide.West ? "west" : "east";
        return slot.Kind == TowerKind.Keep ? $"fow_keep_{side}" : $"fow_tower_{side}_{slot.Lane}_{slot.Order}";
    }

    /// <summary>Puts a <see cref="FordsTowerComponent"/> on the tagged entity of a tower the scene built; null (after a log line) when there is none or the engine refused the script.</summary>
    private GameEntity? AttachAuthored(TowerSlot slot)
    {
        var marked = _mission.Scene.FindEntityWithTag(TagOf(slot));
        if (marked == null)
        {
            _logger.LogError($"[FordsOfWar][Tower] {slot.Id} is authored but no entity carries the tag {TagOf(slot)}");
            return null;
        }

        marked.CreateAndAddScriptComponent(nameof(FordsTowerComponent), true);
        if (marked.GetFirstScriptOfType<FordsTowerComponent>() != null)
            return marked;

        _logger.LogError($"[FordsOfWar][Tower] the engine did not attach {nameof(FordsTowerComponent)} to {marked.Name} for {slot.Id}");
        return null;
    }

    // Splinters and a siege tower's break sound: the closest engine assets found to a tower falling, until a stone one is named.
    private static void SetFallEffects(GameEntity entity, DestructableComponent component)
    {
        component.ParticleEffectOnDestroy = FallParticle;
        component.SoundEffectOnDestroy = FallSound;
        try
        {
            var (low, high) = entity.ComputeGlobalPhysicsBoundingBoxMinMax();
            component.SoundAndParticleEffectHeightOffset = Math.Max(0f, (high.z - low.z) / 2f);
        }
        catch (Exception)
        {
            component.SoundAndParticleEffectHeightOffset = DefaultFallHeight;
        }
    }

    private GameEntity? Instantiate(TowerSlot slot, TowerPrefab prefab)
    {
        var frame = MatrixFrame.Identity;
        frame.origin = FordsEngine.ToVec3(slot.Position);
        frame.rotation.f = FordsEngine.ToVec3(FordsTowerGeometry.MarchDirection(slot));
        frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        frame.rotation.ApplyScaleLocal(prefab.Scale);
        return GameEntity.Instantiate(_mission.Scene, prefab.Prefab, frame);
    }

    // The engine raises this inside DestructableComponent.OnHit, on whatever thread the hit arrived on.
    private void HandleDestroyed(DestructableComponent target, Agent attackerAgent, in MissionWeapon weapon,
        ScriptComponentBehavior attackerScriptComponentBehavior, int inflictedDamage)
    {
        try
        {
            var id = TowerIdOf(target);
            if (id != null)
                _onDestroyed?.Invoke(id);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] tower destroyed handler failed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    public FordsVolleyItemInfo? VolleyItemInfo() =>
        VolleyItem() == null ? null : new FordsVolleyItemInfo(VolleyItemId, _volleyThrust, _volleySpeed, FordsTowerGeometry.Reach(_volleySpeed));

    public float? HitPoints(string towerId) => Find(towerId)?.Component.HitPoint;

    private Tower? Find(string towerId)
    {
        lock (_gate)
            return _towers.TryGetValue(towerId, out var tower) ? tower : null;
    }

    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);

    private float Spread() => ((float)_spread.NextDouble() * 2f - 1f) * VolleySpread;

    // Looked up at match start through VolleyItemInfo, or at the first volley when that has not run: the item's own numbers are read,
    // logged once, and checked against the fire range.
    private ItemObject? TowerArrow()
    {
        if (_arrowLookedUp)
            return _arrowItem;

        _arrowLookedUp = true;
        _arrowItem = MBObjectManager.Instance?.GetObject<ItemObject>(TowerArrowItemId);
        if (_arrowItem == null)
        {
            _logger.LogWarning($"[FordsOfWar] the tower arrow item '{TowerArrowItemId}' does not exist; towers fire javelins only");
            return null;
        }

        var probe = new MissionWeapon(_arrowItem, null, null, 1);
        _arrowSpeed = probe.GetModifiedMissileSpeedForCurrentUsage();
        _logger.LogInfo($"[FordsOfWar] tower arrow item '{TowerArrowItemId}': thrust damage {probe.GetModifiedThrustDamageForCurrentUsage()}, missile speed {_arrowSpeed}");
        return _arrowItem;
    }

    private ItemObject? VolleyItem()
    {
        if (_volleyLookedUp)
            return _volleyItem;

        _volleyLookedUp = true;
        _volleyItem = MBObjectManager.Instance?.GetObject<ItemObject>(VolleyItemId);
        if (_volleyItem == null)
        {
            _logger.LogWarning($"[FordsOfWar] the volley javelin item '{VolleyItemId}' does not exist; towers will not fire");
            return null;
        }

        var probe = new MissionWeapon(_volleyItem, null, null, 1);
        _volleySpeed = probe.GetModifiedMissileSpeedForCurrentUsage();
        _volleyThrust = probe.GetModifiedThrustDamageForCurrentUsage();
        _logger.LogInfo($"[FordsOfWar] tower volley item '{VolleyItemId}': thrust damage {_volleyThrust}, missile speed {_volleySpeed}");
        if (FordsTowerGeometry.Reach(_volleySpeed) < _fireRange)
            _logger.LogWarning($"[FordsOfWar] the volley javelin '{VolleyItemId}' reaches {FordsTowerGeometry.Reach(_volleySpeed):0.#} m at 45 degrees (missile speed {_volleySpeed}), short of the {_fireRange:0.#} m fire range");
        return _volleyItem;
    }
}
