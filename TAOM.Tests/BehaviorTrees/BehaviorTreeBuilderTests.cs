using System;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.BehaviorTrees;

// Characterization tests for the inlined BehaviorTrees builder (Main/BehaviorTrees/BehaviorTreesCore.cs)
// — previously a vendored binary with ZERO test coverage despite being load-bearing for all four creature
// features (Warg/Spider/Elephant/Mumakil trees). These pin the semantics the creature BTs depend on:
//   1. Blackboard reflection-copy: a node implementing a blackboard interface receives the TREE's
//      BTBlackboardValue INSTANCES (shared state, not copies) at Add* time.
//   2. A node whose blackboard interface the tree does NOT implement fails the build loudly.
//   3. A get-only blackboard property fails the build loudly (builder requires get+set).
//   4. Non-blackboard interfaces on nodes are ignored.
//   5. Copy happens at Add* time — reassigning a tree blackboard property AFTER build does not
//      propagate (creature trees MUST initialize blackboard values in their ctor, before BuildTree).
//   6. Decorators passed to AddSelector/AddSequence get the same copy treatment as tasks.
//   7. Up() beyond the root throws.
// (R3, round-2 untested-pure-logic target, 2026-07-01.)
[TestClass]
public class BehaviorTreeBuilderTests
{
    // ------------------------------------------------------------------ fixtures

    public interface IBTCounterBlackboard : IBTBlackboard
    {
        BTBlackboardValue<int> Counter { get; set; }
    }

    public interface IBTOrphanBlackboard : IBTBlackboard
    {
        BTBlackboardValue<int> Orphan { get; set; }
    }

    public interface IBTGetOnlyBlackboard : IBTBlackboard
    {
        BTBlackboardValue<int> ReadOnlyValue { get; }
    }

    private sealed class CounterTask : BTTask, IBTCounterBlackboard
    {
        public BTBlackboardValue<int> Counter { get; set; }

        public override BTTaskStatus Execute()
        {
            Counter.SetValue(Counter.GetValue() + 1);
            return BTTaskStatus.FinishedWithTrue;
        }
    }

    private sealed class OrphanTask : BTTask, IBTOrphanBlackboard
    {
        public BTBlackboardValue<int> Orphan { get; set; }
        public override BTTaskStatus Execute() => BTTaskStatus.FinishedWithTrue;
    }

    private sealed class DisposableTask : BTTask, IDisposable
    {
        public override BTTaskStatus Execute() => BTTaskStatus.FinishedWithTrue;
        public void Dispose() { }
    }

    private sealed class CounterDecorator : BTReturnFalseDecorator, IBTCounterBlackboard
    {
        public BTBlackboardValue<int> Counter { get; set; }
        public override bool Evaluate() => true;
    }

    private sealed class CounterTree : global::BehaviorTrees.BehaviorTree, IBTCounterBlackboard
    {
        public BTBlackboardValue<int> Counter { get; set; } = new BTBlackboardValue<int>(0);

        public CounterTask Task { get; } = new CounterTask();
        public CounterDecorator Decorator { get; } = new CounterDecorator();

        public CounterTree() : base(10) { }

        public static CounterTree BuildValid()
        {
            var tree = new CounterTree();
            StartBuildingTree(tree)
                .AddSelector("root selector", tree.Decorator)
                    .AddTask(tree.Task)
                .Up()
                .Finish();
            return tree;
        }

        public static void BuildWithOrphanTask()
        {
            var tree = new CounterTree();
            StartBuildingTree(tree)
                .AddSelector("root selector")
                    .AddTask(new OrphanTask());
        }

        public static void BuildWithNonBlackboardInterfaceTask()
        {
            var tree = new CounterTree();
            StartBuildingTree(tree)
                .AddSelector("root selector")
                    .AddTask(new DisposableTask());
        }

        public static void UpBeyondRoot()
        {
            StartBuildingTree(new CounterTree()).Up();
        }

        public static (CounterTree tree, CounterTask task) BuildThenReplaceTreeValue()
        {
            var tree = BuildValid();
            tree.Counter = new BTBlackboardValue<int>(99);
            return (tree, tree.Task);
        }
    }

    private sealed class GetOnlyTask : BTTask, IBTGetOnlyBlackboard
    {
        public BTBlackboardValue<int> ReadOnlyValue { get; } = new BTBlackboardValue<int>(0);
        public override BTTaskStatus Execute() => BTTaskStatus.FinishedWithTrue;
    }

    private sealed class GetOnlyTree : global::BehaviorTrees.BehaviorTree, IBTGetOnlyBlackboard
    {
        public BTBlackboardValue<int> ReadOnlyValue { get; } = new BTBlackboardValue<int>(0);

        public GetOnlyTree() : base(10) { }

        public static void Build()
        {
            StartBuildingTree(new GetOnlyTree())
                .AddSelector("root")
                    .AddTask(new GetOnlyTask());
        }
    }

    // ------------------------------------------------------------------ blackboard copy

    [TestMethod]
    public void AddTask_NodeWithTreeBlackboard_SharesTheTreeInstance()
    {
        var tree = CounterTree.BuildValid();
        Assert.AreSame(tree.Counter, tree.Task.Counter,
            "the builder must copy the tree's BTBlackboardValue INSTANCE onto the node (shared state)");
    }

    [TestMethod]
    public void AddTask_SharedBlackboard_MutationThroughNodeIsVisibleOnTree()
    {
        var tree = CounterTree.BuildValid();
        tree.Task.Counter.SetValue(41);
        Assert.AreEqual(41, tree.Counter.GetValue());
    }

    [TestMethod]
    public void AddSelector_DecoratorWithTreeBlackboard_SharesTheTreeInstance()
    {
        var tree = CounterTree.BuildValid();
        Assert.AreSame(tree.Counter, tree.Decorator.Counter,
            "decorators passed to AddSelector get the same reflection-copy as tasks");
    }

    [TestMethod]
    public void AddTask_CopyHappensAtAddTime_LaterTreeReassignmentDoesNotPropagate()
    {
        var (tree, task) = CounterTree.BuildThenReplaceTreeValue();
        Assert.AreNotSame(tree.Counter, task.Counter,
            "blackboard values are copied at Add* time; creature trees must initialize them in the ctor");
        Assert.AreEqual(0, task.Counter.GetValue());
        Assert.AreEqual(99, tree.Counter.GetValue());
    }

    // ------------------------------------------------------------------ build-time failure modes

    [TestMethod]
    public void AddTask_NodeBlackboardInterfaceMissingOnTree_ThrowsMissingTreeBlackBoard()
    {
        var ex = Assert.ThrowsException<BehaviorTreeBuilder<CounterTree>.MissingTreeBlackBoardException>(
            () => CounterTree.BuildWithOrphanTask());
        StringAssert.Contains(ex.Message, "IBTOrphanBlackboard");
        StringAssert.Contains(ex.Message, "OrphanTask");
    }

    [TestMethod]
    public void AddTask_GetOnlyBlackboardProperty_ThrowsIncorrectProperty()
    {
        var ex = Assert.ThrowsException<BehaviorTreeBuilder<GetOnlyTree>.IncorrectPropertyException>(
            () => GetOnlyTree.Build());
        StringAssert.Contains(ex.Message, "ReadOnlyValue");
    }

    [TestMethod]
    public void AddTask_NonBlackboardInterfaceOnNode_IsIgnored()
        // IDisposable on a task must not trigger the blackboard machinery.
        => CounterTree.BuildWithNonBlackboardInterfaceTask();

    [TestMethod]
    public void Up_BeyondRoot_ThrowsAlreadyInRoot()
        => Assert.ThrowsException<BehaviorTreeBuilder<CounterTree>.AlreadyInRootException>(
            () => CounterTree.UpBeyondRoot());

    // ------------------------------------------------------------------ execution smoke

    [TestMethod]
    public void RunTree_SingleAlwaysTrueTask_ExecutesTaskAndStaysRunning()
    {
        var tree = CounterTree.BuildValid();
        tree.RunTree();
        Assert.IsTrue(tree.IsRunning, "a trivially-true tree must not kill itself");
        Assert.AreEqual(1, tree.Counter.GetValue(), "the task must have executed exactly once");
    }
}
