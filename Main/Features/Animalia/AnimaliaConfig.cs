using TaleWorlds.Core;

namespace TAOM.Features.Animalia;

/// <summary>
/// Tuning for the Animalia elk and moose's antler attacks (#646, docs/features/animalia-elk-moose.md). Both animals
/// are horse-skeleton reskins of the Fab Animalia packs with their OWN clips, so unlike the great elk (#636), which
/// borrows the war ram's head-butt, each attack is its own action bound to its own clip in its own action set
/// (as_animalia_elk / as_animalia_moose in LOTRLOME_Armory), typed actt_kick in action_types.xml as
/// act_war_ram_butt is. Not act_horse_kick: the inherited horse usage set fires that itself as its kick_action.
///
/// The attack is the great elk's shape: one blow on the one enemy the animal faces most squarely (the ram's #618
/// single-target rule), fired automatically under any rider, scaled by the rider's career charge bonus. The reach is
/// the war ram's 1.5 m trigger and 2 m radius at 1.0x, and the shared nodes multiply it by each animal's live size,
/// which lives on its Monster (taom_body_length, docs/features/monster-size.md).
/// </summary>
public static class AnimaliaConfig
{
    /// <summary>Monster ids in the Armory's lotr_monster_animalia.xml. The attach key: the mount agent has no Character.</summary>
    public const string ElkMonsterId = "taom_animalia_elk";
    public const string MooseMonsterId = "taom_animalia_moose";

    /// <summary>The action sets those Monsters name; the drift guard looks each up to prove the attack clip is bound.</summary>
    public const string ElkActionSetId = "as_animalia_elk";
    public const string MooseActionSetId = "as_animalia_moose";

    /// <summary>Each animal's size lives on its Monster (taom_body_length: the elk 100, the moose 150 since Mike's
    /// "way bigger", 2026-09-23), which MonsterSizeService copies into its Horse item at game init. The ranges below
    /// are the reach at 1.0x, and the shared nodes multiply them by the animal's live size (this flag).</summary>
    public const bool ReachScalesWithBody = true;

    /// <summary>The war ram's reach at 1.0x: the trigger gate and the hit radius, measured from the animal's centre.
    /// The trigger must stay inside the radius: ElephantLikeEngageDecorator scans once at the radius and filters by
    /// the trigger range, so a larger trigger is unreachable.</summary>
    public const float AttackTriggerRange = 1.5f;
    public const float AttackRadius = 2f;

    /// <summary>One victim per attack, the enemy the animal faces most squarely (the ram's #618 rule).</summary>
    public const bool AttackSingleTarget = true;

    /// <summary>The animal must face its target: dot(toEnemy, lookDir) above this (elephant-like parity).</summary>
    public const float AttackFacingDot = 0.25f;

    /// <summary>Seconds between attacks: the great elk's and the ram's 10 s, well past both clips (the elk's
    /// attack_front_low plays 2.5 s, the moose's attack_head_01 1.57 s: tools/gen_animalia_anim_clips.ps1).</summary>
    public const double AttackCooldownSeconds = 10.0;

    /// <summary>One blow each. The elk hits as the great elk does (60); the moose, the heavier animal, harder (70).
    /// Armour does not reduce it (CustomAttacksUtils.TakeDamage writes it directly); a shield block quarters it.</summary>
    public const int ElkAttackDamage = 60;
    public const int MooseAttackDamage = 70;

    /// <summary>Blunt, lethal (TakeDamage marks a Blunt blow CanKillEvenIfBlunt), as the great elk's.</summary>
    public const DamageTypes AttackDamageType = DamageTypes.Blunt;

    /// <summary>Damage multiplier when the victim is shield-blocking (elephant-like parity).</summary>
    public const float BlockedDamageMultiplier = 0.25f;

    /// <summary>Knockback impulse, independent of the damage: the ram's and great elk's 35 for the elk, more for the
    /// moose, both below the war elephant's 50.</summary>
    public const float ElkBlowMagnitude = 35f;
    public const float MooseBlowMagnitude = 45f;

    /// <summary>The attack actions (action_types.xml, actt_kick) and the clips the action sets bind them to.</summary>
    public const string ElkAttackActionName = "act_animalia_elk_antler";
    public const string MooseAttackActionName = "act_animalia_moose_antler";
    public const string ElkAttackClip = "anim_animalia_elk_attack_front_low";
    public const string MooseAttackClip = "anim_animalia_moose_attack_head_01";
}
