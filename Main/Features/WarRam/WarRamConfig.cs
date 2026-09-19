namespace TAOM.Features.WarRam;

/// <summary>
/// Tuning for the Dwarven war ram, a ridden battering-charge mount for mid-tier dwarf cavalry. Unlike
/// the war elephant / Mumakil (giant beasts with their own rig), the ram is authored in
/// LOTRLOME_Armory as base_monster="horse" with its own thin action set as_war_ram (base_set as_horse),
/// inheriting family_type="1", monster_usage="horse" and all twelve rein attributes from the vanilla
/// horse. Locomotion is the vanilla horse's; the one bespoke clip is the head-butt act_war_ram_butt,
/// typed actt_kick (2026-09-18; the vanilla act_horse_kick stood in before). See the clip-mapping block
/// below for why the action TYPE matters and why act_horse_rear and act_horse_strike_front were rejected.
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

    /// <summary>The head-butt's reach: the ram hits ONE enemy inside this radius (see
    /// <see cref="AttackSingleTarget"/>), the one it faces most squarely. Until 2026-09-18 the attack was an
    /// AoE that hit EVERY enemy inside this radius; the radius was 3.5f before that, near the war elephant's 4f,
    /// which made one ram's attack a formation-wide sweep.</summary>
    public const float AttackRadius = 2f;

    /// <summary>The head-butt hits one enemy, not everyone in <see cref="AttackRadius"/> (Mike, 2026-09-18,
    /// #618: "only hit 1 person not AOE"). A test build logged 1,300 head-butt blows from 127 rams in two
    /// Custom Battles, several victims per butt, on top of rams arriving fifteen at a time. The elephant and
    /// mumakil tramples stay radial: they pass nothing, and the profile defaults to the sweep.</summary>
    public const bool AttackSingleTarget = true;

    // --- Per-hit randomized damage ---
    // 40-50 before block scaling since 2026-09-18 (Mike, after the head-butt became single-target; it was 18-28,
    // set when the attack still swept every enemy in the radius). For scale: a mid-tier one-handed weapon swings
    // roughly 25-35 raw BEFORE armor, the warg's bite tops out around 60 (40 base + up to 20 from speed), the
    // elephant's trample is 50-100. Note the butt IGNORES armor: CustomAttacksUtils.TakeDamage writes
    // InflictedDamage directly with DamageCalculated set, so every point lands (measured on the 18-28 band: 976
    // hits on Armored Trolls averaged 23.1, the raw roll). One enemy per butt, one butt per ram per 10 s.
    /// <summary>Minimum head-butt damage before block scaling.</summary>
    public const int AttackMinDamage = 40;

    /// <summary>Maximum head-butt damage before block scaling.</summary>
    public const int AttackMaxDamage = 50;

    /// <summary>Damage multiplier applied when the victim is shield-blocking (elephant-like parity
    /// quarter, kept identical since block-scaling is a shared combat-feel constant, not creature size).</summary>
    public const float BlockedDamageMultiplier = 0.25f;

    /// <summary>Blow magnitude passed to the damage primitive, knockback impulse independent of
    /// inflicted HP. Lower than the war elephant's 50f: a horse-scale creature's kick should
    /// stagger a target, not launch it.</summary>
    public const float AttackBlowMagnitude = 35f;

    // --- Attack clip: the bespoke head-butt. Authored 2026-08-29 on the goat mesh rig, re-authored
    // 2026-09-18 on the engine's own horse_skeleton (tools/blender/transfer_clip_to_engine_rig.py; the
    // four Kit facts that took are in docs/reference/bannerlord-skeleton-authoring.md). Compiled master
    // act_war_ram_butt (30 posed frames plus a 2.5 s hold with the head down, rest frame 0), clip
    // war_ram_butt, bound as act_war_ram_butt in LOTRLOME_Armory action_sets.xml `as_war_ram`
    // (base_set as_horse; the Monster names as_war_ram) and typed actt_kick in the Armory's
    // action_types.xml. Ledger: docs/reference/lotrlome-war-ram-changes.md.
    //
    // WHY A NEW ACTION RATHER THAN RE-POINTING act_horse_kick's animation. The "horse" monster_usage_set
    // the ram inherits names act_horse_kick as its kick_action, so the ENGINE fires that action itself
    // (a horse kicks what stands behind it). Re-pointing its animation would make the engine's rear kick
    // play a forward head-butt. A separate action leaves the engine's kick alone.
    //
    // WHY actt_kick (ActionCodeType.Kick = 28). It is the type the ram has run on since #515, when the
    // attack was the vanilla act_horse_kick: outside Rear (47), which Agent.Mount refuses on channel 0
    // (the ram is the one TAOM mount that is deliberately player-rideable), and outside 48 .. 51, the types
    // Agent.IsInBeingStruckAction reads as BEING STRUCK (MBMath.IsBetween(type, 48, 52) is half-open, so
    // MountStrike = 52 is not in it; corrected 2026-09-18). Two earlier candidates were rejected in review
    // (docs/reviews/rca-war-ram-2026-08-28.md): act_horse_rear for the Rear lock, and act_horse_strike_front /
    // _back because their clips are the horse's hit reactions, not because of their type; the warg's own
    // actt_mount_strike attacks play.
    //
    // The action now lives in the UNVERSIONED Armory, not in Native: a module reinstall drops it and
    // ActionIndexCache resolves act_none. WarRamMissionBehavior's drift guard logs that at mission start.
    //
    // ALL FOUR profile slots hold this one action deliberately. The ram has exactly one attack, and
    // ElephantLikeCombatProfile.IsAttack ORs across all four to answer "am I mid-attack". Collapsing
    // them makes that question mean exactly "is the ram mid-kick". Parking spare slots on unrelated
    // engine-driven actions silently widens the busy-check: a previous revision pointed them at the
    // strike actions, so an engine-driven hit reaction suppressed the ram's own attack for that tick.
    /// <summary>The head-butt: the ram's only attack. Typed actt_kick, bound in the Armory's as_war_ram.</summary>
    public const string AttackActionName = "act_war_ram_butt";

    /// <summary>The Armory action set the ram Monster names (action_set="as_war_ram" in
    /// lotr_monster_war_ram.xml). The drift guard looks it up by id to prove the binding and clip exist.</summary>
    public const string ActionSetId = "as_war_ram";

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
