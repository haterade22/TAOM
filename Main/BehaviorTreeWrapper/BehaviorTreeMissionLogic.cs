using System;
using System.Collections.Generic;
using System.Threading;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTrees;
using TAOM;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Callback = BehaviorTreeWrapper.CallbackSkipLedger.Callback;

namespace BehaviorTreeWrapper;

// Inherits MissionLogic (was MissionBehavior in the vendored DLL). The original
// class declared `BehaviorType => Logic` while inheriting MissionBehavior, which
// made vanilla `Mission.AddMissionBehavior` push a null into `_missionLogics`
// via `MissionLogics.Add(this as MissionLogic)` — causing an NRE every tick
// inside `Mission.CheckMissionEnded`. RCA: docs/reviews/rca-looter-battle-nre-2026-05-24.md.
public class BehaviorTreeMissionLogic : MissionLogic
{
    private readonly Dictionary<SubscriptionPossibilities, List<BannerlordBTListener>> actions
        = new Dictionary<SubscriptionPossibilities, List<BannerlordBTListener>>();

    public Dictionary<Agent, BehaviorTree> trees = new Dictionary<Agent, BehaviorTree>();

    private readonly List<(BannerlordBTTickListener listener, double elapsedTime)> tickListeners
        = new List<(BannerlordBTTickListener, double)>();

    // Trees tick from here, on the main thread (#592). BehaviorTreeAgentComponent schedules itself
    // on construction and unschedules on OnAgentRemoved; the engine's own component tick is a no-op.
    private readonly List<BehaviorTreeAgentComponent> _scheduled = new List<BehaviorTreeAgentComponent>();
    private readonly List<BehaviorTreeAgentComponent> _tickScratch = new List<BehaviorTreeAgentComponent>();

    // Hot-path allocation caches (deep-review 2026-05-24 E1–E3): reused across
    // synchronous notify dispatch so per-frame and per-agent-event arrays/lists
    // do not allocate. Safe because each helper call is consumed inline by the
    // immediate-next foreach in the same handler; no listener notification re-enters
    // a Find/Notify cycle on this mission logic.
    private static readonly object[] EmptyArgs = Array.Empty<object>();
    private readonly object[] _dtArgs = new object[1];
    private readonly List<BannerlordBTListener> _tempMatched = new List<BannerlordBTListener>();
    // True while NotifyAll walks _tempMatched. A listener that lands a blow re-enters OnAgentHit and
    // FindCalledListeners; the inner call then gets its own list instead of clearing the outer one.
    private bool _dispatching;

    // Engine callbacks that arrive off the main thread are parked here and replayed at the top of
    // OnMissionTick, so the maps above are only ever touched from the mission tick. Verified route on
    // v1.4.8: CommonAIComponent.OnTick runs inside the asynchronous agent tick and calls Panic ->
    // Mission.OnAgentPanicked synchronously (CommonAIComponent.cs:119-135). Which thread a native
    // [MBCallback] such as Agent.OnAgentAlarmedStateChanged uses is not established, so every
    // callback asks (#595, Codex review 109).
    // The report runs on the thread that raised the callback, so it goes to the file log (which locks),
    // never to BTRegister.Logger: that can be BannerlordLogger, an on-screen message (#634).
    private static readonly Action<string> ReportOffThread = message =>
    {
        try { IoC.Resolve<IModLogger>()?.LogWarning(message); }
        catch { /* a report must never throw into an engine callback */ }
    };
    private readonly DeferredCallbackQueue _deferred = new DeferredCallbackQueue(ReportOffThread);

    /// <summary>Engine callbacks parked for the next mission tick.</summary>
    public int DeferredCount => _deferred.Count;

    // How many listeners each SubscriptionPossibilities value has. Subscribe and UnSubscribe write it on the
    // mission thread; a callback reads it on whichever thread native raised it, so an event nobody listens
    // to returns before it allocates arguments or parks a replay (plan 033).
    private static readonly int SubscriptionKinds = Enum.GetValues(typeof(SubscriptionPossibilities)).Length;
    private readonly int[] _listenerCounts = new int[SubscriptionKinds];

    // So taom_debug.log keeps what an early return no longer shows: one reason line at the mission's first skip,
    // then the mission-end summary with every skip and every parked replay counted.
    private readonly CallbackSkipLedger _ledger = new CallbackSkipLedger();

    /// <summary>Where this logic's INFO lines go: the file log, or a test's capture.</summary>
    internal Action<string> InfoLog { get; set; } = LogInfoToFile;

    private static void LogInfoToFile(string message)
    {
        try { IoC.Resolve<IModLogger>()?.LogInfo(message); }
        catch { /* a log line must never throw into an engine callback */ }
    }

    private bool Listening(SubscriptionPossibilities kind) => Volatile.Read(ref _listenerCounts[(int)kind]) > 0;

    private bool Listens(Callback callback, SubscriptionPossibilities kind) =>
        Listening(kind) || Skip(callback);

    private bool Listens(Callback callback, SubscriptionPossibilities a, SubscriptionPossibilities b) =>
        Listening(a) || Listening(b) || Skip(callback);

    private bool Listens(Callback callback, SubscriptionPossibilities a, SubscriptionPossibilities b, SubscriptionPossibilities c) =>
        Listening(a) || Listening(b) || Listening(c) || Skip(callback);

    // Counts a callback that returns early; the mission's first one also writes the reason line. Any thread.
    private bool Skip(Callback callback)
    {
        string? reason = _ledger.NoteSkip(callback);
        if (reason != null)
            InfoLog(reason);
        return false;
    }

    public void Subscribe(BannerlordBTListener listener)
    {
        if (!actions.TryGetValue(listener.SubscribesTo, out List<BannerlordBTListener> value))
        {
            value = new List<BannerlordBTListener>();
            actions[listener.SubscribesTo] = value;
        }
        value.Add(listener);
        Interlocked.Increment(ref _listenerCounts[(int)listener.SubscribesTo]);
    }

    public void UnSubscribe(BannerlordBTListener listener)
    {
        if (actions[listener.SubscribesTo].Remove(listener))
            Interlocked.Decrement(ref _listenerCounts[(int)listener.SubscribesTo]);
    }

    public void Subscribe(BannerlordBTTickListener listener)
    {
        tickListeners.Add((listener, 0.0));
    }

    public void UnSubscribe(BannerlordBTTickListener listener)
    {
        for (int i = 0; i < tickListeners.Count; i++)
        {
            if (tickListeners[i].listener == listener)
            {
                tickListeners.RemoveAt(i);
                break;
            }
        }
    }

    internal void Schedule(BehaviorTreeAgentComponent component)
    {
        if (component.IsScheduled) return;
        component.IsScheduled = true;
        _scheduled.Add(component);
    }

    internal void Unschedule(BehaviorTreeAgentComponent component)
    {
        if (!component.IsScheduled) return;
        component.IsScheduled = false;
        _scheduled.Remove(component);
    }

    /// <summary>Components currently ticked from the mission tick.</summary>
    public int ScheduledCount => _scheduled.Count;

    public List<BannerlordBTListener> GetAllListeners()
    {
        var list = new List<BannerlordBTListener>();
        foreach (var value in actions.Values)
            list.AddRange(value);
        return list;
    }

    public BehaviorTreeMissionLogic()
    {
        BehaviorTreeBannerlordWrapper.Instance.CurrentMissionLogic = this;
    }

    public override void OnMissionTick(float dt)
    {
        // This is the main mission thread, the only thread that may touch the maps above. Callbacks
        // that arrived from another thread since the last tick are replayed first (#595).
        MissionThreadGuard.MarkMainThread();
        _deferred.Drain();

        // Every scheduled tree runs here, in the managed mission-tick phase, where the engine's agent
        // callbacks cannot interleave with it. A run can kill an agent and unschedule its component
        // (or another's) mid-loop, so iterate a snapshot and honour the flag. Copied element by element:
        // List.AddRange of a collection allocates a temporary array of the whole schedule, every frame, and the
        // race abilities put every profiled soldier on it.
        _tickScratch.Clear();
        for (int i = 0; i < _scheduled.Count; i++)
            _tickScratch.Add(_scheduled[i]);
        for (int i = 0; i < _tickScratch.Count; i++)
        {
            BehaviorTreeAgentComponent component = _tickScratch[i];
            if (component.IsScheduled)
                component.TickOnMissionThread(dt);
        }

        _dtArgs[0] = dt;
        for (int num = tickListeners.Count - 1; num >= 0; num--)
        {
            var (listener, elapsedTime) = tickListeners[num];
            elapsedTime += dt;
            if (elapsedTime >= listener.SecondsTillEvent)
            {
                elapsedTime = 0.0;
                tickListeners[num] = (listener, elapsedTime);
                listener.Notify(_dtArgs);
                if (tickListeners.Count == 0)
                    break;
            }
            else
            {
                tickListeners[num] = (listener, elapsedTime);
            }
        }
    }

    // True, and reported once per site, when the engine raised a callback on a thread other than the
    // mission tick's; the caller then parks the callback instead of touching the maps.
    private static bool OffMainThread(string site)
    {
        if (MissionThreadGuard.IsOnMainThread) return false;
        MissionThreadGuard.NoteCall(site, ReportOffThread);
        return true;
    }

    // The replay lambda captures only `this`, so the main-thread path allocates nothing.
    private void Defer<T>(T args, Action<T> replay)
    {
        _ledger.NoteParked();
        _deferred.Enqueue(() => replay(args));
    }

    /// <summary>
    /// Runs <paramref name="action"/> now on the mission thread, else parks it behind the callbacks
    /// already waiting, so a component's cleanup replays after this logic's own callbacks for the same
    /// removal, the order the main thread would have used (#634).
    /// </summary>
    internal void RunOnMissionThread(string site, Action action) => _deferred.RunOrDefer(site, action);

    // Returns a SHARED cached list. Caller MUST iterate immediately and not
    // retain a reference past the next FindCalledListeners call. The synchronous
    // notify-dispatch pattern in this class makes that safe — no event handler
    // ever re-enters FindCalledListeners on the same mission logic instance.
    private List<BannerlordBTListener> FindCalledListeners(Agent agent, SubscriptionPossibilities action)
    {
        List<BannerlordBTListener> matched = _dispatching ? new List<BannerlordBTListener>() : _tempMatched;
        matched.Clear();
        if (!actions.TryGetValue(action, out var value) || value == null)
            return matched;
        var agentTree = agent.GetBehaviorTree();
        foreach (var item in value)
        {
            if (agentTree == item.Tree)
                matched.Add(item);
        }
        return matched;
    }

    // One line per listener type per mission: a throwing listener is a bug to fix, not a flood.
    private readonly HashSet<Type> _throwingListeners = new HashSet<Type>();

    // Every listener walk, per-tree and global alike, goes through here (#595).
    private void NotifyAll(List<BannerlordBTListener> listeners, object[] data)
    {
        bool outer = !_dispatching;
        _dispatching = true;
        try
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                // Dispatched from Mission.OnAgentHit / OnAgentRemoved, whose loops over behaviors
                // have no catch: an exception here would skip every later behavior's hit or
                // removal handling for this agent and propagate toward native (#595).
                try
                {
                    listeners[i].Notify(data);
                }
                catch (Exception ex)
                {
                    // Once per listener type per mission, and only once a logger exists: marking the
                    // type before the message could be written would lose the report for good.
                    var logger = BTRegister.Logger;
                    if (logger != null && _throwingListeners.Add(listeners[i].GetType()))
                        logger.LogMessage(
                            $"Behavior tree listener {listeners[i].GetType().Name} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            if (outer) _dispatching = false;
        }
    }

    // Every callback below: the thread tripwire first (it reports an off-thread site once per process), then
    // an early return when none of the values it dispatches has a listener, so nothing is allocated or parked.
    // Off-thread, an event raised while nothing listens is dropped, as the main-thread path drops it; the two
    // differ only when a first listener subscribes between the asynchronous agent tick and the next drain.

    public override void OnAgentDismount(Agent agent)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentDismount");
        if (!Listens(Callback.OnAgentDismount, SubscriptionPossibilities.OnSelfDismount, SubscriptionPossibilities.OnAgentDismount)) return;
        if (offThread) { Defer(agent, a => OnAgentDismount(a)); return; }
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfDismount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentDismount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentFleeing");
        if (!Listens(Callback.OnAgentFleeing, SubscriptionPossibilities.OnSelfFleeing, SubscriptionPossibilities.OnAgentFleeing)) return;
        if (offThread) { Defer(affectedAgent, a => OnAgentFleeing(a)); return; }
        object[]? args = null;
        var self = FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfFleeing);
        if (self.Count > 0)
            NotifyAll(self, args = new object[] { affectedAgent });
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentFleeing, out var globalListeners) && globalListeners != null && globalListeners.Count > 0)
            NotifyAll(globalListeners, args ?? new object[] { affectedAgent });
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentDeleted");
        if (!Listens(Callback.OnAgentDeleted, SubscriptionPossibilities.OnSelfAlarmedStateChanged)) return;
        if (offThread) { Defer(affectedAgent, a => OnAgentDeleted(a)); return; }
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfAlarmedStateChanged), EmptyArgs);
    }

    public override void OnAgentMount(Agent agent)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentMount");
        if (!Listens(Callback.OnAgentMount, SubscriptionPossibilities.OnSelfMount, SubscriptionPossibilities.OnAgentMount)) return;
        if (offThread) { Defer(agent, a => OnAgentMount(a)); return; }
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfMount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentMount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentPanicked(Agent affectedAgent)
    {
        // Raised from CommonAIComponent.OnTick inside the asynchronous agent tick (v1.4.8): the one
        // engine route into this class that is known to arrive off the main thread.
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentPanicked");
        if (!Listens(Callback.OnAgentPanicked, SubscriptionPossibilities.OnAgentPanicked)) return;
        if (offThread) { Defer(affectedAgent, a => OnAgentPanicked(a)); return; }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentPanicked, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { affectedAgent });
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentRemoved");
        if (!Listens(Callback.OnAgentRemoved, SubscriptionPossibilities.OnSelfRemoved, SubscriptionPossibilities.OnSelfKilledEnemy,
                SubscriptionPossibilities.OnAgentRemoved)) return;
        if (offThread)
        {
            Defer((affectedAgent, affectorAgent, agentState, blow),
                t => OnAgentRemoved(t.affectedAgent, t.affectorAgent, t.agentState, t.blow));
            return;
        }
        var selfRemoved = FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfRemoved);
        if (selfRemoved.Count > 0)
            NotifyAll(selfRemoved, new object[] { affectorAgent, agentState, blow });
        if (affectorAgent != null)
        {
            var killedEnemy = FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfKilledEnemy);
            if (killedEnemy.Count > 0)
                NotifyAll(killedEnemy, new object[] { affectedAgent, agentState, blow });
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentRemoved, out var globalListeners) && globalListeners != null && globalListeners.Count > 0)
            NotifyAll(globalListeners, new object[] { affectedAgent, affectorAgent, agentState, blow });
    }

    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentShootMissile");
        if (!Listens(Callback.OnAgentShootMissile, SubscriptionPossibilities.OnAgentShootMissile)) return;
        if (offThread)
        {
            Defer((shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex),
                t => OnAgentShootMissile(t.shooterAgent, t.weaponIndex, t.position, t.velocity, t.orientation, t.hasRigidBody, t.forcedMissileIndex));
            return;
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentShootMissile, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex });
    }

    public override void OnFocusGained(Agent agent, IFocusable focusableObject, bool isInteractable)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnFocusGained");
        if (!Listens(Callback.OnFocusGained, SubscriptionPossibilities.OnSelfGainedFocus)) return;
        if (offThread)
        {
            Defer((agent, focusableObject, isInteractable), t => OnFocusGained(t.agent, t.focusableObject, t.isInteractable));
            return;
        }
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfGainedFocus);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { focusableObject, isInteractable });
    }

    public override void OnFocusLost(Agent agent, IFocusable focusableObject)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnFocusLost");
        if (!Listens(Callback.OnFocusLost, SubscriptionPossibilities.OnSelfLostFocus)) return;
        if (offThread) { Defer((agent, focusableObject), t => OnFocusLost(t.agent, t.focusableObject)); return; }
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfLostFocus);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { focusableObject });
    }

    public override void OnAgentAlarmedStateChanged(Agent agent, Agent.AIStateFlag flag)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentAlarmedStateChanged");
        if (!Listens(Callback.OnAgentAlarmedStateChanged, SubscriptionPossibilities.OnSelfAlarmedStateChanged)) return;
        if (offThread) { Defer((agent, flag), t => OnAgentAlarmedStateChanged(t.agent, t.flag)); return; }
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfAlarmedStateChanged);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { flag });
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnAgentHit");
        if (!Listens(Callback.OnAgentHit, SubscriptionPossibilities.OnSelfIsHit, SubscriptionPossibilities.OnSelfHitsEnemy)) return;
        if (offThread)
        {
            Defer((affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData),
                t => OnAgentHit(t.affectedAgent, t.affectorAgent, in t.affectorWeapon, in t.blow, in t.attackCollisionData));
            return;
        }
        var hitMatched = FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfIsHit);
        if (hitMatched.Count > 0)
            NotifyAll(hitMatched, new object[] { affectorAgent, affectorWeapon, blow, attackCollisionData });
        if (affectorAgent != null)
        {
            var hitsEnemyMatched = FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfHitsEnemy);
            if (hitsEnemyMatched.Count > 0)
                NotifyAll(hitsEnemyMatched, new object[] { affectedAgent, affectorWeapon, blow, attackCollisionData });
        }
    }

    public override void OnObjectUsed(Agent userAgent, UsableMissionObject usedObject)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnObjectUsed");
        if (!Listens(Callback.OnObjectUsed, SubscriptionPossibilities.OnSelfUsedObject)) return;
        if (offThread) { Defer((userAgent, usedObject), t => OnObjectUsed(t.userAgent, t.usedObject)); return; }
        var matched = FindCalledListeners(userAgent, SubscriptionPossibilities.OnSelfUsedObject);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { usedObject });
    }

    protected override void OnObjectDisabled(DestructableComponent destructionComponent)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnObjectDisabled");
        if (!Listens(Callback.OnObjectDisabled, SubscriptionPossibilities.OnObjectDisabled)) return;
        if (offThread) { Defer(destructionComponent, c => OnObjectDisabled(c)); return; }
        if (actions.TryGetValue(SubscriptionPossibilities.OnObjectDisabled, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { destructionComponent });
    }

    public override void OnObjectStoppedBeingUsed(Agent userAgent, UsableMissionObject usedObject)
    {
        bool offThread = OffMainThread("BehaviorTreeMissionLogic.OnObjectStoppedBeingUsed");
        if (!Listens(Callback.OnObjectStoppedBeingUsed, SubscriptionPossibilities.OnSelfStoppedUsingObject)) return;
        if (offThread) { Defer((userAgent, usedObject), t => OnObjectStoppedBeingUsed(t.userAgent, t.usedObject)); return; }
        var matched = FindCalledListeners(userAgent, SubscriptionPossibilities.OnSelfStoppedUsingObject);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { usedObject });
    }

    public override void OnEndMissionInternal()
    {
        // Mission-end cleanup (deep-review 2026-05-24 E5): clear all subscription
        // state so a new Mission starts with empty listener lists. The original
        // vendored DLL leaked these across mission boundaries.
        InfoLog(_ledger.Summary());
        actions.Clear();
        Array.Clear(_listenerCounts, 0, _listenerCounts.Length);
        _ledger.Reset();
        tickListeners.Clear();
        trees.Clear();
        _tempMatched.Clear();
        _scheduled.Clear();
        _tickScratch.Clear();
        _throwingListeners.Clear();
        _deferred.Clear();
        BehaviorTreeBannerlordWrapper.Instance.Dispose();
    }
}
