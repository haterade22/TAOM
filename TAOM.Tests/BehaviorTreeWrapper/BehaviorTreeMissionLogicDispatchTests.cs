using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using BehaviorTrees;
using BehaviorTreeWrapper;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.BehaviorTreeWrapper;

/// <summary>
/// <c>BehaviorTreeMissionLogic</c> routes 14 engine callbacks to tree listeners, and TAOM's trees listen to only three
/// of its 20 subscription values. A callback nobody listens to returns before it builds argument arrays or parks an
/// off-thread replay (plan 033); a callback somebody listens to is delivered exactly as before.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BehaviorTreeMissionLogicDispatchTests
{
    private const long AllocationBudgetBytes = 1024;

    private static readonly Func<long>? AllocatedBytes = BindAllocatedBytes();

    private BehaviorTreeMissionLogic _logic = null!;
    private ILogger? _savedLogger;

    private sealed class StubTree : global::BehaviorTrees.BehaviorTree
    {
        public StubTree() : base(10) { }
    }

    private sealed class RecordingNotifiable : IBTNotifiable
    {
        public BTListener Listener { get; set; } = null!;
        public BehaviorTree Tree { get; set; } = null!;
        public int Calls;
        public object[]? LastData;

        public void HandleNotification(object[] data)
        {
            Calls++;
            LastData = data;
        }

        public void CreateListener() { }
    }

    [TestInitialize]
    public void Setup()
    {
        _savedLogger = BTRegister.Logger;
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _logic = new BehaviorTreeMissionLogic();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _logic.OnEndMissionInternal();
        MissionThreadGuard.ResetForTests();
        BTRegister.AddLogger(_savedLogger!);
    }

    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static BannerlordBTListener Listener(SubscriptionPossibilities kind, RecordingNotifiable? notifiable = null) =>
        new BannerlordBTListener(kind, new StubTree(), notifiable ?? new RecordingNotifiable());

    private static void RunOnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    private static Func<long>? BindAllocatedBytes()
    {
        MethodInfo? method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
            BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
    }

    private static long BytesFor1000Calls(Action action)
    {
        if (AllocatedBytes == null)
            Assert.Inconclusive("allocation probe unavailable: GC.GetAllocatedBytesForCurrentThread did not bind");
        action();
        long before = AllocatedBytes!();
        for (int i = 0; i < 1000; i++)
            action();
        return AllocatedBytes() - before;
    }

    [TestMethod]
    public void OnAgentShootMissile_OffThreadWithNoListener_ParksNothing()
    {
        RunOnWorker(() => _logic.OnAgentShootMissile(null, EquipmentIndex.None, default, default, default, false, 0));

        Assert.AreEqual(0, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentRemoved_OffThreadWithNoListener_ParksNothing()
    {
        RunOnWorker(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));

        Assert.AreEqual(0, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentHit_OffThreadWithNoListener_ParksNothing()
    {
        RunOnWorker(() =>
        {
            MissionWeapon weapon = default;
            Blow blow = default;
            AttackCollisionData collision = default;
            _logic.OnAgentHit(null, null, in weapon, in blow, in collision);
        });

        Assert.AreEqual(0, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentRemoved_OffThreadWithAGlobalListener_ParksTheReplay()
    {
        _logic.Subscribe(Listener(SubscriptionPossibilities.OnAgentRemoved));

        RunOnWorker(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));

        Assert.AreEqual(1, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentRemoved_AfterTheLastListenerUnsubscribes_ParksNothing()
    {
        BannerlordBTListener listener = Listener(SubscriptionPossibilities.OnAgentRemoved);
        _logic.Subscribe(listener);
        _logic.UnSubscribe(listener);

        RunOnWorker(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));

        Assert.AreEqual(0, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnEndMissionInternal_ForgetsTheListeners()
    {
        _logic.Subscribe(Listener(SubscriptionPossibilities.OnAgentRemoved));
        _logic.OnEndMissionInternal();

        RunOnWorker(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));

        Assert.AreEqual(0, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentRemoved_OnTheMainThreadWithAGlobalListener_NotifiesItWithTheRemovalArguments()
    {
        var notifiable = new RecordingNotifiable();
        _logic.Subscribe(Listener(SubscriptionPossibilities.OnAgentRemoved, notifiable));

        _logic.OnAgentRemoved(null, null, AgentState.Killed, default);

        Assert.AreEqual(1, notifiable.Calls);
        Assert.IsNotNull(notifiable.LastData);
        Assert.AreEqual(4, notifiable.LastData!.Length);
        Assert.IsNull(notifiable.LastData[0]);
        Assert.IsNull(notifiable.LastData[1]);
        Assert.AreEqual(AgentState.Killed, notifiable.LastData[2]);
        Assert.AreEqual(default(KillingBlow), notifiable.LastData[3]);
    }

    // The two self values TAOM's trees listen to (OnSelfRemoved: OnWargDied, OnSpiderDied; OnSelfIsHit: WargTryToGoRage)
    // sit behind the early return too; a value missing from a callback's gate would silently stop the death cleanup
    // or the rage.

    private Agent AgentWithTree(out StubTree tree)
    {
        Agent agent = BareAgent();
        tree = new StubTree();
        _logic.trees[agent] = tree;
        return agent;
    }

    [TestMethod]
    public void OnAgentRemoved_OnTheMainThreadWithASelfRemovedListener_NotifiesIt()
    {
        Agent agent = AgentWithTree(out StubTree tree);
        var notifiable = new RecordingNotifiable();
        _logic.Subscribe(new BannerlordBTListener(SubscriptionPossibilities.OnSelfRemoved, tree, notifiable));

        _logic.OnAgentRemoved(agent, null, AgentState.Killed, default);

        Assert.AreEqual(1, notifiable.Calls);
        Assert.AreEqual(AgentState.Killed, notifiable.LastData![1]);
    }

    [TestMethod]
    public void OnAgentRemoved_OffThreadWithASelfRemovedListener_ParksTheReplay()
    {
        Agent agent = AgentWithTree(out StubTree tree);
        _logic.Subscribe(new BannerlordBTListener(SubscriptionPossibilities.OnSelfRemoved, tree, new RecordingNotifiable()));

        RunOnWorker(() => _logic.OnAgentRemoved(agent, null, AgentState.Killed, default));

        Assert.AreEqual(1, _logic.DeferredCount);
    }

    [TestMethod]
    public void OnAgentHit_OnTheMainThreadWithASelfIsHitListener_NotifiesIt()
    {
        Agent agent = AgentWithTree(out StubTree tree);
        var notifiable = new RecordingNotifiable();
        _logic.Subscribe(new BannerlordBTListener(SubscriptionPossibilities.OnSelfIsHit, tree, notifiable));
        MissionWeapon weapon = default;
        Blow blow = default;
        AttackCollisionData collision = default;

        _logic.OnAgentHit(agent, null, in weapon, in blow, in collision);

        Assert.AreEqual(1, notifiable.Calls);
        Assert.AreEqual(4, notifiable.LastData!.Length);
    }

    [TestMethod]
    public void OnAgentHit_OffThreadWithASelfIsHitListener_ParksTheReplay()
    {
        Agent agent = AgentWithTree(out StubTree tree);
        _logic.Subscribe(new BannerlordBTListener(SubscriptionPossibilities.OnSelfIsHit, tree, new RecordingNotifiable()));

        RunOnWorker(() =>
        {
            MissionWeapon weapon = default;
            Blow blow = default;
            AttackCollisionData collision = default;
            _logic.OnAgentHit(agent, null, in weapon, in blow, in collision);
        });

        Assert.AreEqual(1, _logic.DeferredCount);
    }

    // Every routed callback, with the subscription values its body dispatches, read from the bodies by hand: the oracle
    // for each callback's early-return gate.
    private static readonly (string Name, Action<BehaviorTreeMissionLogic> Raise, SubscriptionPossibilities[] Values)[] Routes =
    {
        ("OnAgentDismount", l => l.OnAgentDismount(null), new[] { SubscriptionPossibilities.OnSelfDismount, SubscriptionPossibilities.OnAgentDismount }),
        ("OnAgentFleeing", l => l.OnAgentFleeing(null), new[] { SubscriptionPossibilities.OnSelfFleeing, SubscriptionPossibilities.OnAgentFleeing }),
        ("OnAgentDeleted", l => l.OnAgentDeleted(null), new[] { SubscriptionPossibilities.OnSelfAlarmedStateChanged }),
        ("OnAgentMount", l => l.OnAgentMount(null), new[] { SubscriptionPossibilities.OnSelfMount, SubscriptionPossibilities.OnAgentMount }),
        ("OnAgentPanicked", l => l.OnAgentPanicked(null), new[] { SubscriptionPossibilities.OnAgentPanicked }),
        ("OnAgentRemoved", l => l.OnAgentRemoved(null, null, AgentState.Killed, default),
            new[] { SubscriptionPossibilities.OnSelfRemoved, SubscriptionPossibilities.OnSelfKilledEnemy, SubscriptionPossibilities.OnAgentRemoved }),
        ("OnAgentShootMissile", l => l.OnAgentShootMissile(null, EquipmentIndex.None, default, default, default, false, 0),
            new[] { SubscriptionPossibilities.OnAgentShootMissile }),
        ("OnFocusGained", l => l.OnFocusGained(null, null, false), new[] { SubscriptionPossibilities.OnSelfGainedFocus }),
        ("OnFocusLost", l => l.OnFocusLost(null, null), new[] { SubscriptionPossibilities.OnSelfLostFocus }),
        ("OnAgentAlarmedStateChanged", l => l.OnAgentAlarmedStateChanged(null, default),
            new[] { SubscriptionPossibilities.OnSelfAlarmedStateChanged }),
        ("OnAgentHit", l =>
        {
            MissionWeapon weapon = default;
            Blow blow = default;
            AttackCollisionData collision = default;
            l.OnAgentHit(null, null, in weapon, in blow, in collision);
        }, new[] { SubscriptionPossibilities.OnSelfIsHit, SubscriptionPossibilities.OnSelfHitsEnemy }),
        ("OnObjectUsed", l => l.OnObjectUsed(null, null), new[] { SubscriptionPossibilities.OnSelfUsedObject }),
        ("OnObjectDisabled", l => OnObjectDisabledMethod.Invoke(l, new object?[] { null }),
            new[] { SubscriptionPossibilities.OnObjectDisabled }),
        ("OnObjectStoppedBeingUsed", l => l.OnObjectStoppedBeingUsed(null, null), new[] { SubscriptionPossibilities.OnSelfStoppedUsingObject }),
    };

    private static readonly MethodInfo OnObjectDisabledMethod = typeof(BehaviorTreeMissionLogic).GetMethod(
        "OnObjectDisabled", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [TestMethod]
    public void EveryCallback_OffThread_ParksExactlyWhenOneOfItsOwnValuesHasAListener()
    {
        Assert.IsNotNull(OnObjectDisabledMethod, "OnObjectDisabled was not found; the table needs re-planning");
        var wrong = new System.Collections.Generic.List<string>();
        foreach (var route in Routes)
        {
            foreach (SubscriptionPossibilities value in Enum.GetValues(typeof(SubscriptionPossibilities)))
            {
                var logic = new BehaviorTreeMissionLogic { InfoLog = _ => { } };
                logic.Subscribe(Listener(value));

                RunOnWorker(() => route.Raise(logic));

                int expected = Array.IndexOf(route.Values, value) >= 0 ? 1 : 0;
                if (logic.DeferredCount != expected)
                    wrong.Add($"{route.Name} with a {value} listener parked {logic.DeferredCount}, expected {expected}");
                logic.OnEndMissionInternal();
            }
        }
        _logic = new BehaviorTreeMissionLogic();

        Assert.AreEqual(0, wrong.Count, string.Join("; ", wrong));
    }

    [TestMethod]
    public void OnAgentRemoved_OnTheMainThreadWithNoListener_AllocatesNothing()
    {
        long bytes = BytesFor1000Calls(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));

        Assert.IsTrue(bytes < AllocationBudgetBytes, $"1,000 removals with no listener allocated {bytes} bytes");
    }

    [TestMethod]
    public void OnAgentRemoved_WithOnlyAnotherTreesSelfListener_AllocatesNothing()
    {
        _logic.Subscribe(Listener(SubscriptionPossibilities.OnSelfRemoved));
        Agent bareAgent = BareAgent();

        long bytes = BytesFor1000Calls(() => _logic.OnAgentRemoved(bareAgent, null, AgentState.Killed, default));

        Assert.IsTrue(bytes < AllocationBudgetBytes, $"1,000 removals of an agent with no tree allocated {bytes} bytes");
    }

    [TestMethod]
    public void OnAgentFleeing_OnTheMainThreadWithNoListener_AllocatesNothing()
    {
        long bytes = BytesFor1000Calls(() => _logic.OnAgentFleeing(null));

        Assert.IsTrue(bytes < AllocationBudgetBytes, $"1,000 fleeing callbacks with no listener allocated {bytes} bytes");
    }

    // taom_debug.log keeps what the early return no longer does visibly: one reason line per mission at the first
    // skip, and a mission-end summary with every skip and every parked replay counted (DECISIONS D6).

    [TestMethod]
    public void FirstCallbackWithNoListener_LogsOneReasonLineForTheMission()
    {
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        _logic.InfoLog = lines.Enqueue;

        _logic.OnAgentRemoved(null, null, AgentState.Killed, default);
        _logic.OnAgentRemoved(null, null, AgentState.Killed, default);
        RunOnWorker(() => _logic.OnAgentShootMissile(null, EquipmentIndex.None, default, default, default, false, 0));

        CollectionAssert.AreEqual(new[]
        {
            "[BehaviorTree] OnAgentRemoved had no tree listener, so it returned before building arguments or parking " +
            "a replay; every such skip this mission is counted in the mission-end summary.",
        }, lines.ToArray());
    }

    [TestMethod]
    public void OnEndMissionInternal_LogsTheSkipAndReplayCounts()
    {
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        _logic.InfoLog = lines.Enqueue;
        _logic.OnAgentRemoved(null, null, AgentState.Killed, default);
        _logic.OnAgentRemoved(null, null, AgentState.Killed, default);
        MissionWeapon weapon = default;
        Blow blow = default;
        AttackCollisionData collision = default;
        _logic.OnAgentHit(null, null, in weapon, in blow, in collision);
        _logic.Subscribe(Listener(SubscriptionPossibilities.OnAgentRemoved));
        RunOnWorker(() => _logic.OnAgentRemoved(null, null, AgentState.Killed, default));
        while (lines.TryDequeue(out _)) { }

        _logic.OnEndMissionInternal();

        CollectionAssert.AreEqual(new[]
        {
            "[BehaviorTree] Mission end: 3 callbacks skipped with no tree listener (OnAgentRemoved 2, OnAgentHit 1); " +
            "1 parked off-thread for the mission tick.",
        }, lines.ToArray());
    }

    [TestMethod]
    public void OnEndMissionInternal_AfterAnEarlierSummary_StartsTheCountsAgain()
    {
        var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
        _logic.InfoLog = lines.Enqueue;
        _logic.OnAgentFleeing(null);
        _logic.OnEndMissionInternal();
        while (lines.TryDequeue(out _)) { }

        _logic.OnEndMissionInternal();

        CollectionAssert.AreEqual(new[]
        {
            "[BehaviorTree] Mission end: 0 callbacks skipped with no tree listener; 0 parked off-thread for the mission tick.",
        }, lines.ToArray());
    }
}
