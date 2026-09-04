namespace TAOM.Features.WarRam;

/// <summary>
/// Tuning for the Dwarven war ram, a ridden battering-charge mount for mid-tier dwarf cavalry. Unlike
/// the war elephant / Mumakil (giant beasts with their own rig), the ram is authored in
/// LOTRLOME_Armory as base_monster="horse" (action_set="as_horse"), inheriting family_type="1",
/// monster_usage="horse" and all twelve rein attributes from the vanilla horse. It reuses VANILLA HORSE
/// ANIMATION only, no new clip was authored: the ram's single attack plays act_horse_kick, bound
/// within action_set id="as_horse" and typed actt_kick. See the clip-mapping block below for why the
/// action TYPE matters and why both act_horse_rear and act_horse_strike_front were rejected.
///
/// The profile's alt/side slots exist only because <see cref="TAOM.Features.ElephantLike.BehaviorTreeElements.ElephantLikeCombatProfile"/>'s
/// constructor requires four clip names. WarRamBehaviorTree wires ONLY the kick-attack branch (no
/// side-attack sequence, see its remarks), so all four slots hold the ram's single attack action:
/// they are never fired, but IsAttack reads them, so they must not name unrelated engine actions.
///
/// No mount-lock: unlike the elephant/spider/Mumakil, the war ram is a player-rideable culture mount
/// (there is a shipping ram_rider career for it), so TaomAgentStatCalculateModel does NOT gate it and
/// this config deliberately carries no MountDifficulty constant.
/// </summary>
public static class WarRamConfig
{
    /// <summary>The war ram Monster's StringId, matches Monster id="taom_war_ram" in LOTRLOME_Armory.</summary>
    public const string WarRamMonsterId = "taom_war_ram";

    // --- AI attack gate (elephant-like pattern; ONE kick attack, no side-attack branch is wired) ---
    /// <summary>Proximity gate: an attack only fires when a live enemy is within this distance of the ram's
    /// CENTER and in front of it. Must stay &lt;= <see cref="AttackRadius"/>, and that is load-bearing
    /// rather than advisory: ElephantLikeEngageDecorator runs ONE scan at AttackRadius and then filters
    /// the results by this value, so a trigger range above the radius is silently dead code and the
    /// constant would read as a number the ram never uses.
    ///
    /// Held at 75% of <see cref="AttackRadius"/>, the ratio the original 2.5/3.5 tuning chose. Keeping
    /// the ram's commit point inside the circle it damages matters MORE at the current 10s cooldown
    /// than it did at 6s: an attack committed against a target loitering on the rim is a wasted
    /// 10-second window. Tunable from battle feel.</summary>
    public const float AttackTriggerRange = 1.5f;

    /// <summary>The ram must face its nearest enemy: dot(toEnemy, lookDir) above this (elephant-like
    /// parity: 0.25f, kept identical since this gate is not scale-dependent).</summary>
    public const float AttackFacingDot = 0.25f;

    /// <summary>Seconds between kick attacks. Level with the war elephant's 10s trample. It was 6s,
    /// on the reasoning that a horse-scale creature recovers faster than a giant, and in play that
    /// read as overpowered for a reason the per-hit numbers hide: unlike an elephant, rams arrive
    /// fifteen at a time in a lord's party, so a short cooldown on a knockdown AoE compounds across
    /// the stack rather than across one beast.</summary>
    public const double AttackCooldownSeconds = 10.0;

    /// <summary>Radius around the ram inside which enemies take the kick (also the single scan radius).
    ///
    /// This is an AoE, not a reach: ElephantLikeAttackTasks sweeps EVERY enemy inside it and knocks
    /// down each one that is not shield-blocking. It was 3.5f, near the base war elephant's 4f, which
    /// made a single ram's kick a formation-wide sweep. 2f keeps the kick to what the ram is actually
    /// standing on top of.</summary>
    public const float AttackRadius = 2f;

    // --- Per-hit randomized damage ---
    // A war ram is not a war elephant. The elephant's trample (50-100) represents a multi-ton beast
    // flattening a formation; the ram carries ONE mid-tier dwarf rider and lands a single horned
    // kick at whatever is directly in front of it, not a stomp. For scale: a mid-tier one-handed
    // weapon swings roughly 25-35 raw before armor, and the warg's bite (a comparable predator-scale
    // single-target hit) tops out around 60 (40 base + up to 20 from speed). The ram sits below both:
    // a genuine bonus threat layered on top of the rider's own attacks, not a beast the rider is along
    // for the ride on. 18-28 before block scaling.
    /// <summary>Minimum kick damage before block scaling.</summary>
    public const int AttackMinDamage = 18;

    /// <summary>Maximum kick damage before block scaling.</summary>
    public const int AttackMaxDamage = 28;

    /// <summary>Damage multiplier applied when the victim is shield-blocking (elephant-like parity
    /// quarter, kept identical since block-scaling is a shared combat-feel constant, not creature size).</summary>
    public const float BlockedDamageMultiplier = 0.25f;

    /// <summary>Blow magnitude passed to the damage primitive, knockback impulse independent of
    /// inflicted HP. Lower than the war elephant's 50f: a horse-scale creature's kick should
    /// stagger a target, not launch it.</summary>
    public const float AttackBlowMagnitude = 35f;

    // --- Attack clip mapping: a REUSED vanilla horse clip, nothing was authored. The ram inherits
    // as_horse (base_monster="horse"), so its attack must be one of that rig's own actions.
    //
    // THE HORSE RIG HAS NO HEADBUTT, AND ONLY ONE GENUINELY OFFENSIVE ACTION. Vanilla horses do not
    // have attack animations at all: they deal damage through charge collision, so monster_usage_strikes
    // is the mount's hit-REACTION table, not an attack table. Two candidates were tried and rejected
    // before this one, both caught in review, and the reasons are worth keeping:
    //
    //   * act_horse_rear is typed actt_rear (ActionCodeType.Rear = 47). The "horse" usage set the ram
    //     inherits declares rear_action="act_horse_rear", so the ENGINE fires it on a damaged mount,
    //     and Agent.Mount refuses a mount whose channel-0 type is Rear. Forcing it every cooldown
    //     would have made the ram briefly UNMOUNTABLE mid-fight, on the one TAOM mount that is
    //     deliberately player-rideable (there is a shipping ram_rider career for it).
    //   * act_horse_strike_front / _back are typed actt_mount_strike (ActionCodeType.MountStrike = 52).
    //     That sits inside StrikeBegin = 48 .. StrikeEnd = 52, the band Agent.IsInBeingStruckAction
    //     treats as BEING STRUCK. The clips are named horse_hit_from_front / _back accordingly. Playing
    //     one makes the ram flinch as though hit while we emit damage.
    //
    // act_horse_kick is typed actt_kick (ActionCodeType.Kick = 28): outside the being-struck band and
    // outside Rear, so it is the horse rig's only real offensive action. It reads as a buck or kick
    // rather than a head strike, which is a deliberate accepted compromise: a correct kick would
    // mean authoring a clip in Blender plus the Modding Kit and reopening the whole animation pipeline
    // this reskin exists to avoid. Revisit if a bespoke clip is ever authored.
    //
    // ALL FOUR profile slots hold this one action deliberately. The ram has exactly one attack, and
    // ElephantLikeCombatProfile.IsAttack ORs across all four to answer "am I mid-attack". Collapsing
    // them makes that question mean exactly "is the ram mid-kick". Parking spare slots on unrelated
    // engine-driven actions silently widens the busy-check: a previous revision pointed them at the
    // strike actions, so an engine-driven hit reaction suppressed the ram's own attack for that tick.
    /// <summary>The kick animation: the ram's only attack. Typed actt_kick.</summary>
    public const string AttackActionName = "act_horse_kick";

    /// <summary>Alt slot for ElephantLikeCombatProfile's 50/50 variety pick. The ram has one attack
    /// clip, so this repeats the primary: a same-clip "alternate" is an honest no-op rather than a
    /// fabricated difference.</summary>
    public const string AttackAltActionName = AttackActionName;

    /// <summary>Side-attack slot the profile ctor requires. WarRamBehaviorTree wires no side-attack
    /// branch, so it is never FIRED, but it is still read by IsAttack, so it must be the ram's own
    /// attack rather than an unrelated action.</summary>
    public const string SideSlotLeftActionName = AttackActionName;

    /// <summary>Second side-attack slot, see <see cref="SideSlotLeftActionName"/>.</summary>
    public const string SideSlotRightActionName = AttackActionName;
}
