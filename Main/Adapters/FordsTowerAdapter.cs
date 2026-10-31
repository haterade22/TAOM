using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
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

    private const float VolleySpread = 1.5f;

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

    private readonly Mission _mission;
    private readonly IModLogger _logger;
    private readonly float _fireRange;
    private readonly object _gate = new();
    private readonly Dictionary<string, Tower> _towers = new();
    private readonly Dictionary<object, string> _byComponent = new(ReferenceComparer.Instance);
    private readonly Random _spread = new(12345);
    private Action<string>? _onDestroyed;
    private ItemObject? _volleyItem;
    private float _volleySpeed;
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
            var entity = Adopt(slot) ?? Instantiate(slot, prefab);
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
            component.OnDestroyed += HandleDestroyed;
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
        for (var i = 0; i < count; i++)
        {
            try
            {
                var spot = FordsTowerGeometry.GunnerPosition(slot, i, count);
                var position = new Vec3(spot.X, spot.Y, FordsEngine.TerrainAt(scene, spot.X, spot.Y), -1f);
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
                agent.SetScriptedPositionAndDirection(ref hold, direction.RotationInRadians, false, Agent.AIScriptedFrameFlags.ConsiderRotation);
                tower.Gunners.Add(agent);
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

        tower.Gunners.Clear();
    }

    public bool Fire(string towerId, Vec3f target, int missiles)
    {
        var tower = Find(towerId);
        var shooter = tower?.Gunners.Find(IsLive);
        var item = VolleyItem();
        if (tower == null || shooter == null || item == null || missiles <= 0)
            return false;

        var origin = FordsTowerGeometry.VolleyOrigin(tower.Slot);
        for (var i = 0; i < missiles; i++)
        {
            var aimAt = target + new Vec3f(Spread(), Spread(), 0f);
            if (FordsTowerGeometry.AimDirection(origin, aimAt, _volleySpeed) is not { } aim)
                continue;

            var direction = FordsEngine.ToVec3(aim);
            var orientation = Mat3.Identity;
            orientation.f = direction;
            orientation.Orthonormalize();
            var weapon = new MissionWeapon(item, null, null, 1);
            _mission.AddCustomMissile(shooter, weapon, FordsEngine.ToVec3(origin), direction, orientation, _volleySpeed, _volleySpeed,
                false, tower.Component);
        }

        return true;
    }

    public string? TowerIdOf(object? component)
    {
        if (component == null)
            return null;

        lock (_gate)
            return _byComponent.TryGetValue(component, out var id) ? id : null;
    }

    public void Clear()
    {
        lock (_gate)
        {
            _towers.Clear();
            _byComponent.Clear();
        }
    }

    private GameEntity? Adopt(TowerSlot slot)
    {
        var side = slot.Owner == MatchSide.West ? "west" : "east";
        var tag = slot.Kind == TowerKind.Keep ? $"fow_keep_{side}" : $"fow_tower_{side}_{slot.Lane}_{slot.Order}";
        var marked = _mission.Scene.FindEntityWithTag(tag);
        return marked != null && marked.GetFirstScriptOfType<DestructableComponent>() != null ? marked : null;
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

    private Tower? Find(string towerId)
    {
        lock (_gate)
            return _towers.TryGetValue(towerId, out var tower) ? tower : null;
    }

    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);

    private float Spread() => ((float)_spread.NextDouble() * 2f - 1f) * VolleySpread;

    // Looked up at the first volley: that is when the item's own numbers are read, logged once, and checked against the fire range.
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
        _logger.LogInfo($"[FordsOfWar] tower volley item '{VolleyItemId}': thrust damage {probe.GetModifiedThrustDamageForCurrentUsage()}, missile speed {_volleySpeed}");
        if (FordsTowerGeometry.Reach(_volleySpeed) < _fireRange)
            _logger.LogWarning($"[FordsOfWar] the volley javelin '{VolleyItemId}' reaches {FordsTowerGeometry.Reach(_volleySpeed):0.#} m at 45 degrees (missile speed {_volleySpeed}), short of the {_fireRange:0.#} m fire range");
        return _volleyItem;
    }
}
