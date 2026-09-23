using TaleWorlds.Core;

namespace TAOM.Features.Elk;

/// <summary>
/// Tuning for the great elk, Thranduil's mount and the Mirkwood cavalry's (#636). Built exactly as the Dwarven
/// war ram (docs/features/war-ram.md): elk_001 is skinned to the vanilla horse_skeleton, so its Monster in
/// LOTRLOME_Armory (taom_elk) is base_monster="horse" and it moves on the horse's own clips, inheriting
/// family_type="1", monster_usage="horse" and all twelve rein attributes.
///
/// Its one attack, the antler charge, is the ram's head-butt. The Monster names the ram's action set
/// as_war_ram, so act_war_ram_butt (clip war_ram_butt, authored on the engine horse_skeleton, typed actt_kick)
/// plays on the elk unchanged, and the same head-down pose lowers the antlers. Why that action and type and not
/// act_horse_rear or act_horse_strike_*: see <see cref="TAOM.Features.WarRam.WarRamConfig"/>. The literals here
/// name what the elk's own Monster names, not the ram's constants: if the ram ever moves to a set of its own,
/// the elk stays on as_war_ram until its Monster is changed with it.
///
/// Tuning began equal to the ram's and lives here so the two can be tuned apart; the reach (grown with the 2x body)
/// and the damage (one 60 Blunt blow, scaled by the rider's career charge bonus) now differ. All four profile slots
/// hold the one attack for the reason WarRamConfig gives: IsAttack ORs across them, so any other action there would
/// widen "am I mid-charge" to an unrelated engine-driven action.
///
/// No mount-lock: the elk_rider career hands the elk to a starting player, so TaomAgentStatCalculateModel does
/// not gate it and there is no MountDifficulty constant here.
/// </summary>
public static class ElkConfig
{
    /// <summary>The elk Monster's StringId, matching Monster id="taom_elk" in LOTRLOME_Armory.</summary>
    public const string ElkMonsterId = "taom_elk";

    /// <summary>
    /// The size the elk is built at: taom_elk_a's Horse <c>body_length</c> / 100, which the engine applies at build
    /// (<c>SetInitialAgentScale</c>) to the elk's skeleton, clips, capsules and every mesh on it, the saddle included.
    /// The rider is NOT scaled (observed in game on the 3x mumakil, docs/features/mumakil.md). 2.0 since 2026-09-23
    /// (Mike: "x2 the size ... maybe even bigger"). The reach below is derived from it because nothing scales a
    /// fixed metre for us; ElkConfigTests pins the Armory's body_length to this constant, so change both together.
    /// </summary>
    public const float AuthoredScale = 2.0f;

    /// <summary>Proximity gate: the charge fires only when a live enemy is within this distance of the elk's
    /// CENTER and in front of it. Must stay &lt;= <see cref="AttackRadius"/>: ElephantLikeEngageDecorator scans
    /// ONCE at the radius and filters by this value, so anything larger is unreachable. 75% of the radius, the
    /// ram's ratio. Tuned at 1.0x as the ram's 1.5 m and scaled with the body, so the antlers strike what they
    /// visibly reach (the ElephantConfig rule).</summary>
    public const float AttackTriggerRange = 1.5f * AuthoredScale;

    /// <summary>The elk must face its target: dot(toEnemy, lookDir) above this (elephant-like parity).</summary>
    public const float AttackFacingDot = 0.25f;

    /// <summary>Seconds between antler charges. Must outlast the 3.5 s clip (butt plus head-down hold), or the
    /// tree restarts the charge mid-hold. The ram's 10 s: Mirkwood cavalry come in stacks like the ram's.</summary>
    public const double AttackCooldownSeconds = 10.0;

    /// <summary>The charge's reach: it hits ONE enemy inside this radius (<see cref="AttackSingleTarget"/>). The ram's
    /// 2 m at 1.0x, scaled with the body like the trigger range so the trigger stays inside it.</summary>
    public const float AttackRadius = 2f * AuthoredScale;

    /// <summary>One victim per charge, the enemy the elk faces most squarely (the ram's #618 rule).</summary>
    public const bool AttackSingleTarget = true;

    /// <summary>The antler charge's damage, one blow (Mike, 2026-09-23: first "40 blunt and 20 piercing", then "one 60
    /// blunt blow" once shown the two land alike). Armour does not reduce it (CustomAttacksUtils.TakeDamage writes it
    /// directly); a shield block quarters it (<see cref="BlockedDamageMultiplier"/>) and the rider's career charge bonus
    /// scales it (<c>ElkCombat.RiderChargeMultiplier</c>: "scale the antler blows too").</summary>
    public const int AttackDamage = 60;

    /// <summary>The antler charge lands Blunt. TakeDamage marks a Blunt blow CanKillEvenIfBlunt, because a Blunt killing
    /// blow otherwise only wounds (Mike chose "keep it lethal").</summary>
    public const DamageTypes AttackDamageType = DamageTypes.Blunt;

    /// <summary>Damage multiplier when the victim is shield-blocking (elephant-like parity).</summary>
    public const float BlockedDamageMultiplier = 0.25f;

    /// <summary>Knockback impulse passed to the damage primitive, independent of inflicted HP. The ram's 35f,
    /// below the war elephant's 50f: a horse-scale beast staggers its target rather than launching it.</summary>
    public const float AttackBlowMagnitude = 35f;

    /// <summary>The antler charge: the ram's head-butt, bound in the Armory's as_war_ram and typed actt_kick.</summary>
    public const string AttackActionName = "act_war_ram_butt";

    /// <summary>The Armory action set the elk Monster names (action_set="as_war_ram" in lotr_monster_elk.xml).
    /// The drift guard looks it up by id to prove the binding and clip exist.</summary>
    public const string ActionSetId = "as_war_ram";

    /// <summary>Alt slot for the profile's 50/50 variety pick; the elk has one attack, so it repeats it.</summary>
    public const string AttackAltActionName = AttackActionName;

    /// <summary>Side-attack slot the profile ctor requires. Never fired (no side-attack branch), but IsAttack reads it.</summary>
    public const string SideSlotLeftActionName = AttackActionName;

    /// <summary>Second side-attack slot, see <see cref="SideSlotLeftActionName"/>.</summary>
    public const string SideSlotRightActionName = AttackActionName;
}
