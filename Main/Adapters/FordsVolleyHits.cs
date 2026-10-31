using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Adapters;

/// <summary>
/// Reads one tower-volley missile hit into the numbers the run report checks the damage formula against. Only the tower javelin
/// fired by a gunner (invulnerable, so a real thrower never qualifies) is read; any other missile returns null.
/// </summary>
internal static class FordsVolleyHits
{
    // Built once, never written after: Read can run on any thread and must not allocate a name per hit.
    private static readonly Dictionary<BoneBodyPartType, string> PartNames = BuildPartNames();

    internal static string PartName(BoneBodyPartType part) =>
        PartNames.TryGetValue(part, out var name) ? name : ((int)part).ToString();

    private static Dictionary<BoneBodyPartType, string> BuildPartNames()
    {
        var names = new Dictionary<BoneBodyPartType, string>();
        foreach (BoneBodyPartType part in Enum.GetValues(typeof(BoneBodyPartType)))
            names[part] = part.ToString();
        return names;
    }

    /// <summary>
    /// The hit as numbers, or null. <paramref name="filtered"/> says why a missile hit was left out (noAffector, otherItem or
    /// notGunner); it stays null for a hit that is not a missile at all.
    /// </summary>
    internal static FordsVolleyHit? Read(Agent affected, Agent? affector, in MissionWeapon weapon, in Blow blow, in AttackCollisionData data,
        out string? filtered)
    {
        filtered = null;
        if (!blow.IsMissile)
            return null;
        if (affector == null)
            filtered = "noAffector";
        else if (weapon.Item?.StringId != FordsTowerAdapter.VolleyItemId)
            filtered = "otherItem";
        else if (affector.CurrentMortalityState != Agent.MortalityState.Invulnerable)
            filtered = "notGunner";
        if (filtered != null)
            return null;

        var mounted = affected.MountAgent != null;
        var v = affected.MovementVelocity;
        v.RotateCCW(affected.MovementDirectionAsAngle); // MissionCombatMechanicsHelper.cs:405-417, the unmounted case
        var impact = mounted ? -1f : (v.ToVec3() - data.MissileVelocity).Length; // MissionCombatMechanicsHelper.cs:717
        var multiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(affector!, weapon.CurrentUsageItem);
        return new FordsVolleyHit(data.BaseMagnitude, blow.InflictedDamage, blow.AbsorbedByArmor, data.MissileTotalDamage,
            data.MissileStartingBaseSpeed, impact, multiplier, PartName(blow.VictimBodyPart), data.AttackBlockedWithShield, mounted, affected.IsHuman);
    }
}
