using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine writes and reads that are not the field itself: what the director publishes into the engine's hooks, and what it reads back
/// to prove a number reached the agents. Takes and returns domain types only (ADR-007). Main thread. Slice 2 holds the damage factor and
/// the scale read; the stat refresh, morale and poison members of the plan join it in their own slices.
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
}
