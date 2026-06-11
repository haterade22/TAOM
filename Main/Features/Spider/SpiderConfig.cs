using System.Collections.Generic;

namespace TAOM.Features.Spider;

/// <summary>
/// Tuning for the Giant Spider — a RIDDEN MOUNT (warg → elephant lineage, 2026-06-10) that auto-bites
/// while ridden via <see cref="SpiderBehaviorTree"/>. The rider is the goblin cavalry troop
/// `taom_spider_creature` (characters/spider_creature.xml) with `Item.spider_mount_a` in its Horse slot;
/// the vanilla cavalry spawn path builds rider + mount — no spawn interception. The old DETACHED
/// riderless architecture (Patch45 spawn-swap + native wield guards) was deleted 2026-06-10 — it was an
/// unsupported engine shape (native render AV; see docs/reviews/rca-spider-troop-2026-06-04.md; git
/// history preserves the code).
/// </summary>
public static class SpiderConfig
{
    /// <summary>The spider Monster's StringId (LOTRLOME_Armory lotr_monster_spider.xml) — BT-attach + mount-lock key.</summary>
    public const string SpiderMonsterId = "spider";

    /// <summary>MountDifficulty forced on the spider so non-rider AI can't take it (elephant parity: 999f).</summary>
    public const float MountDifficulty = 999f;

    // --- Bite gates (BT) ---
    /// <summary>SpatialGrid scan radius for the engage gate (warg used a hardcoded 10).</summary>
    internal const float BiteTriggerScanRange = 10f;
    /// <summary>IsAttackLikelyToHit reach for the engage gate (warg: 1f; the spider body is larger).</summary>
    internal const float BiteAttackRange = 1.5f;
    /// <summary>Facing cone for the engage gate, degrees (warg parity: 30).</summary>
    internal const float BiteConeAngleDegrees = 30f;
    /// <summary>CustomAttack bone-collision reach during the bite window.</summary>
    internal static float TargetDetectionRange = 4f;
    /// <summary>Post-bite pacing, seconds — the bite's cooldown (warg-style SleepTask, no stamp state).</summary>
    internal static int SleepAfterAttack = 2;

    // --- Bite action clips (registered in LOTRLOME action_types.xml, bound in as_spider) ---
    public const string BiteStandActionName = "act_spider_attack_front";
    public const string BiteChargeActionName = "act_spider_attack_charge";

    // --- Damage formula (mirrors WargConfig shape) ---
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
