using System;
using BehaviorTrees;
using BehaviorTreeWrapper.AbstractDecoratorsListeners;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BehaviorTreeWrapper;

public class BehaviorTreeBannerlordWrapper : IDisposable
{
    private BehaviorTreeMissionLogic? _missionLogic;
    private static BehaviorTreeBannerlordWrapper? _instance;
    private bool _disposed;

    public BehaviorTreeMissionLogic? CurrentMissionLogic
    {
        get => _missionLogic;
        set
        {
            _missionLogic = value;
            _disposed = false;
        }
    }

    public static BehaviorTreeBannerlordWrapper Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = new BehaviorTreeBannerlordWrapper();
            RegisterDefaultTrees();
            return _instance;
        }
    }

    public void Subscribe(BannerlordBTListener listener) => CurrentMissionLogic?.Subscribe(listener);
    public void UnSubscribe(BannerlordBTListener listener) => CurrentMissionLogic?.UnSubscribe(listener);
    public void Subscribe(BannerlordBTTickListener listener) => CurrentMissionLogic?.Subscribe(listener);
    public void UnSubscribe(BannerlordBTTickListener listener) => CurrentMissionLogic?.UnSubscribe(listener);

    private static void RegisterDefaultTrees()
    {
        // PerformAnAttackTree registration was dropped 2026-05-24 — no TAOM tree consumed
        // "PerformAnAttackTree" via BTRegister.Build. TAOM features register their own
        // trees (e.g. "SpiderTree", "WargTree") from their respective MissionBehaviors.
        BTRegister.AddLogger(new BannerlordLogger());
    }

    internal BehaviorTree? AddBehaviorTree(string treeName, object[] args)
    {
        var logic = CurrentMissionLogic;
        if (logic == null)
        {
            InformationManager.DisplayMessage(new InformationMessage("Error adding a tree: no current MissionLogic"));
            return null;
        }
        var agent = (Agent)args[0];
        if (logic.trees.TryGetValue(agent, out _))
        {
            InformationManager.DisplayMessage(new InformationMessage("Error adding a tree to " + agent.Name + " as it already exists"));
        }
        try
        {
            var behaviorTree = BTRegister.Build(treeName, args);
            if (behaviorTree == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Error building a tree with name " + treeName));
                return null;
            }
            logic.trees[agent] = behaviorTree;
            return behaviorTree;
        }
        catch (Exception ex)
        {
            InformationManager.DisplayMessage(new InformationMessage("Error building a tree " + treeName + " for " + agent.Name + ". Message " + ex.Message));
            return null;
        }
    }

    public void DisposeTree(Agent agent)
    {
        if (!_disposed && _missionLogic != null)
            _missionLogic.trees.Remove(agent);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CurrentMissionLogic = null;
            _disposed = true;
        }
    }
}
