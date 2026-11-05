using System.Collections.Generic;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.BehaviorTrees;

/// <summary>
/// RunTree walks nodes until the walk returns to the root or a node waits, and nothing bounded that loop: two
/// nodes that hand control back and forth would hold the mission thread for ever (plan 124, FREEZE-THREADING-09).
/// These pin the cap, the exact-length boundary and the once-per-tree-type log line.
/// </summary>
[TestClass]
public class BehaviorTreeRunawayCapTests
{
    // Ends an uncapped walk so a missing cap fails these tests instead of hanging the run.
    private const int UncappedEscapeCall = 100_000;

    private sealed class CaptureLogger : ILogger
    {
        public List<string> Lines { get; } = new();
        public void LogMessage(string message) => Lines.Add(message);
    }

    private sealed class CallCounter { public int Calls; }

    // Hands control to Next on every call and never waits; on call number escapeCall it hands control to End.
    private sealed class HandOffNode : BTNode
    {
        private readonly CallCounter _counter;
        private readonly int _escapeCall;
        public HandOffNode(CallCounter counter, int escapeCall) { _counter = counter; _escapeCall = escapeCall; }
        public BTNode Next { get; set; } = null!;
        public BTNode End { get; set; } = null!;
        public override BTNode HandleExecute() => ++_counter.Calls >= _escapeCall ? End : Next;
    }

    // The root: a walk that reaches it is complete.
    private sealed class EndNode : BTNode { public override BTNode HandleExecute() => this; }

    private sealed class CyclingTree : BehaviorTree { }
    private sealed class BoundaryTree : BehaviorTree { }
    private sealed class ReportedTree : BehaviorTree { }

    private ILogger? _previousLogger;
    private CaptureLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousLogger = BTRegister.Logger;
        _logger = new CaptureLogger();
        BTRegister.AddLogger(_logger);
    }

    [TestCleanup]
    public void Cleanup() => BTRegister.AddLogger(_previousLogger!);

    private static CallCounter Wire(BehaviorTree tree, int escapeCall)
    {
        var counter = new CallCounter();
        var end = new EndNode();
        var first = new HandOffNode(counter, escapeCall) { End = end };
        var second = new HandOffNode(counter, escapeCall) { End = end, Next = first };
        first.Next = second;
        tree.RootNode = end;
        tree.CurrentNode = first;
        return counter;
    }

    [TestMethod]
    public void RunTree_NodesThatHandControlBackAndForth_StopAtTheCap()
    {
        var tree = new CyclingTree();
        CallCounter counter = Wire(tree, UncappedEscapeCall);
        tree.RunTree();
        Assert.IsFalse(tree.IsRunning, "a walk that never reaches the root or a waiting node must stop the tree");
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls,
            "one call for the unexecuted first node, then exactly the capped number of transitions");
        tree.RunTree();
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls, "a stopped tree never walks again");
    }

    [TestMethod]
    public void RunTree_AWalkOfExactlyTheCap_CompletesAndKeepsRunning()
    {
        var tree = new BoundaryTree();
        // The first call is the unexecuted first node's; the last allowed transition returns the root.
        CallCounter counter = Wire(tree, BehaviorTree.MaxNodeTransitionsPerRun + 1);
        tree.RunTree();
        Assert.IsTrue(tree.IsRunning, "a walk that reaches the root within the cap is a normal walk");
        Assert.AreEqual(BehaviorTree.MaxNodeTransitionsPerRun + 1, counter.Calls);
        Assert.AreEqual(0, _logger.Lines.Count, "a normal walk logs nothing");
    }

    [TestMethod]
    public void RunTree_RunawaysOfOneTreeType_AreLoggedOnce()
    {
        var first = new ReportedTree();
        var second = new ReportedTree();
        Wire(first, UncappedEscapeCall);
        Wire(second, UncappedEscapeCall);
        first.RunTree();
        second.RunTree();
        Assert.AreEqual(1, _logger.Lines.Count, "one line per tree type, however many of its trees run away");
        StringAssert.Contains(_logger.Lines[0], nameof(ReportedTree));
        Assert.IsFalse(second.IsRunning, "the second tree stops even though its runaway is not logged again");
    }
}
