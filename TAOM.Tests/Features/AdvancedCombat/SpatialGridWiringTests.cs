using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The grid's own orchestration on live-shaped agents (plan 033, Codex review): the first query after skipped
/// rebuilds answers from a fresh build, the rebuild publishes a map together with its agent-to-cell index, and a
/// removal reaches the agent in both map generations. The helpers' tests drive plain points; these drive the
/// instance, with bare agents and a position and liveness seam so no native pointer is read.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class SpatialGridWiringTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    // What HeldAgents() yields for an agent that both map generations hold: one cell-list entry and one agent-to-cell
    // index entry in each of the two maps. A count of 2 is one generation, so a membership test cannot tell them apart.
    private const int HeldByBothGenerations = 4;

    private static int TimesHeld(SpatialGrid grid, Agent agent) => grid.HeldAgents().Count(held => ReferenceEquals(held, agent));

    /// <summary>The mission as the grid sees it: the live agent list the mission tick hands UpdateGrid, and the
    /// position and liveness the grid reads from each agent.</summary>
    private sealed class Battlefield
    {
        private readonly Dictionary<Agent, Vec3> _positions = new();
        private readonly HashSet<Agent> _inactive = new();

        public Battlefield()
        {
            Grid = new SpatialGrid
            {
                InfoLog = _ => { },
                PositionOf = agent => _positions[agent],
                IsLive = agent => !_inactive.Contains(agent),
            };
        }

        public SpatialGrid Grid { get; }

        /// <summary>Stands in for <c>Mission.AllAgents</c>.</summary>
        public List<Agent> AllAgents { get; } = new();

        public Agent Spawn(float x, float y)
        {
            Agent agent = BareAgent();
            _positions[agent] = new Vec3(x, y, 0f);
            AllAgents.Add(agent);
            return agent;
        }

        public void Move(Agent agent, float x, float y) => _positions[agent] = new Vec3(x, y, 0f);

        public void Shift(Agent agent, float dx, float dy) =>
            _positions[agent] = new Vec3(_positions[agent].x + dx, _positions[agent].y + dy, 0f);

        /// <summary>Killed but not yet deleted: still in <c>AllAgents</c>, no longer active.</summary>
        public void Kill(Agent agent) => _inactive.Add(agent);

        /// <summary>The engine's order in <c>Mission.OnAgentDeleted</c>: the state turns Deleted, every behavior's
        /// <c>OnAgentDeleted</c> runs (which calls <c>SpatialGrid.Remove</c>), then <c>AllAgents</c> drops it.</summary>
        public void Delete(Agent agent)
        {
            _inactive.Add(agent);
            Grid.Remove(agent);
            AllAgents.Remove(agent);
        }

        public void Tick() => Grid.UpdateGrid(AllAgents);

        public List<Agent> Near(float x, float y, float radius) => Near(new Vec3(x, y, 0f), radius);

        public List<Agent> Near(Vec3 center, float radius)
        {
            var buffer = new List<Agent>();
            Grid.GetAgentsInRadius(center, radius, buffer);
            return buffer;
        }

        /// <summary>The oracle: every live-list agent within the sphere by its current position.</summary>
        public List<Agent> WithinRadius(Vec3 center, float radius) =>
            AllAgents.Where(agent =>
            {
                Vec3 p = _positions[agent];
                float dx = p.x - center.x, dy = p.y - center.y, dz = p.z - center.z;
                return dx * dx + dy * dy + dz * dz <= radius * radius;
            }).ToList();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    // The first query after skipped rebuilds rebuilds before it answers. Each test changes the field during the idle
    // spell in a way the unread build cannot know about, so an answer from that build is visibly wrong.

    [TestMethod]
    public void GetAgentsInRadius_AfterSkippedRebuilds_FindsAnAgentThatSpawnedDuringTheIdleSpell()
    {
        var field = new Battlefield();
        Agent early = field.Spawn(1f, 1f);
        field.Tick();
        Agent late = field.Spawn(2f, 2f);
        field.Tick();
        field.Tick();

        List<Agent> found = field.Near(0f, 0f, 10f);

        CollectionAssert.AreEquivalent(new[] { early, late }, found);
    }

    [TestMethod]
    public void GetAgentsInRadius_AfterSkippedRebuilds_FindsAnAgentThatMovedIntoRangeDuringTheIdleSpell()
    {
        var field = new Battlefield();
        Agent walker = field.Spawn(100f, 100f);
        field.Tick();
        field.Move(walker, 3f, 3f);
        field.Tick();
        field.Tick();

        List<Agent> found = field.Near(0f, 0f, 10f);

        CollectionAssert.AreEqual(new[] { walker }, found);
    }

    [TestMethod]
    public void GetAgentsInRadius_AfterSkippedRebuilds_DropsAnAgentThatDiedDuringTheIdleSpell()
    {
        var field = new Battlefield();
        Agent survivor = field.Spawn(1f, 1f);
        Agent casualty = field.Spawn(2f, 2f);
        field.Tick();
        field.Kill(casualty);
        field.Tick();
        field.Tick();

        List<Agent> found = field.Near(0f, 0f, 10f);

        CollectionAssert.AreEqual(new[] { survivor }, found);
    }

    [TestMethod]
    public void GetAgentsInRadius_WithNoSkippedRebuild_AnswersFromTheLastScheduledBuild()
    {
        var field = new Battlefield();
        Agent early = field.Spawn(1f, 1f);
        field.Tick();
        Agent late = field.Spawn(2f, 2f);

        List<Agent> beforeTheNextBuild = field.Near(0f, 0f, 10f);
        field.Tick();
        List<Agent> afterTheNextBuild = field.Near(0f, 0f, 10f);

        CollectionAssert.AreEqual(new[] { early }, beforeTheNextBuild, "a query rebuilds only after a skipped rebuild");
        CollectionAssert.AreEquivalent(new[] { early, late }, afterTheNextBuild);
        Assert.AreEqual(2, field.Grid.BuildCount);
    }

    // The rebuild fills the spare map and its index and publishes both. A removal finds the agent by the published
    // index, so a build that published the map but not its index (or the reverse) leaves the agent in the answers.

    [TestMethod]
    public void Remove_AfterTwoBuilds_DropsTheAgentFromTheNextAnswer()
    {
        var field = new Battlefield();
        Agent doomed = field.Spawn(1f, 1f);
        Agent bystander = field.Spawn(2f, 2f);
        field.Tick();
        field.Near(0f, 0f, 10f);
        field.Tick();

        field.Delete(doomed);

        CollectionAssert.AreEqual(new[] { bystander }, field.Near(0f, 0f, 10f));
    }

    // A deleted agent keeps its Mission reference (Agent.OnDelete does not clear it, and the mission's end clears only
    // the agents still in AllAgents), so a handle left in the spare map keeps a finished mission reachable. While
    // nothing queries, no rebuild reaches the spare map, so only the removal can clear it.
    [TestMethod]
    public void Remove_AfterTwoBuildsThenAnIdleSpell_LeavesNeitherMapGenerationHoldingTheAgent()
    {
        var field = new Battlefield();
        Agent doomed = field.Spawn(1f, 1f);
        Agent bystander = field.Spawn(2f, 2f);
        field.Tick();
        field.Near(0f, 0f, 10f);
        field.Tick();
        Assert.AreEqual(HeldByBothGenerations, TimesHeld(field.Grid, doomed), "both generations hold the agent before it is deleted");

        field.Delete(doomed);
        field.Tick();
        field.Tick();

        Assert.IsFalse(field.Grid.HeldAgents().Contains(doomed), "a map generation still holds the deleted agent");
        CollectionAssert.Contains(field.Grid.HeldAgents().ToList(), bystander, "the inspector must still see the agents that remain");
    }

    // Only the mission tick edits a cell list, the spare map's included: a removal raised off the main thread (native
    // places OnAgentDeleted, #634) waits for ApplyPendingRemovals.
    [TestMethod]
    public void Remove_OffTheMainThread_ReachesBothMapsOnlyWhenTheMissionTickAppliesIt()
    {
        var field = new Battlefield();
        Agent doomed = field.Spawn(1f, 1f);
        field.Tick();
        field.Near(0f, 0f, 10f);
        field.Tick();

        var worker = new Thread(() => field.Grid.Remove(doomed));
        worker.Start();
        worker.Join();
        int timesHeldWhileParked = TimesHeld(field.Grid, doomed);
        field.Grid.ApplyPendingRemovals();

        Assert.AreEqual(HeldByBothGenerations, timesHeldWhileParked, "the worker edited a map generation; only the mission tick may");
        Assert.IsFalse(field.Grid.HeldAgents().Contains(doomed), "a map generation still holds the agent after the mission tick applied the removal");
    }

    [TestMethod]
    public void Remove_AfterEachOfSeveralBuilds_DropsTheAgentFromBothMapsAndKeepsEveryAnswerRight()
    {
        var field = new Battlefield();
        List<Agent> agents = Enumerable.Range(0, 12).Select(i => field.Spawn(i * 7f, i * -5f)).ToList();
        var center = new Vec3(30f, -20f, 0f);
        const float radius = 40f;

        for (int round = 0; round < 6; round++)
        {
            foreach (Agent agent in field.AllAgents)
                field.Shift(agent, 3f, -2f);
            field.Tick();
            CollectionAssert.AreEquivalent(field.WithinRadius(center, radius), field.Near(center, radius),
                $"round {round}: the answer after the build");

            Agent doomed = agents[round];
            field.Delete(doomed);

            CollectionAssert.AreEquivalent(field.WithinRadius(center, radius), field.Near(center, radius),
                $"round {round}: the answer after deleting agent {round}");
            Assert.IsFalse(field.Grid.HeldAgents().Contains(doomed), $"round {round}: a map generation still holds agent {round}");
            CollectionAssert.IsSubsetOf(field.AllAgents, field.Grid.HeldAgents().ToList(),
                $"round {round}: the removal took an agent besides agent {round}");
        }
    }
}
