using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

[TestClass]
public class TroopUpgradeGraphTests
{
    private readonly TroopUpgradeGraph _sut = new TroopUpgradeGraph();

    private static Dictionary<string, List<string>> LinearChain() => new Dictionary<string, List<string>>
    {
        ["tier1"] = new List<string> { "tier2" },
        ["tier2"] = new List<string> { "tier3" },
        ["tier3"] = new List<string> { "tier4" },
        ["tier4"] = new List<string> { "tier5" },
    };

    [TestMethod]
    public void FindDescendantInRoster_DirectChildInRoster_ReturnsChild()
    {
        var graph = new Dictionary<string, List<string>> { ["a"] = new List<string> { "b" } };
        var inRoster = new HashSet<string> { "b" };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, inRoster.Contains);

        Assert.AreEqual("b", result);
    }

    [TestMethod]
    public void FindDescendantInRoster_NoDescendantInRoster_ReturnsNull()
    {
        var graph = new Dictionary<string, List<string>> { ["a"] = new List<string> { "b" } };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, _ => false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindDescendantInRoster_DepthWithinCap_FindsGrandchild()
    {
        // a -> b -> c, depth 2 (within the cap of 3)
        var graph = new Dictionary<string, List<string>>
        {
            ["a"] = new List<string> { "b" },
            ["b"] = new List<string> { "c" },
        };
        var inRoster = new HashSet<string> { "c" };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, inRoster.Contains);

        Assert.AreEqual("c", result);
    }

    [TestMethod]
    public void FindDescendantInRoster_BeyondDepthCap_ReturnsNull()
    {
        var chain = LinearChain(); // tier1 -> tier2 -> tier3 -> tier4 -> tier5 (depth 4 to reach tier5)
        var inRoster = new HashSet<string> { "tier5" };

        var result = _sut.FindDescendantInRoster(
            "tier1",
            id => chain.TryGetValue(id, out var t) ? t : null,
            inRoster.Contains);

        Assert.IsNull(result, "tier5 is 4 upgrade hops from tier1 — beyond the depth-3 cap.");
    }

    [TestMethod]
    public void FindDescendantInRoster_WithinDepthCap_FindsTarget()
    {
        var chain = LinearChain(); // tier1 -> tier2 -> tier3 -> tier4 (depth 3, at the cap)
        var inRoster = new HashSet<string> { "tier4" };

        var result = _sut.FindDescendantInRoster(
            "tier1",
            id => chain.TryGetValue(id, out var t) ? t : null,
            inRoster.Contains);

        Assert.AreEqual("tier4", result);
    }

    [TestMethod]
    public void FindDescendantInRoster_CyclicGraph_TerminatesAndReturnsNull()
    {
        // a -> b -> a (a two-node cycle). Without a visited set this would loop forever.
        var graph = new Dictionary<string, List<string>>
        {
            ["a"] = new List<string> { "b" },
            ["b"] = new List<string> { "a" },
        };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, _ => false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindDescendantInRoster_CyclicGraphWithTargetPresent_FindsTargetAndTerminates()
    {
        // a -> b -> c -> a (cycle), target is c.
        var graph = new Dictionary<string, List<string>>
        {
            ["a"] = new List<string> { "b" },
            ["b"] = new List<string> { "c" },
            ["c"] = new List<string> { "a" },
        };
        var inRoster = new HashSet<string> { "c" };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, inRoster.Contains);

        Assert.AreEqual("c", result);
    }

    [TestMethod]
    public void FindDescendantInRoster_SelfReferencingNode_TerminatesAndReturnsNull()
    {
        var graph = new Dictionary<string, List<string>> { ["a"] = new List<string> { "a" } };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, _ => false);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindDescendantInRoster_NoUpgradeTargets_ReturnsNull()
    {
        var result = _sut.FindDescendantInRoster("a", _ => null, _ => true);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindDescendantInRoster_MultipleBranches_ReturnsFirstMatchInBreadthFirstOrder()
    {
        var graph = new Dictionary<string, List<string>>
        {
            ["a"] = new List<string> { "b", "c" },
            ["b"] = new List<string> { "d" },
            ["c"] = new List<string> { "e" },
        };
        var inRoster = new HashSet<string> { "e", "d" }; // both present; "d" is under the FIRST branch "b"

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, inRoster.Contains);

        Assert.AreEqual("d", result, "BFS visits branch order [b, c] before descending — d (under b) must win over e (under c).");
    }

    [TestMethod]
    public void FindDescendantInRoster_NullUpgradeTargetInList_SkipsWithoutThrowing()
    {
        var graph = new Dictionary<string, List<string>> { ["a"] = new List<string> { null, "b" } };
        var inRoster = new HashSet<string> { "b" };

        var result = _sut.FindDescendantInRoster("a", id => graph.TryGetValue(id, out var t) ? t : null, inRoster.Contains);

        Assert.AreEqual("b", result);
    }
}
