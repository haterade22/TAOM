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
    /// Distance, in town gaps, out to which a siege target suffers no reach penalty at all. The
    /// widest genuine hostile front measured on this map is about 2.95 gaps, so this clears every
    /// real war and the falloff only bites past them.
    /// </summary>
    public float ReachInnerRadiusInTownGaps { get; set; } = 3.0f;

    /// <summary>
    /// Distance, in town gaps, at which reach bottoms out at <see cref="ReachFloor"/>.
    ///
    /// <para><b>Sized against vanilla, not in a vacuum.</b> Vanilla's own besieger distance term is
    /// <c>MBMath.Map((5G - d)/G, 0, 5, 0.9f, 10f)</c>: it already spans 10.0 at zero distance down
    /// to 0.9 at five gaps, an 11.1x ramp, and <c>CalculateDistanceScoreForBesieging</c> hard-zeroes
    /// any target whose two-hop topology score is under 0.1. Vanilla is therefore NOT flat with
    /// distance, and TAOM's term multiplies on top of it rather than replacing it.</para>
    /// </summary>
    public float ReachRadiusInTownGaps { get; set; } = 6.0f;

    /// <summary>
    /// Multiplier applied at and beyond <see cref="ReachRadiusInTownGaps"/>.
    ///
    /// <para>Derived, not picked: TAOM's own multipliers can stack to 3.75x on a far target
    /// (priority 3.0 x primary theater 1.25) against vanilla's 2.2x distance penalty at three gaps,
    /// so a floor near 2.2/3.75 = 0.59 is where TAOM stops outrunning vanilla. 0.35 leaves a margin
    /// without turning a 2.2x vanilla penalty into the 44x it became when this was 0.05.</para>
    ///
    /// <para>Never zero, so a faction with no near target still ranks its options rather than
    /// idling and losing its army to <c>Army.CheckInactivity</c>.</para>
    /// </summary>
    public float ReachFloor { get; set; } = 0.35f;

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
