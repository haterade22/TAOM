using System.Collections.Generic;

namespace TAOM.Features.Spider;

public static class SpiderConfig
{
    // Spawn behavior (Custom Battle smoke test)
    public const int SpawnCount = 5;
    public const float SpawnRadius = 12f;

    // Combat behavior
    internal static float TargetDetectionRange = 4f;
    internal static int SleepAfterAttack = 2;

    // Damage formula (mirrors WargConfig shape)
    public const float SpeedForMaxDamage = 15f;
    public const int MaxSpeedDamage = 15;
    public const int MaxBaseDamage = 35;
    public const int DamageToFlinch = 8;
    public const int DamageToFall = 30;

    // Bone indices for fang/bite collision points.
    // PLACEHOLDER values copied from warg pattern (chest 23, jaw 37, fangs 43).
    // Codex review #spider-2026-04-23 confirmed these are FUNCTIONAL placeholders
    // (not cosmetic) — bites only land when the indexed spider bones are within
    // 0.3-0.4f of target bones. With wrong indices, mouth-over-target contact may
    // miss while leg/body contact may erroneously hit. Refine after a runtime
    // bone-index dump that resolves joint5_l, joint5_r, joint12_m on as_spider.
    public const sbyte FangBoneIndexPrimary = 23;
    public const sbyte FangBoneIndexSecondaryLeft = 37;
    public const sbyte FangBoneIndexSecondaryRight = 43;

    // Pre-allocated bone-index lists for SpiderAttackService.SpiderAttack —
    // avoids per-attack list allocation on the BT tick path.
    // Note: List<sbyte> rather than IReadOnlyList<sbyte> because IAgentAdapter.CustomAttack
    // requires a concrete List<sbyte>. Treat as immutable; never call .Add/.Remove.
    public static readonly List<sbyte> ChargeAttackBones = new List<sbyte> { FangBoneIndexPrimary };
    public static readonly List<sbyte> StandAttackBones = new List<sbyte>
    {
        FangBoneIndexPrimary, FangBoneIndexSecondaryLeft, FangBoneIndexSecondaryRight,
    };
}
