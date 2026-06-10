namespace TAOM.Features.Elephant;

/// <summary>
/// Tuning for the Harad war-elephant. Values are a 1-for-1 port of ADOD_Beasts'
/// <c>ADODBeastsElephantAgentComponent.OnTickAsAI</c> trample + <c>ADODAgentStatCalculateModel</c> mount-lock
/// (decompiled 2026-06-05). "1 for 1 then improve": the trample is a fixed radial knockdown like ADOD's;
/// velocity-scaling / bone-collision polish is the later improve step. See docs/features/elephant.md.
/// </summary>
public static class ElephantConfig
{
    /// <summary>The elephant Monster's StringId — matches Monster id="taom_war_elephant" in LOTRLOME_Armory.</summary>
    public const string ElephantMonsterId = "taom_war_elephant";

    /// <summary>HorseHarness item StringId that triggers howdah instantiation (sk_elephant_armor_a in LOTRLOME_Armory).</summary>
    public const string HarnessStringId = "sk_elephant_armor_a";

    /// <summary>Z offset above the mahout rider's root position for howdah entity placement.
    /// Tunable — start at 0.8f; increase if the seat appears below the elephant's back surface.</summary>
    public const float HowdahHeightAboveRider = 0.8f;

    /// <summary>Z offset above the elephant agent's ground position when the rider is absent (fallback).</summary>
    public const float HowdahHeightAboveGround = 3.2f;

    /// <summary>CharacterObject StringId force-spawned into the howdah seat on mahout build.
    /// Vanilla detachment cannot path to a moving machine — crew must be spawned directly.</summary>
    public const string HowdahCrewCharacterId = "harad_archer";

    /// <summary>MountDifficulty forced on the elephant so non-rider AI can't take it (ADOD: 999f).</summary>
    public const float MountDifficulty = 999f;

    // --- AI attack gates (facing/range/damage are 1-for-1 ADOD; the cooldown model is TAOM's 2026-06-10
    // rework replacing ADOD's per-tick probability roll, so the BT sequences trample → side attacks) ---
    /// <summary>Proximity gate (restored ADOD parity; ADOD used 3f). An attack only fires when a live enemy is
    /// within this distance of the elephant's CENTER and in front of it — roughly 2m ahead of the tusks, since the
    /// agent origin sits ~1m behind the head. Must stay ≤ <see cref="TrampleRadius"/> (the damage radius). Without
    /// this gate the elephant swings its tusks into empty air, overriding its walk cycle (foot-slide).</summary>
    public const float TrampleTriggerRange = 3f;
    /// <summary>The elephant must face the direction of its nearest enemy: dot(toEnemy, lookDir) above this (ADOD: 0.25f).</summary>
    public const float TrampleFacingDot = 0.25f;
    /// <summary>Seconds between trample attacks (the big stomp — the priority attack when off cooldown).</summary>
    public const double TrampleCooldownSeconds = 10.0;
    /// <summary>Seconds between side (left/right tusk-swing) attacks — fills the gap while the trample recharges.</summary>
    public const double SideAttackCooldownSeconds = 4.0;
    /// <summary>Radius around the target inside which enemies are trampled (ADOD: 2f; raised to 4f to match elephant footprint).</summary>
    public const float TrampleRadius = 4f;
    /// <summary>Base trample damage; halved-to-a-quarter vs blocking targets, doubled for inflicted (ADOD: 10).</summary>
    public const int TrampleBaseDamage = 10;
    /// <summary>Blow magnitude passed to the damage primitive (ADOD blows use ~the base magnitude; 50f is TAOM's default).</summary>
    public const float TrampleBlowMagnitude = 50f;

    // --- Attack clip mapping — VERIFIED numerically (2026-06-10) by sampling the Head_08 bone's lateral
    // trajectory per clip frame-window in Blender against the staged source FBX (elephant_anims_all_left.fbx,
    // pack0.tpac frame ranges): attack_1 sweeps right→LEFT (left-target swing), attack_2 mirrors left→RIGHT
    // (right-target swing), attack_3 + attack_4 are near-identical full double-sweep thrashes (both sides) —
    // the natural trample visuals for the radial damage. ADOD played 1-3 randomly and never used 4.
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
