using System.Collections.Generic;
using System.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The Elven Volley's engine work (phase3-plan.md Slice 6, D22, D23): the arrow item, the attackers in reach, the two rays and the fire. The volley is scripted missiles through
/// <c>Mission.AddCustomMissile</c>, as <c>FordsTowerAdapter.Fire</c> adds its tower volley, from the recorded origins behind the parapet. Every arrow is owned by a real defender, because a kill must reach
/// <c>CustomBattleMoraleModel</c> with a non-null affector (E17) and a friendly body hit is cancelled when the shooter is AI-controlled or the mission forces no friendly fire, while a friendly shield or weapon block is never cancelled (<c>Mission.cs:5823</c>, <c>:5861</c>,
/// <c>:5866-5904</c>, E14). The reach figures are the vacuum arc (gravity only): the engine also gives an arrow air drag, so the real reach is shorter (see <c>impact_speed=</c> in the <c>volley</c> line). Main thread only.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    // The arrow items looked up so far, by id; a null entry is an item the game does not hold, remembered so it is warned about once.
    private readonly Dictionary<string, VolleyArrow?> _arrows = new();

    private sealed record VolleyArrow(ItemObject Item, LdVolleyItem Info);

    // Looked up at the first volley and logged once with its own thrust and speed, and the reach against the volley's range, as the Fords adapter does for its tower arrow (FordsTowerAdapter.TowerArrow,
    // :476-494): AddCustomMissile adds no bow damage (Mission.cs:4954-4980), so an arrow carries only its own thrust, and a speed that cannot reach the range is warned about.
    private VolleyArrow? ArrowOf(string itemId, float range)
    {
        if (_arrows.TryGetValue(itemId, out var known))
            return known;

        var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        VolleyArrow? arrow = null;
        if (item == null || item.ItemType != ItemObject.ItemTypeEnum.Arrows)
        {
            _logger.LogWarning($"[LongDefeat][Volley] the arrow item '{itemId}' {(item == null ? "does not exist" : "is not an arrow")}; the volley will not fire");
        }
        else
        {
            var probe = new MissionWeapon(item, null, null, 1);
            var speed = probe.GetModifiedMissileSpeedForCurrentUsage();
            var thrust = probe.GetModifiedThrustDamageForCurrentUsage();
            var reach = FordsTowerGeometry.Reach(speed);
            _logger.LogInfo($"[LongDefeat][Volley] arrow item '{itemId}': thrust damage {thrust}, missile speed {speed}, reach {Num(reach)} m at 45 degrees before air drag");
            if (!(speed > 0f) || float.IsInfinity(speed))
                _logger.LogWarning($"[LongDefeat][Volley] the arrow '{itemId}' has missile speed {speed}; the volley will not fire");
            else
            {
                arrow = new VolleyArrow(item, new LdVolleyItem(speed));
                if (reach < range)
                    _logger.LogWarning($"[LongDefeat][Volley] the arrow '{itemId}' reaches {Num(reach)} m at 45 degrees before air drag (missile speed {speed}), short of the volley's range {Num(range)} m");
            }
        }

        _arrows[itemId] = arrow;
        return arrow;
    }

    public LdVolleyItem? VolleyItem(string itemId, float range) => ArrowOf(itemId, range)?.Info;

    // The range test is VolleyAim's, in the same space: 3D from the nearest raised origin to the chest point (the pre-filter is never narrower than the aim). A mount is no target (IsHuman), a runner is not
    // standing, and an attacker the registry does not hold has no key and is left out. The velocity is the horizontal movement (Agent.cs:1541-1549, E29), z forced to 0 for the aim's lead.
    public IReadOnlyList<LdVolleyTarget> ReadVolleyTargets(IReadOnlyList<LdPoint> origins, float originHeight, float range)
    {
        var raised = origins.Select(o => new Vec3(o.X, o.Y, o.Z + originHeight)).ToList();
        var found = new List<LdVolleyTarget>();
        foreach (var agent in _mission.AttackerTeam.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.IsRunningAway || !_registry.TryGetKey(agent, out var key))
                continue;

            var chest = agent.GetChestGlobalPosition();
            if (!raised.Any(o => (chest - o).Length <= range))
                continue;

            var velocity = agent.Velocity;
            found.Add(new LdVolleyTarget(key, FordsEngine.ToVec3f(chest), new Vec3f(velocity.x, velocity.y, 0f)));
        }

        return found;
    }

    // Scene.RayCastForClosestEntityOrTerrain(source, target, out distance, out point, thickness, flags) answers whether the segment hits and where (Scene.cs:1405-1411), with the focus ray's own default flags.
    public Vec3f? RayHit(Vec3f origin, Vec3f direction, float range)
    {
        var from = FordsEngine.ToVec3(origin);
        var to = FordsEngine.ToVec3(new Vec3f(origin.X + direction.X * range, origin.Y + direction.Y * range, origin.Z + direction.Z * range));
        Vec3 point;
        return _mission.Scene.RayCastForClosestEntityOrTerrain(from, to, out _, out point) && point.IsValid ? FordsEngine.ToVec3f(point) : null;
    }

    public LdVolleyRay? EyeRay()
    {
        var main = _mission.MainAgent;
        if (main == null || !main.IsActive())
            return null;

        return new LdVolleyRay(FordsEngine.ToVec3f(main.GetEyeGlobalPosition()), FordsEngine.ToVec3f(main.LookDirection));
    }

    // One missile per shot, owned by a defender chosen and read for IsLive, AI control and the main agent right here, then the call: a missile that outlives its owner is A24's case. The missile gets the
    // direction as its forward axis, as the Fords tower fire builds it (FordsTowerAdapter.Fire), and the item's speed for both the base and the flight speed (A25). A null mission object to ignore is handled
    // outside a server or a recorder ("missionObjectToIgnore?.GameEntity ?? WeakGameEntity.Invalid", Mission.cs:4954-4980, E11; only the server branch reads the object's Id); a wall origin has none, and neither has
    // a tower's: Fords passes its tower's FordsTowerComponent (FordsTowerAdapter.cs:348-349), a MissionObject, while Helm's Deep's high towers are plain static entities (scene.xscene, reworked 2026-11-06: "sm_helms_dep_castle_tower_a" carries a
    // mesh and no script), so there is no MissionObject to name; the guard against an arrow striking its own parapet is the boot clearance ray and the origin height (R5). A missile is no agent,
    // which the agent count around the calls shows, and the missile list's growth is the count added. The index of each missile is handed back, because it is what a hit carries (E11) and tells one use's arrows from another's.
    public LdVolleyFire FireVolley(string itemId, IReadOnlyList<LdVolleyShot> shots, string towerId)
    {
        var arrow = ArrowOf(itemId, 0f);
        if (arrow == null)
            return LdVolleyFire.Refused(VolleyWithheldReason.NoItem);

        var owner = ChooseVolleyOwner(towerId);
        if (owner == null)
            return LdVolleyFire.Refused(VolleyWithheldReason.NoOwner);

        var before = _mission.AllAgents.Count;
        var missilesBefore = _mission.MissilesList.Count;
        var multiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(owner, new MissionWeapon(arrow.Item, null, null, 1).CurrentUsageItem);
        var indices = new List<int>();
        foreach (var shot in shots)
        {
            var direction = FordsEngine.ToVec3(shot.Direction);
            var orientation = Mat3.Identity;
            orientation.f = direction;
            orientation.Orthonormalize();
            var weapon = new MissionWeapon(arrow.Item, null, null, 1);
            var missile = _mission.AddCustomMissile(owner, weapon, FordsEngine.ToVec3(shot.Origin), direction, orientation, shot.Speed, shot.Speed, false, null);
            indices.Add(missile.Index);
        }

        // The engine lists each missile as it adds it (Mission.cs:4970), so the list's growth is what the engine took, and the check's "added equals shots" leg can fail.
        var added = _mission.MissilesList.Count - missilesBefore;
        return new LdVolleyFire(null, added, before, _mission.AllAgents.Count, owner.Character?.StringId ?? string.Empty, multiplier, indices);
    }

    // D22: a live, AI-controlled human defender who is not the main agent, archers first and the one farthest from the nearest attacker. A tower's volley is owned by a post archer of that tower, chosen by the same
    // rule among that tower's posted archers only (the lists of Posts.cs, held by reference; PruneDead drops a dead or recycled handle before any native read), and a tower with none standing has no owner, so its shot is withheld and the run stops that
    // tower's clock for the wave. A standing defender is one the slot still holds (IsLive) who is not running away.
    private Agent? ChooseVolleyOwner(string towerId)
    {
        var attackers = _mission.AttackerTeam.ActiveAgents.Where(a => a.IsHuman).Select(a => a.Position).ToList();
        var defenders = towerId.Length > 0
            ? PruneDead().Where(p => p.TowerId == towerId).Select(p => p.Agent).ToList()
            : _mission.DefenderTeam.ActiveAgents.Where(a => a.IsHuman).ToList();
        var candidates = defenders.Select(a =>
        {
            var live = IsLive(a) && !a.IsRunningAway;
            var eligible = live && a.IsAIControlled && !a.IsMainAgent;
            return new LdOwnerCandidate(live, a.IsAIControlled, a.IsMainAgent, a.IsRangedCached, eligible ? NearestAttacker(a.Position, attackers) : 0f);
        }).ToList();
        var chosen = VolleyOwnerRule.Choose(candidates);
        return chosen < 0 ? null : defenders[chosen];
    }

    private static float NearestAttacker(Vec3 at, IReadOnlyList<Vec3> attackers)
    {
        var nearest = float.PositiveInfinity;
        foreach (var other in attackers)
            nearest = System.Math.Min(nearest, (at - other).Length);
        return nearest;
    }

    // A deleted Agent answers IsActive() for the slot's next tenant (csharp-architecture.md, "Mission-scope agent handles"), so a handle read across frames passes only while it is still the occupant of its slot.
    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);
}
