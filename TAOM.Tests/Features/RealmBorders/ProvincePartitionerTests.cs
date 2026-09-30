using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the territory model behind realm borders: one province per fief, grown by a cost-distance
/// flood over the map's terrain classes. The province map is computed once per map and every border
/// is read off it, so a rule broken here moves every border in the game.
/// </summary>
[TestClass]
public class ProvincePartitionerTests
{
    private static readonly PartitionSettings Unlimited = new PartitionSettings { MaxClaim = 1e9f, PocketCells = 0 };

    private static TerrainGrid Grid(int columns, int rows, params (int Column, int Row, TerrainClass Class)[] marks)
    {
        var cells = new TerrainClass[columns * rows];
        foreach (var (column, row, cls) in marks)
            cells[row * columns + column] = cls;
        return new TerrainGrid(columns, rows, 0f, 0f, 1f, cells);
    }

    private static (int, int, TerrainClass)[] Column(int column, int rows, TerrainClass cls) =>
        Enumerable.Range(0, rows).Select(r => (column, r, cls)).ToArray();

    private static ProvinceSeed Seed(int province, float x, float y, float headStart = 0f) =>
        new ProvinceSeed(province, x, y, headStart);

    [TestMethod]
    public void Partition_WallBetweenSeeds_SplitsTheLandAtTheWall()
    {
        var grid = Grid(20, 20, Column(10, 20, TerrainClass.Wall));

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 2.5f, 10.5f), Seed(1, 17.5f, 10.5f) }, Unlimited);

        for (int row = 0; row < 20; row++)
        {
            Assert.AreEqual(0, map[3, row], "west of the wall belongs to the west seed");
            Assert.AreEqual(1, map[16, row], "east of the wall belongs to the east seed");
            Assert.AreEqual(ProvinceMap.Unclaimed, map[10, row], "a wall is never claimed");
        }
    }

    [TestMethod]
    public void Partition_HeadStart_MovesTheBorderTowardTheWeakerSeed()
    {
        var grid = Grid(21, 1);

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 0.5f, 0.5f), Seed(1, 20.5f, 0.5f, headStart: 5f) }, Unlimited);

        // Equal footing would split at column 10; 5 of head start moves the split 2.5 cells west.
        Assert.AreEqual(0, map[7, 0]);
        Assert.AreEqual(1, map[8, 0]);
    }

    [TestMethod]
    public void Partition_BeyondMaxClaim_LeavesTheLandWild()
    {
        var grid = Grid(30, 1);

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 0.5f, 0.5f) }, new PartitionSettings { MaxClaim = 10f, PocketCells = 0 });

        Assert.AreEqual(0, map[5, 0]);
        Assert.AreEqual(ProvinceMap.Unclaimed, map[29, 0]);
    }

    [TestMethod]
    public void Partition_SeedsSharingAProvince_GrowOneProvince()
    {
        var grid = Grid(20, 20);

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(3, 2.5f, 2.5f), Seed(3, 17.5f, 17.5f) }, Unlimited);

        Assert.IsTrue(Enumerable.Range(0, 400).All(i => map[i % 20, i / 20] == 3));
    }

    [TestMethod]
    public void Partition_WaterIsNeverClaimed()
    {
        var grid = Grid(10, 10, (5, 5, TerrainClass.Water));

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 0.5f, 0.5f) }, Unlimited);

        Assert.AreEqual(ProvinceMap.Unclaimed, map[5, 5]);
        Assert.AreEqual(0, map[6, 5]);
    }

    [TestMethod]
    public void Partition_RiverCrossingPenalty_KeepsEachBankWithItsOwnSeed()
    {
        // Without the river the west seed, three cells closer, would take the near east bank.
        var grid = Grid(21, 1, (10, 0, TerrainClass.River));
        var seeds = new[] { Seed(0, 7.5f, 0.5f), Seed(1, 17.5f, 0.5f) };

        var map = ProvincePartitioner.Partition(grid, seeds, new PartitionSettings { MaxClaim = 1e9f, PocketCells = 0, RiverCost = 20f });

        Assert.AreEqual(0, map[9, 0], "west bank");
        Assert.AreEqual(1, map[11, 0], "east bank");
    }

    [TestMethod]
    public void Partition_FordAcrossTheRiver_LetsTheCloserSeedCross()
    {
        var grid = Grid(21, 1, (10, 0, TerrainClass.Crossing));
        var seeds = new[] { Seed(0, 7.5f, 0.5f), Seed(1, 17.5f, 0.5f) };

        var map = ProvincePartitioner.Partition(grid, seeds, new PartitionSettings { MaxClaim = 1e9f, PocketCells = 0, RiverCost = 20f });

        Assert.AreEqual(0, map[11, 0], "a ford costs no more than open ground, so the nearer seed crosses");
    }

    [TestMethod]
    public void Partition_RoughGround_CostsMoreThanOpenGround()
    {
        var marks = Enumerable.Range(11, 10).Select(c => (c, 0, TerrainClass.Rough)).ToArray();
        var grid = Grid(21, 1, marks);

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 0.5f, 0.5f), Seed(1, 20.5f, 0.5f) }, Unlimited);

        Assert.AreEqual(0, map[12, 0], "the seed crossing rough ground reaches less far");
    }

    [TestMethod]
    public void Partition_SeedOnAWall_StartsFromTheNearestOpenCell()
    {
        var grid = Grid(10, 10, (5, 5, TerrainClass.Wall));

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(4, 5.5f, 5.5f) }, Unlimited);

        Assert.AreEqual(4, map[0, 0]);
    }

    [TestMethod]
    public void Partition_SmallPocketRingedByWalls_JoinsTheRealmAroundIt()
    {
        var marks = new List<(int, int, TerrainClass)>();
        for (int i = 8; i <= 11; i++)
        {
            marks.Add((i, 8, TerrainClass.Wall));
            marks.Add((i, 11, TerrainClass.Wall));
            marks.Add((8, i, TerrainClass.Wall));
            marks.Add((11, i, TerrainClass.Wall));
        }
        var grid = Grid(20, 20, marks.ToArray());
        var seeds = new[] { Seed(0, 2.5f, 2.5f) };

        Assert.AreEqual(ProvinceMap.Unclaimed, ProvincePartitioner.Partition(grid, seeds, Unlimited)[9, 9]);
        // the pocket is its 4 open cells plus the 12 walls around them
        var merged = ProvincePartitioner.Partition(grid, seeds, new PartitionSettings { MaxClaim = 1e9f, PocketCells = 20 });
        Assert.AreEqual(0, merged[9, 9]);
        Assert.AreEqual(ProvinceMap.Unclaimed, ProvincePartitioner.Partition(grid, seeds, new PartitionSettings { MaxClaim = 1e9f, PocketCells = 10 })[9, 9]);
    }

    [TestMethod]
    public void Partition_LakeInsideARealm_NeverJoinsAPocket()
    {
        var marks = new List<(int, int, TerrainClass)>();
        for (int c = 8; c < 12; c++)
            for (int r = 8; r < 12; r++)
                marks.Add((c, r, TerrainClass.Water));
        var grid = Grid(20, 20, marks.ToArray());

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 2.5f, 2.5f) }, new PartitionSettings { MaxClaim = 1e9f, PocketCells = 50 });

        Assert.AreEqual(ProvinceMap.Unclaimed, map[9, 9]);
    }

    [TestMethod]
    public void ProvinceAt_WorldPosition_ReadsTheCellUnderIt()
    {
        var cells = new TerrainClass[4 * 4];
        var grid = new TerrainGrid(4, 4, 100f, 200f, 10f, cells);

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(7, 105f, 205f) }, Unlimited);

        Assert.AreEqual(7, map.ProvinceAt(135f, 235f));
        Assert.AreEqual(ProvinceMap.Unclaimed, map.ProvinceAt(99f, 205f), "outside the grid is unclaimed");
    }

    [TestMethod]
    public void Partition_NoSeeds_ClaimsNothing()
    {
        var map = ProvincePartitioner.Partition(Grid(5, 5), new ProvinceSeed[0], Unlimited);

        Assert.IsTrue(Enumerable.Range(0, 25).All(i => map[i % 5, i / 5] == ProvinceMap.Unclaimed));
    }

    [DataTestMethod]
    [DataRow(TerrainClass.Wall)]
    [DataRow(TerrainClass.Water)]
    public void Partition_OneCellDiagonalBarrier_IsNotSlippedThrough(TerrainClass barrier)
    {
        var grid = Grid(20, 20, Enumerable.Range(0, 20).Select(i => (i, i, barrier)).ToArray());

        var map = ProvincePartitioner.Partition(grid, new[] { Seed(0, 2.5f, 15.5f) }, Unlimited);

        Assert.AreEqual(0, map[2, 15], "the seed's own side");
        Assert.AreEqual(ProvinceMap.Unclaimed, map[15, 2], "a one-cell diagonal ridge or channel stops the flood");
    }

    [TestMethod]
    public void Partition_SeedAtANaNPosition_StartsNothing()
    {
        var map = ProvincePartitioner.Partition(Grid(5, 5), new[] { Seed(0, float.NaN, 2.5f), Seed(1, 2.5f, float.NaN) }, Unlimited);

        for (int row = 0; row < 5; row++)
            for (int column = 0; column < 5; column++)
                Assert.AreEqual(ProvinceMap.Unclaimed, map[column, row]);
    }

    [TestMethod]
    public void ProvinceAt_NotAFinitePosition_IsUnclaimed()
    {
        var map = ProvincePartitioner.Partition(Grid(4, 4), new[] { Seed(0, 1.5f, 1.5f) }, Unlimited);

        Assert.AreEqual(0, map.ProvinceAt(1.5f, 1.5f));
        Assert.AreEqual(ProvinceMap.Unclaimed, map.ProvinceAt(float.NaN, 1.5f));
        Assert.AreEqual(ProvinceMap.Unclaimed, map.ProvinceAt(1.5f, float.PositiveInfinity));
        Assert.AreEqual(ProvinceMap.Unclaimed, map.ProvinceAt(float.NegativeInfinity, 1.5f));
    }
}
