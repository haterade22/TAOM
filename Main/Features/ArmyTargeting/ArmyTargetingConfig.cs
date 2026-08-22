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

    /// <summary>
    /// Ordered axis of advance per kingdom. A list says WHERE a faction wants to go and in what
    /// order; it makes no claim about what it can reach today. Do not delete an entry for being
    /// currently far: reach is anchored on current holdings, so a far entry becomes near the moment
    /// the fief before it falls, and deleting one re-steepens the boost curve for every survivor.
    /// </summary>
    public Dictionary<string, List<string>> FactionPriorityTargets { get; set; } = new();

    public Dictionary<string, float> FactionAggressionMultipliers { get; set; } = new();

    /// <summary>
    /// How far, in town gaps, Patch22 may reach when it overturns vanilla's "unreachable" verdict
    /// for a priority-list target.
    ///
    /// <para><b>This is not a general distance penalty, and TAOM does not apply one.</b> Vanilla's
    /// besieger term is <c>MBMath.Map((5G - d)/G, 0f, 5f, 0.9f, 10f)</c>, which already ramps 10.0
    /// at zero distance down to 0.9 at five gaps, and <c>CalculateDistanceScoreForBesieging</c>
    /// hard-zeroes anything scoring under 0.1 topology. The only distance decision TAOM owns is
    /// whether to overturn that veto for an authored priority target, which is what this bounds.
    /// 3.2 gaps clears the widest measured genuine front (Lothlorien to Gundabad, 3.08 gaps by the
    /// engine's own path cache) without admitting the whole map.</para>
    /// </summary>
    public float BorderRescueRadiusInTownGaps { get; set; } = 3.2f;

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
