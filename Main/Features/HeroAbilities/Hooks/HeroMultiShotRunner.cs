using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.HeroAbilities.Ranged;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The extra arrows of a running window (#1361, Many Arrows): for one of the hero's own shots, up to N arrows at the
/// enemies nearest his target, through Mission.AddCustomMissile as the Long Defeat volley adds its arrows
/// (LongDefeatBattlefieldAdapter.FireVolley). AddCustomMissile raises no behaviour call (Mission.cs:4954-4979), so an
/// extra arrow never starts a burst of its own; its item (the kit's extra arrow, which no one else carries) tells its
/// hits apart, and it adds no bow damage (Mission.cs:4961 passes 0f), so the item carries the bow's thrust. Main thread
/// only. Boundary code, game-tested (ADR-008).
/// </summary>
internal sealed class HeroMultiShotRunner
{
    private readonly IModLogger _logger;
    private readonly Dictionary<string, ItemObject?> _items = new Dictionary<string, ItemObject?>(StringComparer.Ordinal);
    private readonly List<Agent> _targets = new List<Agent>();
    private readonly List<Vec3f> _points = new List<Vec3f>();
    private bool _refusalLogged;

    internal HeroMultiShotRunner(IModLogger logger)
    {
        _logger = logger;
    }

    internal void Fire(Mission mission, in HeroMissileEvent shot, int extraArrows, HeroRangedTally t)
    {
        // AddCustomMissile writes missionObjectToIgnore.Id on a server or a recorder (Mission.cs:4972-4975), and an
        // extra arrow has no object to ignore.
        if (GameNetwork.IsServerOrRecorder)
        {
            if (!_refusalLogged)
                _logger.LogInfo("[HeroAbilities] extra arrows are not added on a server or a recorder");
            _refusalLogged = true;
            return;
        }
        t.WindowShots++;
        var kit = shot.Kit;
        var hero = shot.Hero;
        var item = ItemOf(kit.ExtraArrowItem);
        t.ItemResolved = item != null;
        if (item == null || hero.Team == null || !AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive())
            return;

        CollectEnemies(mission, hero.Team);
        var pick = ExtraShotPicker.Pick(shot.Position, shot.Velocity, _points, extraArrows, kit.ExtraArrowSpreadMeters, kit.ExtraArrowRangeMeters);
        if (pick.Primary < 0)
            t.NoTarget++;
        var speed = (float)Math.Sqrt(shot.Velocity.X * shot.Velocity.X + shot.Velocity.Y * shot.Velocity.Y + shot.Velocity.Z * shot.Velocity.Z);
        var origin = ToVec3(shot.Position);
        foreach (var i in pick.Extras)
        {
            var weapon = new MissionWeapon(item, null, null, 1);
            var direction = speed > 0f && !float.IsInfinity(speed) ? Aim(shot.Position, _targets[i], weapon, speed) : null;
            if (direction == null)
            {
                t.OutOfRange++;
                continue;
            }
            var orientation = Mat3.Identity;
            orientation.f = direction.Value;
            orientation.Orthonormalize();
            mission.AddCustomMissile(hero, weapon, origin, direction.Value, orientation, BaseSpeed(hero, speed), speed, false, null);
            t.Fired++;
        }
        _targets.Clear();
        _points.Clear();
    }

    internal void Clear()
    {
        _items.Clear();
        _targets.Clear();
        _points.Clear();
        _refusalLogged = false;
    }

    // Mission.GetMissileVerticalAimCorrection answers the release angle above the horizontal, and above pi/2 when the
    // target is out of reach (RangedSiegeWeapon.cs:1323-1326, 1345). Solved at the chest, then at the lead point.
    private static Vec3? Aim(Vec3f origin, Agent target, MissionWeapon weapon, float speed)
    {
        var usage = weapon.CurrentUsageItem;
        if (usage == null)
            return null;
        var stats = weapon.GetWeaponStatsDataForUsage(0);
        var air = ItemObject.GetAirFrictionConstant(usage.WeaponClass, usage.WeaponFlags);
        var chest = ToVec3f(target.GetChestGlobalPosition());
        var first = Mission.GetMissileVerticalAimCorrection(ToVec3(chest) - ToVec3(origin), speed, ref stats, air);
        if (!ExtraShotAim.IsUsableAngle(first))
            return null;
        // The horizontal movement (Agent.cs:1541-1549), as the volley leads its targets.
        var velocity = target.Velocity;
        var lead = ExtraShotAim.LeadPoint(origin, chest, new Vec3f(velocity.x, velocity.y, 0f), speed, first);
        if (lead == null)
            return null;
        var angle = Mission.GetMissileVerticalAimCorrection(ToVec3(lead.Value) - ToVec3(origin), speed, ref stats, air);
        if (!ExtraShotAim.IsUsableAngle(angle))
            return null;
        var direction = ExtraShotAim.Direction(origin, lead.Value, angle);
        return direction == null ? null : ToVec3(direction.Value);
    }

    // The engine's own base speed for a shot is the wielded bow's (Mission.cs:5008).
    private static float BaseSpeed(Agent hero, float speed)
    {
        var index = hero.GetPrimaryWieldedItemIndex();
        if (index < EquipmentIndex.WeaponItemBeginSlot || index >= EquipmentIndex.NumAllWeaponSlots)
            return speed;
        var wielded = hero.Equipment[index];
        return wielded.IsEmpty ? speed : wielded.GetModifiedMissileSpeedForCurrentUsage();
    }

    // Every human of a team hostile to his that stands: a mount is no target, and a runner is leaving.
    private void CollectEnemies(Mission mission, Team heroTeam)
    {
        _targets.Clear();
        _points.Clear();
        foreach (var team in mission.Teams)
        {
            if (ReferenceEquals(team, heroTeam) || !team.IsEnemyOf(heroTeam))
                continue;
            foreach (var agent in team.ActiveAgents)
            {
                if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.IsRunningAway)
                    continue;
                _targets.Add(agent);
                _points.Add(ToVec3f(agent.Position));
            }
        }
    }

    private ItemObject? ItemOf(string id)
    {
        if (id.Length == 0)
            return null;
        if (_items.TryGetValue(id, out var known))
            return known;
        var item = MBObjectManager.Instance?.GetObject<ItemObject>(id);
        if (item != null && item.ItemType != ItemObject.ItemTypeEnum.Arrows)
            item = null;
        if (item == null)
            _logger.LogWarning($"[HeroAbilities] extra arrow item '{id}' does not exist or is not an arrow; no extra arrows this mission");
        _items[id] = item;
        return item;
    }

    private static Vec3f ToVec3f(Vec3 v) => new(v.x, v.y, v.z);

    private static Vec3 ToVec3(Vec3f v) => new(v.X, v.Y, v.Z, -1f);
}
