namespace TAOM.Features.Elephant;

/// <summary>
/// Tuning for the Harad war-elephant. Values are a 1-for-1 port of ADOD_Beasts's elephant
/// agent-component <c>OnTickAsAI</c> trample + its agent-stat-calculate-model mount-lock
/// (decompiled 2026-06-05). "1 for 1 then improve": the trample is a fixed radial knockdown like
/// ADOD_Beasts's; velocity-scaling / bone-collision polish is the later improve step.
/// Provenance: docs/reference/provenance-register.md (ADOD_Beasts row). See docs/features/elephant.md.
/// </summary>
public static class ElephantConfig
{
    /// <summary>The elephant Monster's StringId — matches Monster id="taom_war_elephant" in LOTRLOME_Armory.</summary>
    public const string ElephantMonsterId = "taom_war_elephant";

    /// <summary>HorseHarness item StringId that triggers howdah instantiation (sk_elephant_armor_a in LOTRLOME_Armory).</summary>
    public const string HarnessStringId = "sk_elephant_armor_a";

    /// <summary>HorseHarness item that shows the elite howdah (mesh sk_hd_elep_armor_howdah_elite_a, the deck the platform
    /// prefab is fitted to) in LOTRLOME_Armory's LOTRAOM_horses.xml. It triggers the platform AND its crew; the plain
    /// <see cref="HarnessStringId"/> keeps a crewless platform (#627, Mike 2026-09-19). See <see cref="HowdahHarness"/>.</summary>
    public const string HowdahHarnessStringId = "sk_elephant_armor_howdah_elite";

    /// <summary>Root game_entity name of the howdah platform prefab in LOTRLOME_Armory/Prefabs (#627). Renamed from
    /// taom_howdah_agent 2026-09-19: TAOM installs from v2.0.22 to v2.0.30 keep a copy under that name in
    /// Modules/TAOM/Prefabs, and two prefabs of one name have no defined winner. HowdahPrefabTests pins the prefab to it.</summary>
    public const string HowdahPrefabName = "taom_howdah_platform";

    /// <summary>Name of the platform's floor child, which the diagnostics log measures against the elephant capsule.</summary>
    public const string HowdahFloorEntityName = "howdah_floor";

    /// <summary>Tag on the platform's crew frames: where crew stand (research doc step 3 spawns at them).</summary>
    public const string HowdahCrewTag = "taom_howdah_crew";

    /// <summary>
    /// How far a seated archer may drift from its frame before the seat teleports it back, in metres (#627).
    /// Teleporting EVERY frame is what kept the crew from shooting: the archer settles about 0.10 m from the frame,
    /// the seat yanks it back, and the engine reads that as 30 m/s of real movement with the elephant standing still
    /// and the archer's own legs stopped. Nothing completes a bow draw at that speed. A deadband wider than the
    /// settle leaves a resting archer alone, so its measured velocity falls to what the elephant is actually doing.
    /// </summary>
    public const float HowdahSeatDeadbandMetres = 0.15f;

    /// <summary>Seconds between howdah status lines in the diagnostics log, per howdah.</summary>
    public const float HowdahStatusPeriodSeconds = 5f;

    /// <summary>
    /// The size the war elephant is built at: taom_war_elephant's Horse <c>body_length</c> / 100, which the engine
    /// applies uniformly at build (<c>SetInitialAgentScale</c>) to the skeleton, the animations, the body and hit
    /// capsules and every mesh riding the skeleton, the visible howdah included. 1.3 since 2026-09-22 (Mike: "about
    /// 30 percent" bigger). Everything the engine does NOT scale for us is derived from this below, and the howdah
    /// prefab is authored at this final size. It is a constant rather than a read of AgentScale deliberately: the
    /// platform carries a physics body, and no vanilla object carrying one is ever runtime-scaled; doing so is
    /// what dropped the mumakil's entire crew to the ground (#627). HowdahPrefabTests pins body_length to it.
    /// </summary>
    public const float AuthoredScale = 1.3f;

    /// <summary>
    /// How much higher the elephant carries its back in the live standing pose than in the FBX rest pose, at 1.0x.
    /// Measured in game 2026-09-22 with the Spine1_05 probe on a 1.3x elephant: the spine sat 0.06 to 0.24 m above
    /// its scaled rest height across nine samples, averaging 0.19 m, which is 0.146 m at 1.0x. The 3.15 m deck and
    /// the 3.2 m root below were measured off the REST pose, so the platform stood that much under the visible deck
    /// at every size; at 1.0x it was 15 cm and did not show, at 1.3x with walls 30% taller it did.
    /// </summary>
    public const float HowdahLivePoseLift = 0.146f;

    /// <summary>The howdah platform root above the elephant's feet in the FBX rest pose at 1.0x, measured against
    /// the elite howdah deck.</summary>
    public const float HowdahRootRestAboveFeet = 3.2f;

    /// <summary>Spine1_05's rest head above the feet on elephant_skeleton at 1.0x, measured in Blender. The visible
    /// howdah is skinned to this bone, so the root sits a fixed (3.2 - 2.199) above it in any pose.</summary>
    public const float HowdahSpineRestAboveFeet = 2.199f;

    /// <summary>
    /// The FALLBACK height of the howdah platform above the elephant's feet, used only when the live spine cannot be
    /// read (the build-time frame, before the skeleton is posed, or a failed bone read). The live path follows the
    /// spine every frame (HowdahSeatMotion.RootAboveFeetFromSpine), so the deck bobs with the visible one; this is
    /// the rest height plus the AVERAGE live-pose lift, the best a single fixed number can do.
    /// </summary>
    public const float HowdahHeightAboveGround = (HowdahRootRestAboveFeet + HowdahLivePoseLift) * AuthoredScale;

    /// <summary>CharacterObject StringId force-spawned into the howdah seat on mahout build.
    /// Vanilla detachment cannot path to a moving machine — crew must be spawned directly.</summary>
    // The crew's own troop (#627, Mike 2026-09-19): bow and quivers, no sword. harad_archer sat here first and its
    // roster carries aserai_sword_3_t3, so archers were seen with swords drawn on the deck; a seated archer can never
    // reach a melee target anyway. troops_harad.xml owns it, HowdahCrewLoadoutTests pins the roster.
    public const string HowdahCrewCharacterId = "harad_howdah_crew";

    /// <summary>MountDifficulty forced on the elephant so non-rider AI can't take it (ADOD_Beasts:999f).</summary>
    public const float MountDifficulty = 999f;

    // --- AI attack gates (facing/range/damage are 1-for-1 with ADOD_Beasts; the cooldown model is TAOM's 2026-06-10
    // rework replacing ADOD_Beasts's per-tick probability roll, so the BT sequences trample → side attacks) ---
    /// <summary>Proximity gate (restored ADOD_Beasts parity; ADOD_Beasts used 3f). An attack only fires when a live enemy is
    /// within this distance of the elephant's CENTER and in front of it — roughly 2m ahead of the tusks, since the
    /// agent origin sits ~1m behind the head. Must stay ≤ <see cref="TrampleRadius"/> (the damage radius). Without
    /// this gate the elephant swings its tusks into empty air, overriding its walk cycle (foot-slide).</summary>
    // Scaled with the body (Mike, 2026-09-22): tuned at 3 m for 1.0x, so a bigger elephant's tusks strike what they
    // visibly reach rather than swinging short.
    public const float TrampleTriggerRange = 3f * AuthoredScale;
    /// <summary>The elephant must face the direction of its nearest enemy: dot(toEnemy, lookDir) above this (ADOD_Beasts:0.25f).</summary>
    public const float TrampleFacingDot = 0.25f;
    /// <summary>Seconds between trample attacks (the big stomp — the priority attack when off cooldown).</summary>
    public const double TrampleCooldownSeconds = 10.0;
    /// <summary>Seconds between side (left/right tusk-swing) attacks — fills the gap while the trample recharges.</summary>
    public const double SideAttackCooldownSeconds = 4.0;
    /// <summary>Radius around the target inside which enemies are trampled (ADOD_Beasts:2f; raised to 4f to match elephant footprint).</summary>
    // Scaled with the body, like the trigger range, so the trigger stays inside it.
    public const float TrampleRadius = 4f * AuthoredScale;
    // --- Per-hit randomized damage (2026-06-15) — replaced ADOD_Beasts's fixed `round(10 * mult) * 2 = 20`
    // with distinct per-kind bands rolled per victim, so a war elephant feels lethal. The roll is supplied by the
    // BT node (MBRandom.RandomFloat) into the pure service. A shield block scales the rolled value by
    // BlockedDamageMultiplier (ADOD_Beasts's 0.25 quarter, now named); its `* 2` doubling is gone
    // because damage is expressed directly. TrampleBlowMagnitude stays the knockback impulse, not HP.
    /// <summary>Minimum trample (radial stomp) damage before block scaling.</summary>
    public const int TrampleMinDamage = 50;
    /// <summary>Maximum trample (radial stomp) damage before block scaling.</summary>
    public const int TrampleMaxDamage = 100;
    /// <summary>Minimum tusk (side-swing) damage before block scaling.</summary>
    public const int TuskMinDamage = 50;
    /// <summary>Maximum tusk (side-swing) damage before block scaling.</summary>
    public const int TuskMaxDamage = 75;
    /// <summary>Damage multiplier applied when the victim is shield-blocking (ADOD_Beasts parity quarter).</summary>
    public const float BlockedDamageMultiplier = 0.25f;
    /// <summary>Blow magnitude passed to the damage primitive — knockback impulse, independent of inflicted HP (TAOM default).</summary>
    public const float TrampleBlowMagnitude = 50f;

    // --- Attack clip mapping — VERIFIED numerically (2026-06-10) by sampling the Head_08 bone's lateral
    // trajectory per clip frame-window in Blender against the staged source FBX (elephant_anims_all_left.fbx,
    // pack0.tpac frame ranges): attack_1 sweeps right→LEFT (left-target swing), attack_2 mirrors left→RIGHT
    // (right-target swing), attack_3 + attack_4 are near-identical full double-sweep thrashes (both sides) —
    // the natural trample visuals for the radial damage. ADOD_Beasts played 1-3 randomly and never used 4.
    // All four are registered in LOTRLOME action_types.xml + bound in the as_elephant action set.
    /// <summary>The trample (double-sweep thrash) animation.</summary>
    public const string TrampleActionName = "act_elephant_attack_3";
    /// <summary>Trample variant (near-identical double sweep) — alternated randomly with the primary for variety.</summary>
    public const string TrampleAltActionName = "act_elephant_attack_4";
    /// <summary>Left tusk-swing animation (strike sweeps toward the elephant's LEFT — played for a left-bearing target).</summary>
    public const string SideAttackLeftActionName = "act_elephant_attack_1";
    /// <summary>Right tusk-swing animation (strike sweeps toward the elephant's RIGHT — played for a right-bearing target).</summary>
    public const string SideAttackRightActionName = "act_elephant_attack_2";
}
