namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// Tuning for the dwarves' head butt and drop kick (#1087). First guesses, tuned in the Custom Battle smoke. The two
/// actions, their bindings in <c>as_dwarf_warrior</c> and their clips live in the UNVERSIONED LOTRLOME_Armory
/// (ledger: docs/reference/lotrlome-dwarf-attack-changes.md; rebuilt by tools/gen_dwarf_attack_clips.ps1 and
/// tools/bind_dwarf_attack_actions.py). Distances are metres between agent positions on the ground plane.
/// </summary>
public static class DwarfAttacksConfig
{
    /// <summary>The dwarf battle Monster, male and female (its <c>female_action_set</c> inherits the warrior set). <c>dwarf_child</c> never matches.</summary>
    public const string DwarfMonsterId = "dwarf";

    /// <summary>The set the Monster names and both actions are bound in.</summary>
    public const string ActionSetId = "as_dwarf_warrior";

    public const string HeadButtActionName = "act_dwarf_head_butt";
    public const string DropKickActionName = "act_dwarf_drop_kick";

    // --- When an attack starts: the nearest enemy's distance picks the attack ---

    /// <summary>The nearest enemy must be in front: strictly above this cosine (about 45 degrees either side).</summary>
    public const float FacingDot = 0.7f;

    /// <summary>A nearest enemy this close gets the head butt, which is keyed with the feet planted.</summary>
    public const float HeadButtMaxRange = 1.3f;

    /// <summary>
    /// A nearest enemy from here to <see cref="DropKickMaxRange"/> gets the drop kick: the clip runs two steps and
    /// carries the dwarf forward. Closer than this there is no room for the run-up, so nothing fires between the bands.
    /// </summary>
    public const float DropKickMinRange = 1.8f;

    public const float DropKickMaxRange = 2.6f;

    /// <summary>Mission seconds between head butts. Separate from the drop kick's, so a dwarf may do one of each.</summary>
    public const float HeadButtCooldownSeconds = 12f;

    /// <summary>Mission seconds between drop kicks: longer, because it floors the victim.</summary>
    public const float DropKickCooldownSeconds = 20f;

    // --- When and where the attack lands ---

    /// <summary>
    /// Clip progress at the head's impact: frame 13 of the 37-frame master, played as Source 1 to 36, so 12 / 35
    /// (tools/blender/author_dwarf_attack_clips.py, author_report.json).
    /// </summary>
    public const float HeadButtImpactFraction = 12f / 35f;

    /// <summary>Clip progress at full leg extension: frame 21 of the 55-frame master, Source 1 to 54, so 20 / 53.</summary>
    public const float DropKickImpactFraction = 20f / 53f;

    /// <summary>
    /// The drop kick clip's <c>DisplacementUsage</c>: <see cref="DropKickTravel"/> metres forward, complete at progress
    /// <see cref="DropKickTravelEndFraction"/> (31 / 53, tools/gen_dwarf_attack_clips.ps1). The tuning checks use it;
    /// the code does not move the dwarf itself.
    /// </summary>
    public const float DropKickTravel = 2.0f;

    public const float DropKickTravelEndFraction = 31f / 53f;

    /// <summary>At impact, the victim is the enemy within this distance of the dwarf, faced most squarely.</summary>
    public const float HeadButtReach = 1.5f;

    /// <summary>The drop kick's reach from where the dwarf is at impact, about 1.3 m into his travel.</summary>
    public const float DropKickReach = 1.6f;

    /// <summary>At impact a victim must be in front: strictly above this cosine (about 60 degrees either side).</summary>
    public const float HitFacingDot = 0.5f;

    /// <summary>
    /// The largest height difference between the dwarf and an enemy he may target or hit, in metres. The engine's nearby
    /// query is flat (it takes a Vec2 centre), so without this a dwarf on a wall would butt a man on a ladder below it.
    /// </summary>
    public const float MaxHeightDifference = 1.0f;

    /// <summary>
    /// An idle dwarf scans for a target once a period, from this minimum up to the minimum plus the spread less 1 ms.
    /// Each dwarf gets his own period (by build order), so a battle's scans do not all land in one frame.
    /// </summary>
    public const int IdleScanMinMilliseconds = 250;
    public const int IdleScanSpreadMilliseconds = 100;

    // --- What the victim takes ---
    // Blunt, through CustomAttacksUtils.TakeDamage, which writes the damage directly: armour does not reduce it (the
    // war ram's measured behaviour, WarRamConfig). For scale, the war ram's butt is 40 to 50 and a mid-tier one-handed
    // swing about 25 to 35 before armour.

    public const int HeadButtDamage = 18;
    public const int DropKickDamage = 25;

    /// <summary>A victim blocking with a shield takes this share and is never floored.</summary>
    public const float ShieldBlockedMultiplier = 0.5f;

    /// <summary>Knock-back impulse, independent of damage: the butt staggers, the kick throws.</summary>
    public const float HeadButtBlowMagnitude = 25f;

    public const float DropKickBlowMagnitude = 45f;

    /// <summary>The MCM damage slider: a percentage of the damage above, 0 turns the hits off.</summary>
    public const int DefaultDamagePercent = 100;

    public const int MaxDamagePercent = 300;
}
