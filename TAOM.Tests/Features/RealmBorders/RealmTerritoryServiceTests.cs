using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the province map's lifecycle: sampled on the game thread in slices, partitioned on a worker,
/// kept while the fief layout is unchanged, recomputed when it changes, and failing loudly (borders
/// off, a log line) rather than drawing from nothing.
/// </summary>
[TestClass]
public class RealmTerritoryServiceTests
{
    internal sealed class SyncWorker : ITerritoryWorker
    {
        public Task<T> Run<T>(Func<T> work) => Task.FromResult(work());
    }

    internal sealed class FailingWorker : ITerritoryWorker
    {
        public Task<T> Run<T>(Func<T> work) => Task.FromException<T>(new InvalidOperationException("boom"));
    }

    /// <summary>A flat 400 x 200 map; <see cref="TerrainAt"/> overrides the plain ground.</summary>
    internal sealed class FakeTerrain : IMapTerrainAdapter
    {
        public bool HasBounds = true;
        public (float MinX, float MinY, float MaxX, float MaxY) Bounds = (0f, 0f, 400f, 200f);
        public Func<float, float, int> TerrainAt = (x, y) => 1;
        public int Queries;

        public bool TryGetBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            (minX, minY, maxX, maxY) = Bounds;
            return HasBounds;
        }

        public int TerrainTypeAt(float x, float y)
        {
            Queries++;
            return TerrainAt(x, y);
        }

        public float HeightAt(float x, float y, float fallback) => 50f;
    }

    internal static FiefSite Town(string id, float x, float y) => new FiefSite(id, new MapPoint(x, y), true, new MapPoint[0]);

    internal static IRealmMapAdapter MapWith(params FiefSite[] sites)
    {
        var map = Substitute.For<IRealmMapAdapter>();
        map.GetFiefSites().Returns(sites);
        return map;
    }

    internal static RealmTerritoryService ReadyTerritory(FakeTerrain terrain, IRealmMapAdapter map)
    {
        var territory = new RealmTerritoryService(terrain, map, new SyncWorker(), Substitute.For<IModLogger>());
        territory.Begin(new PartitionSettings());
        territory.Tick(1e7);
        territory.Tick(1e7);
        Assert.AreEqual(TerritoryState.Ready, territory.State);
        return territory;
    }

    [TestMethod]
    public void Begin_NoMapBounds_FailsAndLogs()
    {
        var logger = Substitute.For<IModLogger>();
        var territory = new RealmTerritoryService(new FakeTerrain { HasBounds = false }, MapWith(Town("a", 100, 100)), new SyncWorker(), logger);

        territory.Begin(new PartitionSettings());

        Assert.AreEqual(TerritoryState.Failed, territory.State);
        logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("RealmBorders")));
    }

    [TestMethod]
    public void Begin_NoFiefs_Fails()
    {
        var territory = new RealmTerritoryService(new FakeTerrain(), MapWith(), new SyncWorker(), Substitute.For<IModLogger>());

        territory.Begin(new PartitionSettings());

        Assert.AreEqual(TerritoryState.Failed, territory.State);
    }

    [TestMethod]
    public void Tick_WholeMapSampled_PartitionsOnTheWorkerAndBecomesReady()
    {
        var territory = ReadyTerritory(new FakeTerrain(), MapWith(Town("west", 100, 100), Town("east", 300, 100)));

        var result = territory.Result!;
        Assert.AreEqual(0, result.Map.ProvinceAt(50, 100));
        Assert.AreEqual(1, result.Map.ProvinceAt(350, 100));
        CollectionAssert.AreEqual(new[] { "west", "east" }, result.FiefIds.ToArray());
        Assert.IsTrue(result.Boundaries.Count > 0);
    }

    [TestMethod]
    public void Tick_TinyBudget_SamplesInSlices()
    {
        var terrain = new FakeTerrain();
        var territory = new RealmTerritoryService(terrain, MapWith(Town("west", 100, 100)), new SyncWorker(), Substitute.For<IModLogger>());
        territory.Begin(new PartitionSettings());

        territory.Tick(0);

        Assert.AreEqual(TerritoryState.Sampling, territory.State, "one slice does not cover the map");
        Assert.IsTrue(terrain.Queries > 0);
    }

    [TestMethod]
    public void Begin_SameLayoutWhileReady_KeepsTheProvinces()
    {
        var terrain = new FakeTerrain();
        var territory = ReadyTerritory(terrain, MapWith(Town("west", 100, 100), Town("east", 300, 100)));
        int queries = terrain.Queries;

        territory.Begin(new PartitionSettings());

        Assert.AreEqual(TerritoryState.Ready, territory.State);
        Assert.AreEqual(queries, terrain.Queries, "no second sampling pass for the same map");
    }

    [TestMethod]
    public void Begin_DifferentLayout_Recomputes()
    {
        var terrain = new FakeTerrain();
        var map = MapWith(Town("west", 100, 100), Town("east", 300, 100));
        var territory = ReadyTerritory(terrain, map);
        map.GetFiefSites().Returns(new[] { Town("west", 100, 100), Town("east", 320, 100) });

        territory.Begin(new PartitionSettings());

        Assert.AreEqual(TerritoryState.Sampling, territory.State);
    }

    [TestMethod]
    public void Tick_WorkerFails_BecomesFailed()
    {
        var logger = Substitute.For<IModLogger>();
        var territory = new RealmTerritoryService(new FakeTerrain(), MapWith(Town("west", 100, 100)), new FailingWorker(), logger);
        territory.Begin(new PartitionSettings());

        territory.Tick(1e7);
        territory.Tick(1e7);

        Assert.AreEqual(TerritoryState.Failed, territory.State);
        logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
    }

    [TestMethod]
    public void Tick_NoLandFace_IsWaterAndNeverClaimed()
    {
        var terrain = new FakeTerrain { TerrainAt = (x, y) => x > 300 ? -1 : 1 };

        var territory = ReadyTerritory(terrain, MapWith(Town("west", 100, 100)));

        Assert.AreEqual(ProvinceMap.Unclaimed, territory.Result!.Map.ProvinceAt(350, 100));
        Assert.AreEqual(0, territory.Result.Map.ProvinceAt(250, 100));
    }

    [TestMethod]
    public void Tick_MountainRidge_StopsTheProvince()
    {
        var terrain = new FakeTerrain { TerrainAt = (x, y) => x >= 196 && x < 204 ? 7 : 1 };

        var territory = ReadyTerritory(terrain, MapWith(Town("west", 100, 100)));

        Assert.AreEqual(ProvinceMap.Unclaimed, territory.Result!.Map.ProvinceAt(300, 100), "the far side of a range is not reached");
    }

    [TestMethod]
    public void Seeds_TownCastleAndVillages_ShareTheProvinceWithTheirHeadStarts()
    {
        var sites = new[]
        {
            new FiefSite("town", new MapPoint(1, 1), true, new[] { new MapPoint(2, 2) }),
            new FiefSite("castle", new MapPoint(5, 5), false, new MapPoint[0]),
        };

        var seeds = RealmTerritoryService.Seeds(sites);

        CollectionAssert.AreEqual(new[] { 0, 0, 1 }, seeds.Select(s => s.Province).ToArray());
        CollectionAssert.AreEqual(
            new[] { RealmTerritoryService.TownHeadStart, RealmTerritoryService.VillageHeadStart, RealmTerritoryService.CastleHeadStart },
            seeds.Select(s => s.HeadStart).ToArray());
    }

    [TestMethod]
    public void Invalidate_ReturnsToIdle()
    {
        var territory = ReadyTerritory(new FakeTerrain(), MapWith(Town("west", 100, 100)));

        territory.Invalidate();

        Assert.AreEqual(TerritoryState.Idle, territory.State);
        Assert.IsNull(territory.Result);
    }

    [DataTestMethod]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NaN)]
    public void Begin_BoundsNotFinite_FailsWithoutSampling(float maxX)
    {
        var terrain = new FakeTerrain { Bounds = (0f, 0f, maxX, 200f) };
        var territory = new RealmTerritoryService(terrain, MapWith(Town("a", 100, 100)), new SyncWorker(), Substitute.For<IModLogger>());

        territory.Begin(new PartitionSettings());
        territory.Tick(1e7);

        Assert.AreEqual(TerritoryState.Failed, territory.State);
        Assert.AreEqual(0, terrain.Queries, "no navmesh query at an infinite position");
    }
}
