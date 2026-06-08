using System.Collections.Generic;

namespace TAOM.Features.Spider;

public static class SpiderConfig
{
    // FEATURE PAUSED (2026-06-05): the spider render AccessViolation (native Agent.PreloadForRendering on the
    // 62-bone skeleton through the mount render path) is unresolved after an exhaustive investigation — see
    // docs/reviews/rca-spider-troop-2026-06-04.md. Enabled=false makes Mission_SpawnAgent_SpiderSwap_Patch skip
    // the detached spawn, so the recruitable troop appears as its harmless humanoid anchor (dg_uruk) instead of
    // crashing the battle. Paused in favour of a warg-pattern rideable war-elephant. Flip to true (and resolve
    // the render AV) to resume. IsSpiderTroop is left intact so its unit tests still pass.
    public static bool Enabled = false;

    // Object IDs — the recruitable troop anchor (humanoid race dg_uruk, so recruitment + roster UI
    // resolve) and the Monster it is spawned as by Patch45_SpiderTroopSpawn (see SpiderTroopSpawnService
    // + SpiderDetachedAgentSpawner).
    //
    // REAL SPIDER — PAUSED, render AV unresolved. SpiderMonsterId / SpiderMountItemId point at the real
    // spider monster + body mesh. It AccessViolates in native Agent.PreloadForRendering on spawn.
    //
    // CORRECTED 2026-06-06 (workflow w06c6pz7n) — the standing theories below were wrong/refuted:
    //   * NOT skeleton Usage: spider_skeleton is already Usage='other' (== the working ADOD wolf); the
    //     elephant renders at Usage='horse'. Usage doesn't route the crash.
    //   * NOT bone count: wolf renders riderless at 57 bones; spider is 62.
    //   * NOT spawn path: SpiderDetachedAgentSpawner's reflected FromHorseObj chain is decompiled-identical
    //     to the wolf's public Mission.SpawnMonster.
    //   * The "L/R mesh split <=40 bones/half" was already tested in-game and the AV PERSISTED — AND the
    //     split tpac is the SAME file size as the original single mesh, so the split likely duplicated
    //     geometry instead of partitioning palettes (it may never have actually worked).
    // MOST-LIKELY real cause: the spider MESH's native skin data (per-mesh palette / >4 influences / vertex
    // buffer in spider_correct.fbx). CHEAPEST untried test: an UN-split single mesh (like the wolf's).
    // SpiderDetachedAgentSpawner fails open (try/catch -> humanoid anchor + log). Full ranked experiments:
    // docs/reviews/rca-spider-troop-2026-06-04.md "Update 2026-06-06".
    public const string SpiderMonsterId = "spider";
    public const string SpiderCharacterId = "taom_spider_creature";

    // The mesh-only Horse item (LOTRLOME_Armory/LOTRAOM_horses.xml) supplying the body mesh + Monster for
    // the detached non-humanoid agent (SpiderDetachedAgentSpawner). is_mountable="false" +
    // is_merchandise="false" + culture-less, so it is never rideable/rostered. WARG STAND-IN (see above).
    public const string SpiderMountItemId = "spider_mount_a";

    // Formation membership = enrolling the detached spider in the army formation so it advances with the
    // line (otherwise it is detached + passive). The engine team-AI then reads the agent's UNINITIALIZED
    // native weapon state (FormationQuerySystem -> Agent.GetMissileRange -> native AccessViolation), and
    // the native state cannot be fixed (RemoveEquippedWeapon -> native WeaponEquipped also AVs, confirmed
    // 2026-06-05). Instead Agent_SpiderNativeWieldGuard_Patch guards the three managed methods that deref
    // the garbage native wield/missile pointers (GetMissileRange -> 0, Get{Primary,Offhand}WieldedItemIndex
    // -> None) for the spider, so the engine never touches them — whether detached or formationed.
    // Formation membership is OFF, by decision (2026-06-05 investigation): enrolling the spider adds a
    // NEW crash — a null-HumanAIComponent NRE in the per-agent formation AI tick (Agent.cs:4726 ->
    // ParallelUpdateFormationMovement, every ~5s) — plus the GetMissileRange read, both gated on
    // Formation != null, and buys nothing a detached spider needs except movement (which a BT move-node
    // gives without the formation team-AI surface). Detached + Agent_SpiderNativeWieldGuard_Patch (the
    // closed 3-method native-wield guard) is the lower-risk architecture. See the RCA.
    public static bool EnableFormationMembership = false;

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
