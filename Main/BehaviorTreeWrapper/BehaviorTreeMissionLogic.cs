using System;
using System.Collections.Generic;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTrees;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

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
    private readonly DeferredCallbackQueue _deferred = new DeferredCallbackQueue();
    private static readonly Action<string> ReportOffThread = message => BTRegister.Logger?.LogMessage(message);

    /// <summary>Engine callbacks parked for the next mission tick.</summary>
    public int DeferredCount => _deferred.Count;

    public void Subscribe(BannerlordBTListener listener)
    {
        if (!actions.TryGetValue(listener.SubscribesTo, out List<BannerlordBTListener> value))
        {
            value = new List<BannerlordBTListener>();
            actions[listener.SubscribesTo] = value;
        }
        value.Add(listener);
    }

    public void UnSubscribe(BannerlordBTListener listener)
    {
        actions[listener.SubscribesTo].Remove(listener);
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
        // (or another's) mid-loop, so iterate a snapshot and honour the flag.
        _tickScratch.Clear();
        _tickScratch.AddRange(_scheduled);
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
    private void Defer<T>(T args, Action<T> replay) => _deferred.Enqueue(() => replay(args));

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

    public override void OnAgentDismount(Agent agent)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentDismount")) { Defer(agent, a => OnAgentDismount(a)); return; }
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfDismount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentDismount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentFleeing")) { Defer(affectedAgent, a => OnAgentFleeing(a)); return; }
        var selfArgs = new object[] { affectedAgent };
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfFleeing), selfArgs);
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentFleeing, out var globalListeners) && globalListeners != null)
            NotifyAll(globalListeners, selfArgs);
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentDeleted")) { Defer(affectedAgent, a => OnAgentDeleted(a)); return; }
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfAlarmedStateChanged), EmptyArgs);
    }

    public override void OnAgentMount(Agent agent)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentMount")) { Defer(agent, a => OnAgentMount(a)); return; }
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfMount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentMount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentPanicked(Agent affectedAgent)
    {
        // Raised from CommonAIComponent.OnTick inside the asynchronous agent tick (v1.4.8): the one
        // engine route into this class that is known to arrive off the main thread.
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentPanicked")) { Defer(affectedAgent, a => OnAgentPanicked(a)); return; }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentPanicked, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { affectedAgent });
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentRemoved"))
        {
            Defer((affectedAgent, affectorAgent, agentState, blow),
                t => OnAgentRemoved(t.affectedAgent, t.affectorAgent, t.agentState, t.blow));
            return;
        }
        var selfRemovedArgs = new object[] { affectorAgent, agentState, blow };
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfRemoved), selfRemovedArgs);
        if (affectorAgent != null)
        {
            var killedArgs = new object[] { affectedAgent, agentState, blow };
            NotifyAll(FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfKilledEnemy), killedArgs);
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentRemoved, out var globalListeners) && globalListeners != null)
            NotifyAll(globalListeners, new object[] { affectedAgent, affectorAgent, agentState, blow });
    }

    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentShootMissile"))
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
        if (OffMainThread("BehaviorTreeMissionLogic.OnFocusGained"))
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
        if (OffMainThread("BehaviorTreeMissionLogic.OnFocusLost")) { Defer((agent, focusableObject), t => OnFocusLost(t.agent, t.focusableObject)); return; }
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfLostFocus);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { focusableObject });
    }

    public override void OnAgentAlarmedStateChanged(Agent agent, Agent.AIStateFlag flag)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentAlarmedStateChanged")) { Defer((agent, flag), t => OnAgentAlarmedStateChanged(t.agent, t.flag)); return; }
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfAlarmedStateChanged);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { flag });
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnAgentHit"))
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
        if (OffMainThread("BehaviorTreeMissionLogic.OnObjectUsed")) { Defer((userAgent, usedObject), t => OnObjectUsed(t.userAgent, t.usedObject)); return; }
        var matched = FindCalledListeners(userAgent, SubscriptionPossibilities.OnSelfUsedObject);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { usedObject });
    }

    protected override void OnObjectDisabled(DestructableComponent destructionComponent)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnObjectDisabled")) { Defer(destructionComponent, c => OnObjectDisabled(c)); return; }
        if (actions.TryGetValue(SubscriptionPossibilities.OnObjectDisabled, out var listeners) && listeners != null)
            NotifyAll(listeners, new object[] { destructionComponent });
    }

    public override void OnObjectStoppedBeingUsed(Agent userAgent, UsableMissionObject usedObject)
    {
        if (OffMainThread("BehaviorTreeMissionLogic.OnObjectStoppedBeingUsed")) { Defer((userAgent, usedObject), t => OnObjectStoppedBeingUsed(t.userAgent, t.usedObject)); return; }
        var matched = FindCalledListeners(userAgent, SubscriptionPossibilities.OnSelfStoppedUsingObject);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { usedObject });
    }

    public override void OnEndMissionInternal()
    {
        // Mission-end cleanup (deep-review 2026-05-24 E5): clear all subscription
        // state so a new Mission starts with empty listener lists. The original
        // vendored DLL leaked these across mission boundaries.
        actions.Clear();
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
