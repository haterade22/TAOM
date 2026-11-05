using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine writes and reads that are not the field itself: what the director publishes into the engine's hooks, and what it reads back
/// to prove a number reached the agents. Takes and returns domain types only (ADR-007). Main thread. Slices 2 and 3 hold the wave's published
/// table (damage factor, stat record, armour) and the two reads that prove it; Slice 6 adds the troop and class boons' refresh, health writes and read-back; the morale and poison
/// members of the plan join it in their own slices.
/// </summary>
internal interface ILongDefeatEffectsAdapter
{
    /// <summary>Publishes the wave's numbers into the engine's hooks. One write each, read by the engine's threads from then on.</summary>
    void PublishStats(LdStatTable table);

    /// <summary>
    /// One standing human attacker as the engine holds it now: its troop, its health limit, the maximum its character gives it, and the blows
    /// the damage factor has raised since the wave's factor was published. An empty troop with no attacker standing; the blow count is read either way.
    /// </summary>
    LdScaleSample ReadScale();

    /// <summary>
    /// One standing human attacker that is not running away and has no live race ability, with each named driven property read as the engine holds
    /// it and as the stat model gives the same agent with the affixes left out (an armour property: as its spawn equipment sums to). No attacker, an empty sample.
    /// </summary>
    LdStatSample ReadStats(IReadOnlyList<string> stats);

    /// <summary>
    /// Derives the driven properties of every standing human of one group of the player's side again (and of each one's mount), so a stat record just published reaches soldiers already
    /// built: the engine derives them at spawn and on events only, and its controller setter calls no update. <see cref="BoonTarget.Player"/> is the main agent, a troop group excludes him.
    /// </summary>
    LdTouched RefreshStats(BoonTarget target);

    /// <summary>
    /// Multiplies the health limit of every standing human of a troop group by one boon's own factor, keeping each one's health share. The limit, its base and
    /// the health are the three writes the engine's own horse spawn makes (<c>Mission.cs:4607-4609</c>). Not for the lord: a relative write on a body adopted this tick would apply
    /// the factor twice, so his health is only ever set absolutely (<see cref="SetPlayerHealth"/>).
    /// </summary>
    LdTouched ScaleHealth(BoonTarget target, float factor);

    /// <summary>
    /// Sets the main agent's health limit to its character's maximum times <paramref name="factor"/>, keeping his health share: an absolute write, so a soldier taken over from the
    /// garrison (who spawned with his group's factor) ends with the player's and the lord's own fresh body with the same.
    /// </summary>
    void SetPlayerHealth(float factor);

    /// <summary>
    /// One standing human of the group that is not running away and has no live race ability: the oldest, or with <paramref name="newest"/> the one spawned last (a team's agent list keeps
    /// spawn order), with its health limit and each named driven property as the engine holds it and as the stat model gives the same agent with the boons left out. None when no such soldier stands.
    /// </summary>
    LdTargetSample ReadTarget(BoonTarget target, IReadOnlyList<string> stats, bool newest);
}
