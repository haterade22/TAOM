using System.Collections.Generic;

namespace TAOM.Features.ArmyTargeting;

public class ArmyTargetingConfig
{
    /// <summary>
    /// The closed set of legal theater names. Membership entries are validated against this list,
    /// so a typo warns and is skipped rather than silently creating a private theater of one.
    /// </summary>
    public List<string> Theaters { get; set; } = new();

    /// <summary>
    /// Kingdom StringId to its ordered theater list. The FIRST entry is that kingdom's primary
    /// front. An empty list marks a deliberately passive kingdom (bluecraig and lindon sit on a
    /// closed land-navigation island and cannot reach anything).
    ///
    /// A kingdom ABSENT from this map is neutral, not foreign. That is deliberate: player-founded
    /// kingdoms get the runtime StringId "new_kingdom", rebels get "&lt;settlementId&gt;_rebel_clan",
    /// and neither can ever appear in a shipped config. Failing closed would silently make the
    /// player's own realm un-besiegeable.
    /// </summary>
    public Dictionary<string, List<string>> KingdomTheaters { get; set; } = new();

    public Dictionary<string, List<string>> FactionPriorityTargets { get; set; } = new();
    public Dictionary<string, float> FactionAggressionMultipliers { get; set; } = new();

    /// <summary>
    /// Distance, in town gaps, out to which a siege target suffers no reach penalty at all.
    /// Measured genuine fronts on this map sit between 1.58 and 1.95 town gaps (Rohan to Mordor is
    /// 148 units against a 93.95-unit gap), so the inner radius must clear them.
    /// </summary>
    public float ReachInnerRadiusInTownGaps { get; set; } = 1.5f;

    /// <summary>
    /// Distance, in town gaps, at which reach bottoms out at <see cref="ReachFloor"/>. 3 gaps is
    /// 282 map units, about 17 percent above the widest genuine front. Vanilla's own factor never
    /// falls below 0.9x at ANY distance, which is why a far target costs it almost nothing.
    /// </summary>
    public float ReachRadiusInTownGaps { get; set; } = 3.0f;

    /// <summary>Multiplier applied at and beyond <see cref="ReachRadiusInTownGaps"/>. Never zero, so a faction with no near target still ranks its options rather than idling.</summary>
    public float ReachFloor { get; set; } = 0.05f;

    /// <summary>Applied when the target's owner is a member of the attacker's PRIMARY theater.</summary>
    public float PrimaryTheaterWeight { get; set; } = 1.25f;

    /// <summary>Applied when attacker and target share a theater that is not the attacker's primary.</summary>
    public float SecondaryTheaterWeight { get; set; } = 1.0f;

    /// <summary>
    /// Applied when attacker and target share NO theater. Damped, never vetoed: measurement showed
    /// a hard gate severs genuine fronts, and a vetoed kingdom gathers, fails to find a legal
    /// target, patrols, and disbands its army about two days later via Army.CheckInactivity.
    /// </summary>
    public float ForeignTheaterWeight { get; set; } = 0.35f;
}
