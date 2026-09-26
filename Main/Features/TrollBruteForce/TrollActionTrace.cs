using System;
using System.Collections.Generic;
using System.Reflection;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Crash forensics for the hill troll's first-contact native CTD (TaleWorlds.Native.dll +0x6590B9, a hash-map miss
/// under melee hit processing, 2026-09-25). A native AV leaves no managed stack, so the evidence has to be on disk
/// before it happens: every main-thread tick this logs, on CHANGE only, each troll's channel 0 and 1 actions and the
/// nearest other agent within reach, enemy or ally (packed trolls may swing into each other), with its actions. The
/// FileLogger flushes every write, so the last <c>[TrollTrace]</c> lines before the crash name the swing, the reaction
/// and both agents' Monsters and action sets.
/// Boundary code (raw Agent); game-tested per ADR-008. Remove once the crash is identified.
/// </summary>
public sealed class TrollActionTrace
{
    private const float ContactRange = 8f;   // a hill troll's arm is 2.8 m; 4 m missed the target
    private const float EnemyRange = 12f;    // the nearest agent is always a packed ally; the enemy is logged apart

    private readonly Func<Agent, bool> _isTroll;
    private readonly IModLogger _logger;
    private readonly List<Agent> _trolls = new();
    private readonly Dictionary<Agent, string> _lastState = new();

    public TrollActionTrace(Func<Agent, bool> isTroll, IModLogger logger)
    {
        _isTroll = isTroll;
        _logger = logger;
    }

    public void Track(Agent agent)
    {
        // Called from OnAgentBuild inside Mission.SpawnAgent's unguarded behavior loop (#595): never throw.
        try
        {
            if (agent == null || !_isTroll(agent) || _trolls.Contains(agent)) return;
            _trolls.Add(agent);
            _logger.LogInfo($"[TrollTrace] track {Describe(agent)} skeleton={SkeletonName(agent)} " +
                $"hp={agent.Health} weapon={WeaponId(agent)}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[TrollTrace] track threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Main thread only (OnMissionTick): reads native agent state.</summary>
    public void Tick(IEnumerable<Agent> allAgents)
    {
        for (int i = _trolls.Count - 1; i >= 0; i--)
        {
            Agent troll = _trolls[i];
            if (!troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll))
            {
                _trolls.RemoveAt(i);
                _lastState.Remove(troll);
                continue;
            }

            Agent other = NearestOther(troll, allAgents, out float distance);
            Agent enemy = NearestEnemy(troll, allAgents);
            Agent target = troll.GetTargetAgent();
            // a dead target's slot may already hold another agent (#592): never read native state through it
            if (target != null && (!target.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(target))) target = null;
            // distances stay out of the change key, or every frame of an approach would log
            string state = $"{Actions(troll)} | near=" +
                (other == null ? "none" : $"{(other.IsEnemyOf(troll) ? "enemy" : "ally")} {Describe(other)} {Actions(other)}") +
                " | enemy=" + (enemy == null ? "none" : $"{Describe(enemy)} {Actions(enemy)}") +
                " | target=" + (target == null ? "none" : Describe(target));
            if (_lastState.TryGetValue(troll, out string last) && last == state) continue;
            _lastState[troll] = state;
            _logger.LogInfo($"[TrollTrace] {troll.Index}:{troll.Monster?.StringId} {state}" +
                (other == null ? "" : $" d={distance:0.0}") +
                (enemy == null ? "" : $" enemyD={enemy.Position.Distance(troll.Position):0.0}") +
                (target == null ? "" : $" targetD={target.Position.Distance(troll.Position):0.0}"));
        }
    }

    public void Clear()
    {
        _trolls.Clear();
        _lastState.Clear();
    }

    // The keys the crashing lookup missed, read from r9 in three dumps (2026-09-25): 6511 twice on the swing,
    // 6462 on the first contact. The lookup's caller builds the key from the agent's action set and its current
    // action, so it is a clip index or an action code; this names both readings.
    private static readonly int[] CrashKeys = { 6511, 6462 };
    private static readonly string[] CrashKeySets = { "as_hill_troll_warrior", "as_cave_troll_warrior", "as_human_warrior" };

    /// <summary>Once per mission: names every action whose clip index, or whose own code, is a crash key.</summary>
    public void LogCrashKeys()
    {
        try
        {
            var ctor = typeof(ActionIndexCache).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
            int codes = MBAnimation.GetNumActionCodes();
            _logger.LogInfo($"[TrollTrace] keys: {codes} action codes, {MBAnimation.GetNumAnimations()} animations; " +
                $"looking for {string.Join(", ", CrashKeys)}");
            if (ctor == null) return;
            foreach (int key in CrashKeys)
                if (key < codes)
                    _logger.LogInfo($"[TrollTrace] key {key} as an action code: {((ActionIndexCache)ctor.Invoke(new object[] { key })).GetName()}");

            foreach (string setId in CrashKeySets)
            {
                MBActionSet set = MBActionSet.GetActionSet(setId);
                if (!set.IsValid) continue;
                for (int code = 0; code < codes; code++)
                {
                    var action = (ActionIndexCache)ctor.Invoke(new object[] { code });
                    if (!MBActionSet.CheckActionAnimationClipExists(set, action)) continue;
                    int clip = MBActionSet.GetAnimationIndexOfAction(set, action);
                    string clipName = MBActionSet.GetActionAnimationName(set, action);
                    if (Array.IndexOf(CrashKeys, clip) >= 0)
                        _logger.LogInfo($"[TrollTrace] key {clip} as a clip index: {setId} {action.GetName()} -> {clipName}");
                    // every troll clip the hill troll set still binds, so the key of the next +0x6590B9 dump
                    // resolves from this log alone (the lookup is keyed by clip index)
                    if (setId == "as_hill_troll_warrior" && clipName != null && clipName.StartsWith("anim_hill_troll_"))
                        _logger.LogInfo($"[TrollTrace] clip {clip} = {clipName} ({action.GetName()}, {MBAnimation.GetActionType(action)})");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[TrollTrace] key scan threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Agent NearestOther(Agent troll, IEnumerable<Agent> allAgents, out float distance)
    {
        Agent best = null;
        distance = ContactRange;
        var at = troll.Position;
        foreach (Agent a in allAgents)
        {
            if (a == troll || !a.IsActive() || a.IsMount) continue;
            float d = a.Position.Distance(at);
            if (d < distance)
            {
                distance = d;
                best = a;
            }
        }
        return best;
    }

    private static Agent NearestEnemy(Agent troll, IEnumerable<Agent> allAgents)
    {
        Agent best = null;
        float distance = EnemyRange;
        var at = troll.Position;
        foreach (Agent a in allAgents)
        {
            if (!a.IsActive() || a.IsMount || !a.IsEnemyOf(troll)) continue;
            float d = a.Position.Distance(at);
            if (d < distance)
            {
                distance = d;
                best = a;
            }
        }
        return best;
    }

    // "+banner" marks a banner carrier: Mike saw a standard among the trolls, and neither troll may carry one
    private static string Describe(Agent a) =>
        $"{a.Index}:{a.Character?.StringId ?? a.Name}/{a.Monster?.StringId}/{a.ActionSet.GetName()}" +
        (a.Banner != null ? "+banner" : "");

    private static string Actions(Agent a) =>
        $"c0={a.GetCurrentAction(0).GetName()}({a.GetCurrentActionType(0)}) " +
        $"c1={a.GetCurrentAction(1).GetName()}({a.GetCurrentActionType(1)},{a.GetCurrentActionStage(1)})";

    private static string SkeletonName(Agent a)
    {
        var skeleton = a.AgentVisuals?.GetSkeleton();
        return skeleton == null ? "null" : $"{skeleton.GetName()}({skeleton.GetBoneCount()} bones)";
    }

    private static string WeaponId(Agent a)
    {
        var index = a.GetPrimaryWieldedItemIndex();
        return index == TaleWorlds.Core.EquipmentIndex.None ? "none" : a.Equipment[index].Item?.StringId ?? "null";
    }
}
