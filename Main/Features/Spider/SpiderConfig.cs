namespace TAOM.Features.Spider;

/// <summary>
/// Tuning for the Giant Spider — a RIDDEN MOUNT (warg → elephant lineage, 2026-06-10) that auto-attacks
/// while ridden via <see cref="SpiderBehaviorTree"/>. The rider is the goblin cavalry troop
/// `taom_spider_creature` (characters/spider_creature.xml) with `Item.spider_mount_a` in its Horse slot;
/// the vanilla cavalry spawn path builds rider + mount — no spawn interception.
///
/// Attack model (2026-06-15): the warg's single bite was replaced with the ELEPHANT's directional model —
/// a priority <b>pounce</b> (front, or the charge variant at speed) on a long cooldown, else a <b>left/right
/// swipe</b> picked by the enemy's bearing on a short cooldown. All clips already exist + are bound in the
/// live root `as_spider` (LOTRLOME_Armory action_sets.xml); the directional clips were previously unused.
/// </summary>
public static class SpiderConfig
{
    /// <summary>The spider Monster's StringId (LOTRLOME_Armory lotr_monster_spider.xml) — BT-attach + mount-lock key.</summary>
    public const string SpiderMonsterId = "spider";

    /// <summary>MountDifficulty forced on the spider so non-rider AI can't take it (elephant parity: 999f).</summary>
    public const float MountDifficulty = 999f;

    // --- Engage gate (BT) ---
    /// <summary>SpatialGrid scan radius for the engage gate (warg used a hardcoded 10).</summary>
    internal const float BiteTriggerScanRange = 10f;
    /// <summary>IsAttackLikelyToHit reach for the engage gate (warg: 1f; the spider body is larger).</summary>
    internal const float BiteAttackRange = 1.5f;
    /// <summary>Facing cone for the engage gate, degrees (warg parity: 30).</summary>
    internal const float BiteConeAngleDegrees = 30f;
    // --- Radial strike (2026-06-15): bone-collision connected only ~6% on this big fast mount even with the
    // correct front-leg bones + a 1.8m sphere, so damage is now dealt RADIALLY in a front arc when the spider
    // attacks (the elephant's reliable model). The front-leg ANIMATION still plays; the front ARC keeps it a
    // directed front-leg strike. Tunable from battle feel. ---
    /// <summary>Radius (m) around the spider within which an attack damages enemies — covers the front-leg strike reach.</summary>
    public const float StrikeRadius = 3.5f;
    /// <summary>Pounce arc half-angle (deg) about straight ahead — a forward cone (±70° = 140°).</summary>
    public const float PounceArcHalfAngleDeg = 70f;
    /// <summary>Side-swipe arc center offset (deg) from straight ahead (+ = LEFT). Left swipe centers here; right at its negation.</summary>
    public const float SideArcCenterDeg = 45f;
    /// <summary>Side-swipe arc half-angle (deg) about the side center — covers forward-to-flank on that side.</summary>
    public const float SideArcHalfAngleDeg = 50f;

    // --- Directional attack model (elephant parity: priority attack on a long cooldown, side attacks on a short one) ---
    /// <summary>Forward speed (vel.Y) at or above which a pounce uses the running charge clip instead of the standing front bite.</summary>
    internal const float ChargeVelocityThreshold = 4f;
    /// <summary>Seconds between pounces (the priority lunge — fires whenever engaged + off cooldown). First-pass; tune from battle feel.</summary>
    internal const double PounceCooldownSeconds = 5.0;
    /// <summary>Seconds between side (left/right) swipes — fills the gap while the pounce recharges. First-pass; tune from battle feel.</summary>
    internal const double SideAttackCooldownSeconds = 2.0;

    // --- Attack clips (registered in LOTRLOME action_types.xml, bound in as_spider) ---
    /// <summary>Standing front bite — the default pounce.</summary>
    public const string PounceFrontActionName = "act_spider_attack_front";
    /// <summary>Running lunge — the pounce variant when moving fast (vel.Y ≥ <see cref="ChargeVelocityThreshold"/>).</summary>
    public const string PounceChargeActionName = "act_spider_attack_charge";
    /// <summary>Left swipe — played when the best enemy bears to the spider's LEFT (TargetBearing ≥ 0).</summary>
    public const string SwingLeftActionName = "act_spider_attack_left";
    /// <summary>Right swipe — played when the best enemy bears to the spider's RIGHT (TargetBearing &lt; 0).</summary>
    public const string SwingRightActionName = "act_spider_attack_right";

    // --- Damage formula (warg-shape; applied per bone-collision hit in HandleSpiderTargetHit). Tuned 2026-06-15
    // (battle feedback) for a STEEP, lethal creature curve: unarmored/light troops are basically killed in one
    // bite, medium line troops in ~2, heavy elites in ~3 — plus a per-hit CRIT roll for burst (a crit can drop
    // a heavy in 2). Levers: high base (75) so light/unarmored die; ArmorMitigationFactor > 1 to WIDEN the armor
    // spread (heavy survives longer — armor must matter MORE here, not less, so light dies while heavy doesn't);
    // a MinArmorPassthrough floor so even plate always takes a bite (fangs pierce); and crit variance.
    // ArmorTorso (GetBaseArmorEffectivenessForBodyPart) ≈ body-armor value: ~0 unarmored, ~15 light, ~35 medium,
    // ~55 heavy. raw = 75 + min(velY×25/15, 25) = 75..100. ---
    public const float SpeedForMaxDamage = 15f;
    public const int MaxSpeedDamage = 25;
    public const int MaxBaseDamage = 75;
    /// <summary>Armor mitigation strength: absorption = (100 − armor × this) / 100, clamped to [MinArmorPassthrough, 1].
    /// &gt;1 widens the spread (heavy survives longer); 1 = linear; &lt;1 softens.</summary>
    public const float ArmorMitigationFactor = 1.1f;
    /// <summary>Absorption floor — even max-armor plate always takes this fraction (a giant spider's fangs pierce).</summary>
    public const float MinArmorPassthrough = 0.2f;
    /// <summary>Per-hit critical-strike chance [0..1] (rolled in HandleSpiderTargetHit via MBRandom.RandomFloat).</summary>
    public const float CritChance = 0.2f;
    /// <summary>Critical-strike damage multiplier — applied after armor; lets a crit drop a heavy in ~2 hits.</summary>
    public const float CritMultiplier = 1.75f;
    public const int DamageToFlinch = 8;
    public const int DamageToFall = 30;

    // Front-leg bone indices — VERIFIED reference, kept for a possible future bone-based feature (NOT wired to
    // damage in the radial model). From spider_skeleton via
    // `python tools/tpac_skeleton_dump.py <spider_correct_geo.tpac> spider_skeleton` (2026-06-15): front-RIGHT
    // joint40-44_r = 14-18, front-LEFT joint40-44_l = 19-23 (40=shoulder 41=thigh 42=knee 43=tibia 44=tip).
    public const sbyte FrontRightLegThigh = 15;  // joint41_r
    public const sbyte FrontRightLegKnee  = 16;  // joint42_r
    public const sbyte FrontRightLegTibia = 17;  // joint43_r
    public const sbyte FrontRightLegTip   = 18;  // joint44_r
    public const sbyte FrontLeftLegThigh  = 20;  // joint41_l
    public const sbyte FrontLeftLegKnee   = 21;  // joint42_l
    public const sbyte FrontLeftLegTibia  = 22;  // joint43_l
    public const sbyte FrontLeftLegTip    = 23;  // joint44_l
}
