namespace TAOM.Features.Mumakil;

/// <summary>
/// Tuning for the Mûmakil — a giant ridden Harad war beast (the Oliphaunt). It is a SCALED-UP variant of the
/// <see cref="TAOM.Features.Elephant"/>: it shares the elephant rig (<c>elephant_skeleton</c>), the
/// <c>as_elephant</c> action set, and every elephant animation clip — including the four
/// <c>act_elephant_attack_*</c> clips this BT plays. Size comes entirely from the Horse item's
/// <c>BodyLength</c> (the engine scales a mount via <c>SetInitialAgentScale(0.01f * BodyLength)</c>), NOT from
/// any Monster-XML field. Behaviorally identical to the elephant (auto trample + tusk on deterministic
/// cooldowns); cloned per the project-owner's one-feature-per-creature convention (spider/chariot precedent).
///
/// Phase 1 = ridden mount with ONE rider that auto-attacks. No platform crew, no howdah. The platform is baked
/// into the mesh (visual only). See docs/features/mumakil.md.
/// </summary>
public static class MumakilConfig
{
    /// <summary>The Mûmakil Monster's StringId — matches Monster id="taom_mumakil" in LOTRLOME_Armory.</summary>
    public const string MumakilMonsterId = "taom_mumakil";

    /// <summary>MountDifficulty forced on the Mûmakil so non-rider AI can't take it (elephant/spider parity: 999f).</summary>
    public const float MountDifficulty = 999f;

    // --- AI attack gates (1-for-1 with the elephant; cooldown model replaces the donor's per-tick roll) ---
    /// <summary>Proximity gate: an attack only fires when a live enemy is within this distance of the Mûmakil's
    /// CENTER and in front of it. Must stay ≤ <see cref="TrampleRadius"/>. Without it the beast swings into empty
    /// air, overriding its walk cycle (the elephant "slide" class). Scaled ~3× the elephant's 3f to match the 3.0×
    /// body (the agent origin sits ~3× further from the tusks at this scale). Tunable from battle feel.</summary>
    public const float TrampleTriggerRange = 9f;
    /// <summary>The Mûmakil must face its nearest enemy: dot(toEnemy, lookDir) above this (elephant parity: 0.25f).</summary>
    public const float TrampleFacingDot = 0.25f;
    /// <summary>Seconds between trample attacks (the big stomp — the priority attack when off cooldown).</summary>
    public const double TrampleCooldownSeconds = 10.0;
    /// <summary>Seconds between side (left/right tusk-swing) attacks — fills the gap while the trample recharges.</summary>
    public const double SideAttackCooldownSeconds = 4.0;
    /// <summary>Radius around the Mûmakil inside which enemies are trampled. Scaled ~3× the elephant's 4f to cover
    /// the 3.0× body's footprint. Tunable from battle feel.</summary>
    public const float TrampleRadius = 12f;

    // --- Per-hit randomized damage (elephant parity for Phase 1; a giant could hit harder later) ---
    /// <summary>Minimum trample (radial stomp) damage before block scaling.</summary>
    public const int TrampleMinDamage = 50;
    /// <summary>Maximum trample (radial stomp) damage before block scaling.</summary>
    public const int TrampleMaxDamage = 100;
    /// <summary>Minimum tusk (side-swing) damage before block scaling.</summary>
    public const int TuskMinDamage = 50;
    /// <summary>Maximum tusk (side-swing) damage before block scaling.</summary>
    public const int TuskMaxDamage = 75;
    /// <summary>Damage multiplier applied when the victim is shield-blocking (elephant-parity quarter).</summary>
    public const float BlockedDamageMultiplier = 0.25f;
    /// <summary>Blow magnitude passed to the damage primitive — knockback impulse, independent of inflicted HP.</summary>
    public const float TrampleBlowMagnitude = 50f;

    // --- Attack clip mapping — REUSED from the elephant. Because the Mûmakil shares the as_elephant action set
    // (same skeleton), the attack actions ARE the elephant's: attack_3/4 = double-sweep trample, attack_1 = left
    // tusk swing, attack_2 = right tusk swing (roles verified by Blender trajectory analysis for the elephant,
    // 2026-06-10). All four are registered in LOTRLOME action_types.xml + bound in the as_elephant action set. ---
    /// <summary>The trample (double-sweep thrash) animation (shared elephant clip).</summary>
    public const string TrampleActionName = "act_elephant_attack_3";
    /// <summary>Trample variant (near-identical double sweep) — alternated randomly with the primary for variety.</summary>
    public const string TrampleAltActionName = "act_elephant_attack_4";
    /// <summary>Left tusk-swing animation (strike sweeps toward the Mûmakil's LEFT — played for a left-bearing target).</summary>
    public const string SideAttackLeftActionName = "act_elephant_attack_1";
    /// <summary>Right tusk-swing animation (strike sweeps toward the Mûmakil's RIGHT — played for a right-bearing target).</summary>
    public const string SideAttackRightActionName = "act_elephant_attack_2";
}
