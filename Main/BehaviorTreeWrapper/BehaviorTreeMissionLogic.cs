using System;
using System.Collections.Generic;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using BehaviorTrees;
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

    // Hot-path allocation caches (deep-review 2026-05-24 E1–E3): reused across
    // synchronous notify dispatch so per-frame and per-agent-event arrays/lists
    // do not allocate. Safe because each helper call is consumed inline by the
    // immediate-next foreach in the same handler; no listener notification re-enters
    // a Find/Notify cycle on this mission logic.
    private static readonly object[] EmptyArgs = Array.Empty<object>();
    private readonly object[] _dtArgs = new object[1];
    private readonly List<BannerlordBTListener> _tempMatched = new List<BannerlordBTListener>();

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

    // Returns a SHARED cached list. Caller MUST iterate immediately and not
    // retain a reference past the next FindCalledListeners call. The synchronous
    // notify-dispatch pattern in this class makes that safe — no event handler
    // ever re-enters FindCalledListeners on the same mission logic instance.
    private List<BannerlordBTListener> FindCalledListeners(Agent agent, SubscriptionPossibilities action)
    {
        _tempMatched.Clear();
        if (!actions.TryGetValue(action, out var value) || value == null)
            return _tempMatched;
        var agentTree = agent.GetBehaviorTree();
        foreach (var item in value)
        {
            if (agentTree == item.Tree)
                _tempMatched.Add(item);
        }
        return _tempMatched;
    }

    private static void NotifyAll(List<BannerlordBTListener> listeners, object[] data)
    {
        for (int i = 0; i < listeners.Count; i++)
            listeners[i].Notify(data);
    }

    public override void OnAgentDismount(Agent agent)
    {
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfDismount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentDismount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        var selfArgs = new object[] { affectedAgent };
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfFleeing), selfArgs);
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentFleeing, out var globalListeners) && globalListeners != null)
        {
            for (int i = 0; i < globalListeners.Count; i++)
                globalListeners[i].Notify(selfArgs);
        }
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfAlarmedStateChanged), EmptyArgs);
    }

    public override void OnAgentMount(Agent agent)
    {
        NotifyAll(FindCalledListeners(agent, SubscriptionPossibilities.OnSelfMount), EmptyArgs);
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnAgentMount);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { agent });
    }

    public override void OnAgentPanicked(Agent affectedAgent)
    {
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentPanicked, out var listeners) && listeners != null)
        {
            var args = new object[] { affectedAgent };
            for (int i = 0; i < listeners.Count; i++)
                listeners[i].Notify(args);
        }
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        var selfRemovedArgs = new object[] { affectorAgent, agentState, blow };
        NotifyAll(FindCalledListeners(affectedAgent, SubscriptionPossibilities.OnSelfRemoved), selfRemovedArgs);
        if (affectorAgent != null)
        {
            var killedArgs = new object[] { affectedAgent, agentState, blow };
            NotifyAll(FindCalledListeners(affectorAgent, SubscriptionPossibilities.OnSelfKilledEnemy), killedArgs);
        }
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentRemoved, out var globalListeners) && globalListeners != null)
        {
            var args = new object[] { affectedAgent, affectorAgent, agentState, blow };
            for (int i = 0; i < globalListeners.Count; i++)
                globalListeners[i].Notify(args);
        }
    }

    public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
    {
        if (actions.TryGetValue(SubscriptionPossibilities.OnAgentShootMissile, out var listeners) && listeners != null)
        {
            var args = new object[] { shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex };
            for (int i = 0; i < listeners.Count; i++)
                listeners[i].Notify(args);
        }
    }

    public override void OnFocusGained(Agent agent, IFocusable focusableObject, bool isInteractable)
    {
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfGainedFocus);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { focusableObject, isInteractable });
    }

    public override void OnFocusLost(Agent agent, IFocusable focusableObject)
    {
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfLostFocus);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { focusableObject });
    }

    public override void OnAgentAlarmedStateChanged(Agent agent, Agent.AIStateFlag flag)
    {
        var matched = FindCalledListeners(agent, SubscriptionPossibilities.OnSelfAlarmedStateChanged);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { flag });
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
    {
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
        var matched = FindCalledListeners(userAgent, SubscriptionPossibilities.OnSelfUsedObject);
        if (matched.Count > 0)
            NotifyAll(matched, new object[] { usedObject });
    }

    protected override void OnObjectDisabled(DestructableComponent destructionComponent)
    {
        if (actions.TryGetValue(SubscriptionPossibilities.OnObjectDisabled, out var listeners) && listeners != null)
        {
            var args = new object[] { destructionComponent };
            for (int i = 0; i < listeners.Count; i++)
                listeners[i].Notify(args);
        }
    }

    public override void OnObjectStoppedBeingUsed(Agent userAgent, UsableMissionObject usedObject)
    {
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
        BehaviorTreeBannerlordWrapper.Instance.Dispose();
    }
}
