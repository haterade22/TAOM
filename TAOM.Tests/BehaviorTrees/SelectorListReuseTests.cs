using System.Collections.Generic;
using System.Reflection;
using BehaviorTrees;
using BehaviorTrees.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.BehaviorTrees;

/// <summary>
/// A running creature tree re-enters every selector each frame, and <c>Selector.Prepare</c> used to allocate its two
/// child lists on every re-entry. It now clears and refills the lists it already has (plan 033).
/// </summary>
[TestClass]
public class SelectorListReuseTests
{
    private sealed class CountTask : BTTask
    {
        public int Runs;
        public override BTTaskStatus Execute() { Runs++; return BTTaskStatus.FinishedWithTrue; }
    }

    /// <summary>An event gate that never opens: <c>Prepare</c> files its node under <c>childrenWithTasks</c>.</summary>
    private sealed class NeverOpenEventDecorator : BTEventDecorator
    {
        public override bool Evaluate() => false;
        public override void Notify(object[] data) { }
        public override void CreateListener() { }
    }

    private sealed class OneSelectorTree : global::BehaviorTrees.BehaviorTree
    {
        public CountTask Task { get; } = new CountTask();
        public CountTask GatedTask { get; } = new CountTask();
        public OneSelectorTree() : base(10) { }

        /// <summary>
        /// One selector with an undecorated task (it runs and finishes the selector with true) and a child selector
        /// behind a closed event gate (every <c>Prepare</c> files it as event-waiting; it never runs, and the
        /// gate's listener is never subscribed because the first child finishes the selector).
        /// </summary>
        public static OneSelectorTree Build()
        {
            var tree = new OneSelectorTree();
            StartBuildingTree(tree)
                .AddSelector("selector")
                    .AddTask(tree.Task)
                    .AddSelector("gated", new NeverOpenEventDecorator()).AddTask(tree.GatedTask).Up()
                .Up()
                .Finish();
            return tree;
        }
    }

    private static readonly FieldInfo AllChildren =
        typeof(BTControlNode).GetField("allChildren", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo CurrentlyExecutableChildren =
        typeof(BTControlNode).GetField("currentlyExecutableChildren", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ChildrenWithTasks =
        typeof(Selector).GetField("childrenWithTasks", BindingFlags.NonPublic | BindingFlags.Instance);

    [TestMethod]
    public void Prepare_OnReEntry_ReusesBothChildLists()
    {
        // Arrange
        OneSelectorTree tree = OneSelectorTree.Build();
        var rootChildren = (List<BTNode>)AllChildren.GetValue((BTControlNode)tree.RootNode);
        var selector = (Selector)rootChildren[0];

        // Act
        tree.RunTree();
        object executableAfterFirstRun = CurrentlyExecutableChildren.GetValue(selector);
        object withTasksAfterFirstRun = ChildrenWithTasks.GetValue(selector);
        tree.RunTree();

        // Assert: both runs re-entered Prepare (one task run per RunTree), then the lists are the same instances.
        Assert.AreEqual(2, tree.Task.Runs, "each RunTree must re-enter the selector and run its task once");
        Assert.AreEqual(0, tree.GatedTask.Runs, "the closed event gate must keep its child from running");
        Assert.AreSame(executableAfterFirstRun, CurrentlyExecutableChildren.GetValue(selector),
            "Selector.Prepare allocated a new currentlyExecutableChildren list; clear the existing one");
        Assert.AreSame(withTasksAfterFirstRun, ChildrenWithTasks.GetValue(selector),
            "Selector.Prepare allocated a new childrenWithTasks list; clear the existing one");
        // A reused list must hold what a fresh one would: one undecorated child, one event-waiting child.
        Assert.AreEqual(1, ((List<BTNode>)CurrentlyExecutableChildren.GetValue(selector)).Count,
            "Selector.Prepare kept the previous entry's executable children; clear the list before refilling it");
        Assert.AreEqual(1, ((List<BTNode>)ChildrenWithTasks.GetValue(selector)).Count,
            "Selector.Prepare kept the previous entry's event-waiting children; clear the list before refilling it");
    }
}
