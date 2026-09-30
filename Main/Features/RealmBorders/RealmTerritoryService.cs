using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Features.RealmBorders;

public enum TerritoryState
{
    Idle,
    Sampling,
    Partitioning,
    Ready,
    Failed,
}

/// <summary>Runs the partition off the game thread; a test swaps in a synchronous one.</summary>
public interface ITerritoryWorker
{
    Task<T> Run<T>(Func<T> work);
}

public sealed class TaskTerritoryWorker : ITerritoryWorker
{
    public Task<T> Run<T>(Func<T> work) => Task.Run(work);
}

/// <summary>
/// Owns the fixed province map: one province per town or castle, grown over the map's terrain. The
/// terrain is sampled on the game thread in time-boxed slices (every sample is a native navmesh
/// query), then the flood and the boundary tracing run on a worker, touching managed arrays only.
/// The result is per map, not per campaign: a second campaign or a reload on the same map reuses it,
/// and a different fief layout recomputes it.
/// </summary>
public sealed class RealmTerritoryService
{
    /// <summary>Cells along the map's longer side: about 3 world units per cell on TAOM_Map.</summary>
    public const int GridCells = 512;

    public const float TownHeadStart = 30f;
    public const float CastleHeadStart = 18f;
    public const float VillageHeadStart = 6f;

    private const int SampleBatch = 256;

    private readonly IMapTerrainAdapter _terrain;
    private readonly IRealmMapAdapter _map;
    private readonly ITerritoryWorker _worker;
    private readonly IModLogger _logger;
    private readonly Stopwatch _sampling = new Stopwatch();

    private string? _layoutKey;
    private IReadOnlyList<FiefSite> _sites = Array.Empty<FiefSite>();
    private PartitionSettings _settings = new PartitionSettings();
    private TerrainClass[] _classes = Array.Empty<TerrainClass>();
    private int _columns, _rows, _next;
    private float _minX, _minY, _cellSize;
    private Task<TerritoryResult>? _task;

    public RealmTerritoryService(IMapTerrainAdapter terrain, IRealmMapAdapter map, ITerritoryWorker worker, IModLogger logger)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TerritoryState State { get; private set; } = TerritoryState.Idle;

    public TerritoryResult? Result { get; private set; }

    public TimeSpan SamplingTime => _sampling.Elapsed;

    /// <summary>Starts the partition for the loaded map, or keeps the finished one when the fief layout is unchanged.</summary>
    public void Begin(PartitionSettings settings)
    {
        var sites = _map.GetFiefSites();
        string key = LayoutKey(sites);
        if (key == _layoutKey && (State == TerritoryState.Ready || State == TerritoryState.Sampling || State == TerritoryState.Partitioning))
            return;

        Result = null;
        _task = null;
        _layoutKey = key;
        if (sites.Count == 0 || !_terrain.TryGetBounds(out float minX, out float minY, out float maxX, out float maxY)
            || !FiniteFloatValidator.IsFinite(minX) || !FiniteFloatValidator.IsFinite(minY)
            || !FiniteFloatValidator.IsFinite(maxX) || !FiniteFloatValidator.IsFinite(maxY)
            || !(maxX > minX) || !(maxY > minY))
        {
            State = TerritoryState.Failed;
            _logger.LogWarning("[RealmBorders] no campaign map bounds or no fiefs; borders stay off this session");
            return;
        }

        _sites = sites;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _cellSize = Math.Max(maxX - minX, maxY - minY) / GridCells;
        _columns = Math.Max(1, (int)Math.Ceiling((maxX - minX) / _cellSize));
        _rows = Math.Max(1, (int)Math.Ceiling((maxY - minY) / _cellSize));
        _minX = minX;
        _minY = minY;
        _classes = new TerrainClass[_columns * _rows];
        _next = 0;
        _sampling.Reset();
        State = TerritoryState.Sampling;
        _logger.LogInfo($"[RealmBorders] sampling terrain: {_columns}x{_rows} cells of {_cellSize:F2} units, {sites.Count} fiefs");
    }

    /// <summary>Forgets the finished partition so the next <see cref="Begin"/> recomputes it.</summary>
    public void Invalidate()
    {
        _layoutKey = null;
        Result = null;
        _task = null;
        State = TerritoryState.Idle;
    }

    /// <summary>Game-thread step: sample terrain for about <paramref name="budgetMilliseconds"/>, or collect the worker's result.</summary>
    public void Tick(double budgetMilliseconds)
    {
        if (State == TerritoryState.Sampling)
            Sample(budgetMilliseconds);
        if (State == TerritoryState.Partitioning && _task != null && _task.IsCompleted)
            Collect(_task);
    }

    private void Sample(double budgetMilliseconds)
    {
        var slice = Stopwatch.StartNew();
        _sampling.Start();
        do
        {
            int end = Math.Min(_classes.Length, _next + SampleBatch);
            for (int i = _next; i < end; i++)
            {
                int column = i % _columns, row = i / _columns;
                int code = _terrain.TerrainTypeAt(_minX + (column + 0.5f) * _cellSize, _minY + (row + 0.5f) * _cellSize);
                _classes[i] = code < 0 ? TerrainClass.Water : TerrainClassifier.Classify(code);
            }
            _next = end;
        }
        while (_next < _classes.Length && slice.Elapsed.TotalMilliseconds < budgetMilliseconds);
        _sampling.Stop();

        if (_next < _classes.Length)
            return;
        var grid = new TerrainGrid(_columns, _rows, _minX, _minY, _cellSize, _classes);
        var seeds = Seeds(_sites);
        var settings = _settings;
        var fiefIds = new List<string>(_sites.Count);
        foreach (var site in _sites)
            fiefIds.Add(site.Id);
        State = TerritoryState.Partitioning;
        _task = _worker.Run(() => Compute(grid, seeds, settings, fiefIds));
    }

    private void Collect(Task<TerritoryResult> task)
    {
        if (task.IsFaulted || task.IsCanceled)
        {
            State = TerritoryState.Failed;
            _logger.LogError($"[RealmBorders] partition failed: {task.Exception?.GetBaseException().Message ?? "cancelled"}");
            return;
        }
        Result = task.Result;
        State = TerritoryState.Ready;
        _logger.LogInfo($"[RealmBorders] provinces ready: sampling {_sampling.Elapsed.TotalMilliseconds:F0} ms, "
            + $"partition {Result.PartitionTime.TotalMilliseconds:F0} ms, {Result.Boundaries.Count} boundary chains");
    }

    private static TerritoryResult Compute(TerrainGrid grid, IReadOnlyList<ProvinceSeed> seeds, PartitionSettings settings, IReadOnlyList<string> fiefIds)
    {
        var watch = Stopwatch.StartNew();
        var map = ProvincePartitioner.Partition(grid, seeds, settings);
        var boundaries = BoundaryTracer.Trace(map);
        return new TerritoryResult(grid, map, boundaries, fiefIds, watch.Elapsed);
    }

    internal static List<ProvinceSeed> Seeds(IReadOnlyList<FiefSite> sites)
    {
        var seeds = new List<ProvinceSeed>();
        for (int i = 0; i < sites.Count; i++)
        {
            var site = sites[i];
            seeds.Add(new ProvinceSeed(i, site.Position.X, site.Position.Y, site.IsTown ? TownHeadStart : CastleHeadStart));
            foreach (var village in site.Villages)
                seeds.Add(new ProvinceSeed(i, village.X, village.Y, VillageHeadStart));
        }
        return seeds;
    }

    /// <summary>A fingerprint of the fief layout: ids and positions, to the hundredth of a unit.</summary>
    internal static string LayoutKey(IReadOnlyList<FiefSite> sites)
    {
        ulong hash = 14695981039346656037UL;
        void Mix(long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        foreach (var site in sites)
        {
            foreach (char ch in site.Id)
                Mix(ch);
            Mix((long)Math.Round(site.Position.X * 100f));
            Mix((long)Math.Round(site.Position.Y * 100f));
            Mix(site.Villages.Count);
        }
        return $"{sites.Count}:{hash:X16}";
    }
}

/// <summary>A finished partition: the sampled terrain, the province map, its boundary chains, and the fief behind each province index.</summary>
public sealed class TerritoryResult
{
    public TerritoryResult(TerrainGrid terrain, ProvinceMap map, IReadOnlyList<ProvinceBoundary> boundaries, IReadOnlyList<string> fiefIds, TimeSpan partitionTime)
    {
        Terrain = terrain;
        Map = map;
        Boundaries = boundaries;
        FiefIds = fiefIds;
        PartitionTime = partitionTime;
    }

    public TerrainGrid Terrain { get; }

    public ProvinceMap Map { get; }

    public IReadOnlyList<ProvinceBoundary> Boundaries { get; }

    public IReadOnlyList<string> FiefIds { get; }

    public TimeSpan PartitionTime { get; }
}
