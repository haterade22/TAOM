using System.Collections.Generic;

namespace TAOM.Features.Spider;

public static class SpiderConfig
{
    // Object IDs — the recruitable troop anchor (humanoid race dg_uruk, so recruitment + roster UI
    // resolve) and the Monster it is spawned as by Patch45_SpiderTroopSpawn (see SpiderTroopSpawnService
    // + SpiderDetachedAgentSpawner).
    //
    // WARG RENDER STAND-IN (committed 2026-06-04): SpiderMonsterId / SpiderMountItemId point at the WARG
    // (warg skeleton + warg_low mesh), NOT the spider. The real spider mesh "sk_spider_forest_c" is a
    // single 62-bone mesh that overflows the native per-mesh bone palette and AccessViolations in
    // Agent.PreloadForRendering during BuildAgent — so the spider cannot render in-game until that mesh is
    // split into sub-meshes (Modding-Kit asset task). The warg renders cleanly and exercises the ENTIRE
    // spawn code path, so it is the committed test stand-in. Once the spider mesh is split, return to the
    // real spider by setting:  SpiderMonsterId = "spider";  SpiderMountItemId = "spider_mount_a";
    // See docs/reviews/rca-spider-troop-2026-06-04.md.
    public const string SpiderMonsterId = "warg";          // WARG STAND-IN — real value: "spider"
    public const string SpiderCharacterId = "taom_spider_creature";

    // The mesh-only Horse item (LOTRLOME_Armory/LOTRAOM_horses.xml) supplying the body mesh + Monster for
    // the detached non-humanoid agent (SpiderDetachedAgentSpawner). is_mountable="false" +
    // is_merchandise="false" + culture-less, so it is never rideable/rostered. WARG STAND-IN (see above).
    public const string SpiderMountItemId = "warg_brown";  // WARG STAND-IN — real value: "spider_mount_a"

    // Formation membership (advancing with the army) is GATED OFF. Enrolling the detached FromHorseObj
    // agent in a Formation makes the engine team-AI read the agent's UNINITIALIZED native weapon state
    // (FormationQuerySystem -> Agent.GetMissileRange -> native AccessViolation). The root fix
    // (SpiderDetachedAgentSpawner.InitializeNativeWeaponState — native WeaponEquipped(Invalid) via
    // RemoveEquippedWeapon, no skin build) is implemented but UNVERIFIED in-game, so it ships behind this
    // flag together with the membership it enables. Flip to true to test formation membership next.
    // Detached (false) spiders spawn positioned in the deployment zone but are PASSIVE — the SpiderTree BT
    // bites adjacent targets only and has no move-to-enemy node. See docs/reviews/rca-spider-troop-2026-06-04.md.
    public const bool EnableFormationMembership = false;

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
