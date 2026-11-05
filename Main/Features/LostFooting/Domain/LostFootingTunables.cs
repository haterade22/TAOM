namespace TAOM.Features.LostFooting.Domain;

/// <summary>
/// Every number the Lost Footing census reads. They are compiled defaults, not MCM settings: nobody needs to tune a
/// measurement, and a stale saved value cannot reach them. A test pins the shipped values. Pure: no engine types.
/// </summary>
public sealed class LostFootingTunables
{
    /// <summary>Target seconds for one pass over the agent list; each tick reads its share (at least one agent, at most AgentsPerTick), so a pass is longer with very many agents and shorter with very few.</summary>
    public float SweepSeconds { get; init; } = 1f;

    /// <summary>Most agents read in one tick, however long the tick.</summary>
    public int AgentsPerTick { get; init; } = 64;

    /// <summary>Seconds between two census lines, written only while someone is stranded.</summary>
    public float CensusIntervalSeconds { get; init; } = 10f;

    /// <summary>How long an agent must keep missing the navigation mesh before it counts as off the mesh.</summary>
    public float OffMeshConfirmSeconds { get; init; } = 2f;

    /// <summary>How long an agent must stay still in water, or below the water level on land, before it counts.</summary>
    public float WaterStillSeconds { get; init; } = 2f;

    /// <summary>Metres a second under which an agent counts as still.</summary>
    public float StillSpeed { get; init; } = 0.35f;

    /// <summary>A still agent in water counts only when its goal is at least this far away.</summary>
    public float MinDestinationMeters { get; init; } = 3f;

    /// <summary>Most sample lines per class in one census.</summary>
    public int SamplesPerClass { get; init; } = 5;

    /// <summary>Side of one square of the hotspot grid, in metres.</summary>
    public float HotspotCellMeters { get; init; } = 5f;
}
