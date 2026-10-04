using System;
using System.Collections.Generic;
using System.Linq;
using BehaviorTreeWrapper;
using TAOM.Core.Logging;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// Cells of live agents for the creature trees' range scans. The cells are rebuilt every two seconds
/// from the mission tick while something reads them: a build nobody queried makes the next scheduled
/// rebuild a skip, and the next query rebuilds first. Until #592 the trees read the grid from the
/// engine's asynchronous agent tick while this tick rebuilt it; the third player freeze of 2026-09-13
/// had nothing but four warg trees, each scanning here on every evaluation. Every reader and the
/// rebuild are on the mission tick now, the tripwire reports any thread that comes back, and the
/// rebuild fills the spare of two maps and publishes it by one reference write, so a reader never
/// walks a map being cleared (#592, #595). A removal uses each map's agent-to-cell index and reaches both maps.
/// </summary>
public class SpatialGrid
{
    public static SpatialGrid Instance { get; internal set; }

    // Two maps: a rebuild fills the spare one and publishes it by one reference write, so the map a reader
    // holds is never cleared under it until a later rebuild. Each map has its agent-to-cell index, which
    // makes a removal one cell's List.Remove per map instead of a walk over every cell.
    private Dictionary<(int, int), List<Agent>> _grid = new();
    private Dictionary<Agent, (int, int)> _cellOf = new();
    private Dictionary<(int, int), List<Agent>> _spareGrid = new();
    private Dictionary<Agent, (int, int)> _spareCellOf = new();
    private readonly Stack<List<Agent>> _spareLists = new();

    // The list the last UpdateGrid passed (the mission's live AllAgents), whether anything queried the last
    // build, and how many scheduled rebuilds were skipped since it because nothing did.
    private List<Agent>? _agents;
    private bool _queriedSinceBuild;
    private int _skippedRebuilds;
    private bool _skipLogged;
    private bool _queryRebuildLogged;

    /// <summary>Builds made so far; tests read it.</summary>
    internal int BuildCount { get; private set; }

    /// <summary>Where the grid's INFO lines go: the file log, or a test's capture.</summary>
    internal Action<string> InfoLog { get; set; } = LogInfoToFile;

    public float CellSize = 20f;

    private static readonly Action<string> ReportOffThread =
        message => Debug.Print(message, 0, Debug.DebugColor.Red);

    // Removals raised off the main thread (OnAgentDeleted is native's to place, #634), applied from the
    // mission tick by ApplyPendingRemovals so only that tick ever edits a cell list.
    private static readonly Action<string> ReportParkedRemoval =
        message => Debug.Print(message, 0, Debug.DebugColor.Yellow);
    private readonly DeferredCallbackQueue _pendingRemovals = new(ReportParkedRemoval);

    // The Agent-typed API delegates to the generic helpers below, which never touch an Agent, so
    // tests can run the exact query on plain points. One position read per agent per query.
    private static readonly Func<Agent, Vec3> AgentPosition = agent => agent.Position;
    private static readonly Func<Agent, bool> IsLiveAgent = agent => agent.IsActive();

    // The two engine reads the grid makes on an agent. A seam, like InfoLog: a test drives the rebuild, the
    // wake-up query and the removals on bare Agent objects with no native pointer behind them.
    internal Func<Agent, Vec3> PositionOf { get; set; } = AgentPosition;
    internal Func<Agent, bool> IsLive { get; set; } = IsLiveAgent;

    private static void LogInfoToFile(string message)
    {
        try { IoC.Resolve<IModLogger>()?.LogInfo(message); }
        catch { /* a log line must never break the mission tick */ }
    }

    public void UpdateGrid(List<Agent> agents)
    {
        MissionThreadGuard.NoteCall("SpatialGrid.UpdateGrid", ReportOffThread);
        _agents = agents;
        // Nothing read the last build: skip this one. The next query rebuilds first, so a reader that
        // arrives later (a creature spawned mid-battle) never sees cells older than a rebuild would give it.
        if (BuildCount > 0 && !_queriedSinceBuild)
        {
            _skippedRebuilds++;
            if (!_skipLogged)
            {
                _skipLogged = true;
                InfoLog($"[SpatialGrid] Scheduled rebuild skipped: nothing queried build {BuildCount}, so rebuilds pause " +
                    "while nothing reads the grid and the next query rebuilds first. Logged once per grid (one grid per mission).");
            }
            return;
        }
        Rebuild(agents);
    }

    private void Rebuild(List<Agent> agents)
    {
        BuildCellsInto(_spareGrid, _spareCellOf, _spareLists, agents, IsLive, PositionOf, CellSize);
        (_grid, _spareGrid) = (_spareGrid, _grid);
        (_cellOf, _spareCellOf) = (_spareCellOf, _cellOf);
        _queriedSinceBuild = false;
        _skippedRebuilds = 0;
        BuildCount++;
    }

    /// <summary>
    /// Drop a deleted agent from both maps. Its managed handle would otherwise sit here until the next rebuild,
    /// reading position from the engine slot its index now belongs to (#595).
    /// </summary>
    public void Remove(Agent agent)
    {
        if (agent == null) return;
        _pendingRemovals.RunOrDefer("SpatialGrid.Remove", () => RemoveNow(agent));
    }

    /// <summary>Applies removals parked off the main thread. Call from the mission tick.</summary>
    public void ApplyPendingRemovals() => _pendingRemovals.Drain();

    internal int PendingRemovalCount => _pendingRemovals.Count;

    /// <summary>Every agent handle either map generation holds, in its cells and in its agent-to-cell index;
    /// tests read it.</summary>
    internal IEnumerable<Agent> HeldAgents() =>
        _grid.Values.Concat(_spareGrid.Values).SelectMany(cell => cell).Concat(_cellOf.Keys).Concat(_spareCellOf.Keys);

    // Both generations: no rebuild reaches the spare map while nothing queries the grid, and a deleted agent keeps
    // its Mission reference, so a handle left there would hold the finished mission until the next mission's
    // AdvancedCombatBehavior replaced the grid (the base held no deleted agent). The grid's surviving agents still
    // reach that mission through Agent.Team until then, which this removal does not change (plan 033 review, R18).
    private void RemoveNow(Agent agent)
    {
        RemoveFromCells(_grid, _cellOf, agent);
        RemoveFromCells(_spareGrid, _spareCellOf, agent);
    }

    /// <summary>Buckets every included item by the cell of its position into <paramref name="cells"/> and
    /// records each item's cell in <paramref name="cellOf"/>, after returning the previous build's lists to
    /// <paramref name="spareLists"/>. Pure; tests drive it with plain points.</summary>
    internal static void BuildCellsInto<T>(Dictionary<(int, int), List<T>> cells, Dictionary<T, (int, int)> cellOf,
        Stack<List<T>> spareLists, List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        foreach (List<T> list in cells.Values)
        {
            list.Clear();
            spareLists.Push(list);
        }
        cells.Clear();
        cellOf.Clear();
        foreach (T item in items)
        {
            if (!include(item))
                continue;
            Vec3 pos = positionOf(item);
            var key = ((int)Math.Floor(pos.x / cellSize), (int)Math.Floor(pos.y / cellSize));
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = spareLists.Count > 0 ? spareLists.Pop() : new List<T>();
                cells[key] = list;
            }
            list.Add(item);
            cellOf[item] = key;
        }
    }

    /// <summary>The fresh-container form, kept for the query tests.</summary>
    internal static Dictionary<(int, int), List<T>> BuildCells<T>(List<T> items, Func<T, bool> include, Func<T, Vec3> positionOf, float cellSize)
    {
        var cells = new Dictionary<(int, int), List<T>>();
        BuildCellsInto(cells, new Dictionary<T, (int, int)>(), new Stack<List<T>>(), items, include, positionOf, cellSize);
        return cells;
    }

    /// <summary>Removes <paramref name="item"/> from the one cell <paramref name="cellOf"/> names. Returns
    /// false when it is in no cell. Pure.</summary>
    internal static bool RemoveFromCells<T>(Dictionary<(int, int), List<T>> cells, Dictionary<T, (int, int)> cellOf, T item)
    {
        if (!cellOf.TryGetValue(item, out (int, int) key)) return false;
        cellOf.Remove(item);
        return cells.TryGetValue(key, out List<T> list) && list.Remove(item);
    }

    public List<Agent> GetAgentsInRadius(Vec3 center, float radius)
    {
        List<Agent> agents = new();
        GetAgentsInRadius(center, radius, agents);
        return agents;
    }

    /// <summary>
    /// Zero-alloc overload: clears <paramref name="buffer"/> and fills it with the agents in radius. Use from
    /// per-eval BT hot paths (e.g. the creature engage decorators) with a reusable field buffer to avoid a fresh
    /// List allocation every scan — the shared <c>ElephantLikeEngageDecorator</c> uses the equivalent
    /// <c>Mission.GetNearbyAgents(..., scratch)</c> form. The allocating overload above delegates here.
    /// </summary>
    public void GetAgentsInRadius(Vec3 center, float radius, List<Agent> buffer)
    {
        bool offThread = MissionThreadGuard.NoteCall("SpatialGrid.GetAgentsInRadius", ReportOffThread);
        // Only the mission thread may build; an off-thread reader (a reported regression) reads what is there.
        if (_skippedRebuilds > 0 && !offThread && _agents != null)
        {
            if (!_queryRebuildLogged)
            {
                _queryRebuildLogged = true;
                InfoLog($"[SpatialGrid] A query found {_skippedRebuilds} scheduled rebuilds skipped and rebuilt the grid " +
                    $"before answering (build {BuildCount + 1}). Logged once per grid (one grid per mission).");
            }
            Rebuild(_agents);
        }
        _queriedSinceBuild = true;
        CollectInRadius(_grid, center, radius, CellSize, PositionOf, buffer);
    }

    /// <summary>
    /// Clears <paramref name="buffer"/> and fills it with the items whose position is within
    /// <paramref name="radius"/> of <paramref name="center"/> (3D distance, inclusive), looking up only
    /// the cells in the query's bounding box. Returns how many cells it looked up. Touches no state
    /// but the caller's buffer.
    /// </summary>
    internal static int CollectInRadius<T>(Dictionary<(int, int), List<T>> cells, Vec3 center, float radius, float cellSize, Func<T, Vec3> positionOf, List<T> buffer)
    {
        buffer.Clear();
        float radiusSquared = radius * radius;
        int minX = (int)Math.Floor((center.x - radius) / cellSize);
        int maxX = (int)Math.Floor((center.x + radius) / cellSize);
        int minY = (int)Math.Floor((center.y - radius) / cellSize);
        int maxY = (int)Math.Floor((center.y + radius) / cellSize);

        // Cells are keyed on (x, y) only: a battlefield's vertical spread is a few metres, so a z axis
        // mostly added empty lookups (7 x 7 x 7 = 343 for the warg's 60 m scan, now 7 x 7 = 49). The
        // distance test below stays 3D, so on a grid built from current positions the result is the
        // same sphere. Between rebuilds (every 2 s) positions are live and cells are not: a column
        // also returns an agent that has since moved up or down into the sphere, which a z cell
        // would have missed. Within a column, items come back in build order.
        int probes = 0;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            probes++;
            if (!cells.TryGetValue((x, y), out List<T> cell)) continue;
            foreach (T item in cell)
            {
                Vec3 pos = positionOf(item);
                float dx = pos.x - center.x;
                float dy = pos.y - center.y;
                float dz = pos.z - center.z;
                if (dx * dx + dy * dy + dz * dz <= radiusSquared)
                    buffer.Add(item);
            }
        }
        return probes;
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Agent target)
    {
        return GetAgentsInRadius(target.Position, range);
    }

    public List<Agent> GetNearAliveAgentsInRange(float range, Vec3 targetPos)
    {
        return GetAgentsInRadius(targetPos, range);
    }

    /// <summary>Zero-alloc overload — fills <paramref name="buffer"/> with the alive agents in range of <paramref name="target"/>.</summary>
    public void GetNearAliveAgentsInRange(float range, Agent target, List<Agent> buffer)
    {
        GetAgentsInRadius(target.Position, range, buffer);
    }
}
