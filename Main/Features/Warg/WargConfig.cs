using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Warg;

public static class WargConfig
{
    /// <summary>
    /// Every Monster id the warg behaviour tree treats as a warg.
    ///
    /// <see cref="IsWargMonster"/> gates the warg tree (<c>WargMissionBehavior</c>) and rider
    /// hand posing (<c>WargRiderHandManager</c>), both on the agent's own Monster. It was
    /// <c>StringId == "warg"</c>, so any second
    /// warg-family Monster silently got neither, with no crash and no log line. The fell warg reuses
    /// skeleton_warg and as_warg but carries its own Monster for weight (which drives the
    /// charge-knockdown model) and base hit points, so it has to be named here. The dire wolf
    /// (#1081) binds the warg's action names on a skeleton of its own: see <see cref="ImpactBoneIds"/>.
    /// </summary>
    public static readonly IReadOnlyCollection<string> WargMonsterIds =
        new[] { "warg", "fell_warg", DireWolfMonsterId };

    /// <summary>Ordinal match, mirroring the equality this replaced. Monster ids are lowercase in XML.</summary>
    public static bool IsWargMonster(string monsterStringId) =>
        monsterStringId != null && WargMonsterIds.Contains(monsterStringId, StringComparer.Ordinal);

    /// <summary>The dire wolf Monster's StringId, in LOTRLOME_Armory.</summary>
    public const string DireWolfMonsterId = "dire_wolf";

    /// <summary>
    /// The bones that form the bite's impact cone, as indices into the Monster's own skeleton.
    ///
    /// skeleton_warg (49 bones, verified 2026-05-24 #219):
    ///   17 Chest_M, 20 Head_M, 22 Jaw_M, 23 JawEnd_M, 32 Scapula_R, 36 Fingers1_R, 37 Fingers2_R,
    ///   38 Scapula_L, 42 Fingers1_L, 43 Fingers2_L.
    /// adod_wolf_armature_unused (57 bones, read from the tpac with tpac_skeleton_dump 2026-11-04):
    ///   3 Spine2, 5 Head, 6 Jaw, 9 Tongue03, 34 L Clavicle, 37 L Hand, 38 L Finger0, 39 R Clavicle,
    ///   42 R Hand, 43 R Finger0. The wolf has no jaw-end bone, so the tongue tip stands in for it.
    /// A new list per call, because the caller owns it.
    /// </summary>
    public static List<sbyte> ImpactBoneIds(string monsterStringId) =>
        string.Equals(monsterStringId, DireWolfMonsterId, StringComparison.Ordinal)
            ? new List<sbyte> { 3, 5, 6, 9, 34, 37, 38, 39, 42, 43 }
            : new List<sbyte> { 17, 20, 22, 23, 32, 36, 37, 38, 42, 43 };

    internal static float WargAttackRange = 1f;
    // 2026-05-24: restored to 20f to match the original LOTRAOM value. This was
    // hardcoded as `float targetDetectionRange = 20f;` inside WargLogic.WargAttack
    // in LOTRAOM (WargLogic.cs:80). When TAOM extracted it into config during the
    // v1.4.5 refactor the default was reset to 3.5f, costing the BT enough lead
    // time to fire WargAttack while the warg was still closing distance on a
    // running enemy at 8-10 m/s. Earlier session experiments (1.0f, then 3.5f)
    // were compensating for the BoneCheck `_maxRangeForCheck` gate bug (fixed
    // in BoneCheckDuringAnimation 5f63606); with that gate fixed AND the 10-bone
    // impact cone (8a5c89f), the original 20f is the right value. See issue #219.
    internal static float TargetDetectionRange = 20f;
    internal static int SleepAfterAttack = 3;
    public const float SpeedForMaxDamage = 20f;
    public const int MaxSpeedDamage = 20;
    public const int MaxBaseDamage = 40;
    public const int DamageToFlinch = 10;
    public const int DamageToFall = 40;
    public const int minDamageReceivedForRage = 10;
    public const double rageChance = 0.1;
    public const int maxDistanceFromWargToRollForRage = 20;
    public const int rageAttacks = 2;
}
