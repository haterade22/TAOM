namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Tuning for <see cref="ProvincePartitioner"/>. Costs are per world unit walked through a cell of
/// that class; <see cref="MaxClaim"/> is in the same units, head start included. The defaults were
/// tuned offline with <c>tools/realm_borders_preview.py</c> on TAOM_Map.
/// </summary>
public sealed class PartitionSettings
{
    public float OpenCost { get; init; } = 1f;

    public float RoughCost { get; init; } = 2f;

    public float RiverCost { get; init; } = 12f;

    public float CrossingCost { get; init; } = 1f;

    /// <summary>Land whose cheapest claim costs more than this stays wild.</summary>
    public float MaxClaim { get; init; } = 190f;

    /// <summary>
    /// An unclaimed patch of land smaller than this many cells (walls included, water never) joins
    /// the realm around it, so a lone ridge or a crest-ringed valley is not a hole. 0 disables it.
    /// </summary>
    public int PocketCells { get; init; } = 150;

    /// <summary>How many cells out a seed on a wall or on water looks for open ground to start from.</summary>
    public int SeedSearchRadius { get; init; } = 5;
}
