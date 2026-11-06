using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine work of the Long Defeat fortifications (phase3-plan.md section 4), kept apart from <see cref="ILongDefeatBattlefieldAdapter"/> so the director's other fake stays small. The battlefield adapter
/// implements both, and the run host hands the director the same instance as each (<c>LongDefeatFortWiringTests</c>). Takes and returns domain types only (ADR-007). Main thread. Slice 5 holds the gates, the fort
/// points and the A23 boot line; the posts and the volleys join it with their slices.
/// </summary>
internal interface ILongDefeatFortAdapter
{
    /// <summary>
    /// The visible gates as the field reads them now, in the order of the boot list, read from the gate objects themselves (a fallen gate has left the active list, so never from it). Empty before the boot.
    /// </summary>
    IReadOnlyList<LdGateFacts> ReadGates();

    /// <summary>
    /// A gate level (D18): every visible gate's maximum becomes its vanilla maximum times <paramref name="factor"/>, and a standing gate keeps its share of hit points (<see cref="GateMath.KeepShare"/>); a
    /// broken gate stays broken. Writes only gates, never a ram or a tower (D19). Returns each gate before and after.
    /// </summary>
    LdGateWrite SetGateLevel(float factor);

    /// <summary>
    /// The repair (D17): every visible gate missing at least <see cref="GateMath.MinMissingShare"/> of its maximum is made whole at its vanilla maximum times <paramref name="factor"/>, closed and enabled, by
    /// vanilla's own mission reset. A gate that is whole is left alone, and the hidden gates stay broken. Returns each gate before and after, with what the check reads.
    /// </summary>
    LdGateWrite RepairGates(float factor);

    /// <summary>
    /// How many standing attackers are within <see cref="GateRules.BehindDepth"/> behind a standing visible gate's plane (the inside is the side <paramref name="inside"/> lies on), within the door's width and
    /// <see cref="GateRules.HeightBand"/> of its height, summed over the gates. -1 when no standing gate had a plane to measure (no inside point, a frame with no direction, every gate down): the sample is then not
    /// counted. The 5-second sample of <c>gate-attacked</c>'s <c>through=</c>.
    /// </summary>
    int AttackersBehindGates(LdPoint? inside);

    /// <summary>Whether each point has a navmesh and lies inside the mission boundary, in the order given: the <c>fort-points</c> boot check.</summary>
    IReadOnlyList<LdPointFacts> PointsUsable(IReadOnlyList<LdPlace> points);

    /// <summary>One fragment for the boot line (A23): <c>archer_positions=N formations=&lt;index:count,...&gt;</c>, the engine's own count of archer positions and the units in each defender formation.</summary>
    string DescribeArcherPositions();
}
