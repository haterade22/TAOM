using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The always-on grid rebuilds every two seconds in every battle. A build nobody queried makes the next scheduled
/// rebuild a skip, and the next query rebuilds first, so a battle with no warg or spider stops paying for it and a
/// reader never sees cells older than a rebuild would give it (plan 033). Every UpdateGrid here gets an empty agent
/// list, so no native position is read.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class SpatialGridDormancyTests
{
    private const string SkipLine =
        "[SpatialGrid] Scheduled rebuild skipped: nothing queried build 1, so rebuilds pause while nothing reads the " +
        "grid and the next query rebuilds first. Logged once per grid (one grid per mission).";

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    private static SpatialGrid Grid(List<string>? lines = null)
    {
        var grid = new SpatialGrid();
        grid.InfoLog = lines != null ? lines.Add : _ => { };
        return grid;
    }

    private static void Query(SpatialGrid grid) => grid.GetAgentsInRadius(new Vec3(0f, 0f, 0f), 10f, new List<Agent>());

    [TestMethod]
    public void UpdateGrid_FirstCall_Builds()
    {
        SpatialGrid grid = Grid();

        grid.UpdateGrid(new List<Agent>());

        Assert.AreEqual(1, grid.BuildCount);
    }

    [TestMethod]
    public void UpdateGrid_AfterABuildNobodyQueried_SkipsTheRebuild()
    {
        SpatialGrid grid = Grid();

        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());

        Assert.AreEqual(1, grid.BuildCount);
    }

    [TestMethod]
    public void GetAgentsInRadius_OnASkippedGrid_RebuildsBeforeAnswering()
    {
        SpatialGrid grid = Grid();
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());

        Query(grid);

        Assert.AreEqual(2, grid.BuildCount);
    }

    // A query wakes the grid for that one answer: the wake-up rebuild clears the skip count, so the next query reads the
    // build it made. A warg tree scans on every tick, so a rebuild per scan would cost far more than the 2 s schedule.
    [TestMethod]
    public void GetAgentsInRadius_AfterAQueryRebuild_DoesNotRebuildAgain()
    {
        SpatialGrid grid = Grid();
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());
        Query(grid);

        Query(grid);

        Assert.AreEqual(2, grid.BuildCount, "the wake-up rebuild must clear the skip count, or every later query rebuilds");
    }

    [TestMethod]
    public void UpdateGrid_AfterAQueriedBuild_Rebuilds()
    {
        SpatialGrid grid = Grid();
        grid.UpdateGrid(new List<Agent>());
        Query(grid);

        grid.UpdateGrid(new List<Agent>());

        Assert.AreEqual(2, grid.BuildCount);
    }

    // A grid that was read goes dormant again once reading stops: a rebuild marks its own build unread, so the next
    // scheduled rebuild is a skip.
    [TestMethod]
    public void UpdateGrid_AfterAQueriedBuildThenAnUnqueriedOne_SkipsAgain()
    {
        SpatialGrid grid = Grid();
        grid.UpdateGrid(new List<Agent>());
        Query(grid);
        grid.UpdateGrid(new List<Agent>());

        grid.UpdateGrid(new List<Agent>());

        Assert.AreEqual(2, grid.BuildCount, "a rebuild must mark its build unread, or one early query keeps every later rebuild running");
    }

    [TestMethod]
    public void GetAgentsInRadius_OffTheMainThreadOnASkippedGrid_DoesNotRebuild()
    {
        SpatialGrid grid = Grid();
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());

        var worker = new Thread(() => Query(grid));
        worker.Start();
        worker.Join();

        Assert.AreEqual(1, grid.BuildCount, "only the mission thread may build");
    }

    [TestMethod]
    public void GetAgentsInRadius_BeforeAnyUpdate_DoesNotBuild()
    {
        SpatialGrid grid = Grid();

        Query(grid);

        Assert.AreEqual(0, grid.BuildCount);
    }

    // taom_debug.log records the skip and the query rebuild once per grid, with the build number and the skip
    // count (DECISIONS D6).

    [TestMethod]
    public void UpdateGrid_SkippedRebuilds_LogOneReasonLine()
    {
        var lines = new List<string>();
        SpatialGrid grid = Grid(lines);

        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());

        CollectionAssert.AreEqual(new[] { SkipLine }, lines);
    }

    [TestMethod]
    public void GetAgentsInRadius_RebuildsAfterSkips_LogsTheFirstOnce()
    {
        var lines = new List<string>();
        SpatialGrid grid = Grid(lines);
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());

        Query(grid);
        grid.UpdateGrid(new List<Agent>());
        grid.UpdateGrid(new List<Agent>());
        Query(grid);

        CollectionAssert.AreEqual(new[]
        {
            SkipLine,
            "[SpatialGrid] A query found 2 scheduled rebuilds skipped and rebuilt the grid before answering (build 2). " +
            "Logged once per grid (one grid per mission).",
        }, lines);
        Assert.AreEqual(4, grid.BuildCount);
    }
}
