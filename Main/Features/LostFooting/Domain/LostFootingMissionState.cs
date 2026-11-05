using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TAOM.Features.LostFooting.Domain;

/// <summary>What the census knows about one agent, from its sightings. Main thread only.</summary>
public sealed class StrandEntry
{
    public AgentKind Kind { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    /// <summary>Water level at the spot minus its height; NaN when the level is unknown.</summary>
    public float Depth { get; set; } = float.NaN;

    public float DistToFormation { get; set; } = float.NaN;

    public float DistToDestination { get; set; } = float.NaN;

    /// <summary>Mission clock at the latest sighting.</summary>
    public float LastSeen { get; set; } = float.NaN;

    /// <summary>The sweep of the agent list in which it was last read; an entry unseen for several sweeps is dropped.</summary>
    public int Sweep { get; set; }

    /// <summary>Clock time each condition first held in an unbroken run, or NaN while it does not hold.</summary>
    public float MissSince { get; set; } = float.NaN;

    public float WaterSince { get; set; } = float.NaN;

    public float LandSince { get; set; } = float.NaN;

    /// <summary>Clock time the open episode began, or NaN while the agent is not stranded.</summary>
    public float StrandedSince { get; set; } = float.NaN;

    /// <summary>The engine flags the agent as unreachable via the navigation mesh.</summary>
    public bool Unreachable { get; set; }

    public bool OffMesh { get; set; }

    public bool WaterStill { get; set; }

    public bool LandBelow { get; set; }

    /// <summary>The second look at an off-mesh agent has been asked for in this episode.</summary>
    public bool ConfirmTried { get; set; }

    public OffMeshDetail? Detail { get; set; }

    public bool IsStranded => OffMesh || WaterStill || LandBelow;

    public bool InClass(StrandClass c) => c == StrandClass.OffMesh ? OffMesh : c == StrandClass.WaterStill ? WaterStill : LandBelow;
}

/// <summary>One square of the hotspot grid and the agent-seconds spent stranded in it.</summary>
public sealed class HotCell
{
    public int CellX { get; set; }

    public int CellY { get; set; }

    public float AgentSeconds { get; set; }
}

/// <summary>Where the next slice of the agent list starts and how much of it to read.</summary>
public struct SlicePlan
{
    public int Start;
    public int Take;
    public int Next;
    public bool Wrapped;
}

/// <summary>How many tracked agents are stranded now, by class and kind.</summary>
public sealed class LostFootingTally
{
    public static readonly AgentKind[] Kinds = (AgentKind[])Enum.GetValues(typeof(AgentKind));

    public static readonly StrandClass[] Classes = (StrandClass[])Enum.GetValues(typeof(StrandClass));

    private readonly int[] _counts = new int[Classes.Length * Kinds.Length];

    /// <summary>How many agents the census remembers, stranded or not.</summary>
    public int Tracked { get; internal set; }

    /// <summary>Stranded agents in the off-mesh class whose second look confirmed it.</summary>
    public int Confirmed { get; internal set; }

    public int Count(StrandClass c)
    {
        var total = 0;
        foreach (var k in Kinds)
            total += Count(c, k);
        return total;
    }

    public int Count(StrandClass c, AgentKind kind) => _counts[(int)c * Kinds.Length + (int)kind];

    internal void Add(StrandClass c, AgentKind k) => _counts[(int)c * Kinds.Length + (int)k]++;
}

/// <summary>
/// Everything the census remembers inside one mission, keyed on the opaque agent handle by reference. A fresh
/// instance is built for every mission and dropped with it, so nothing carries into the next battle.
/// Main thread only (OnMissionTick); no locking.
/// </summary>
public sealed class LostFootingMissionState
{
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>Agent handles are equal only when they are the same object.</summary>
    internal static readonly IEqualityComparer<object> Comparer = new ReferenceComparer();

    public Dictionary<object, StrandEntry> Entries { get; } = new Dictionary<object, StrandEntry>(Comparer);

    /// <summary>Agent-seconds stranded per grid square, keyed by the square's column and row.</summary>
    public Dictionary<(int X, int Y), float> Hotspots { get; } = new Dictionary<(int X, int Y), float>();

    /// <summary>The scene id, kept for the summary line.</summary>
    public string SceneId { get; set; } = string.Empty;

    /// <summary>The scene's global water level, or NaN until it is read.</summary>
    public float SceneWaterLevel { get; set; } = float.NaN;

    /// <summary>Seconds of mission time accumulated from the tick deltas.</summary>
    public float Clock { get; set; }

    /// <summary>Complete passes over the agent list so far.</summary>
    public int Sweep { get; set; }

    /// <summary>Index in the agent list where the next slice starts.</summary>
    public int Cursor { get; set; }

    public bool Started { get; set; }

    public bool SummaryWritten { get; set; }

    /// <summary>Clock time the next census line is due.</summary>
    public float NextCensus { get; set; }

    /// <summary>Episodes that ended because the agent got going (or onto the mesh) again.</summary>
    public int Recovered { get; set; }

    /// <summary>Episodes that ended because the agent left the census (it died, or was pruned unseen).</summary>
    public int Removed { get; set; }

    /// <summary>Episodes still open when the battle ended or the census was paused.</summary>
    public int AtEnd { get; set; }

    public int Episodes => Recovered + Removed + AtEnd;

    /// <summary>Episodes by the kind of agent, indexed by <see cref="AgentKind"/>.</summary>
    public int[] EpisodeKinds { get; } = new int[LostFootingTally.Kinds.Length];

    public float LongestEpisode { get; set; }

    public int PeakOffMesh { get; set; }

    public int PeakConfirmed { get; set; }

    public int PeakWaterStill { get; set; }

    public int PeakLandBelow { get; set; }

    /// <summary>Agents probed, and the milliseconds spent reading and folding them: the census's own cost.</summary>
    public int Probes { get; set; }

    public double TotalMs { get; set; }

    public double MaxTickMs { get; set; }
}
