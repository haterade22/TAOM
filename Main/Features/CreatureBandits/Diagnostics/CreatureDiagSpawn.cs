using System;
using System.Linq;
using BehaviorTreeWrapper;
using TAOM.Features.Spider;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TAOM.Features.CreatureBandits.Diagnostics.CreatureDiagFormat;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// The spawn breadcrumbs (#692), written by <c>CreatureBanditSpawner</c> on the main thread. INFO lines are flushed
/// synchronously (FileLogger), so a native crash inside <c>SpawnMonster</c> leaves <c>spawn-begin</c> as the last
/// line on disk. Bounded by the number of creature spawns, so written outside the line budget. Every read is guarded:
/// a diagnostic never breaks a spawn. Temporary, strip after sign-off.
/// </summary>
internal static class CreatureDiagSpawn
{
    internal struct HealthTrace
    {
        internal float Before, TroopValue, After, LimitBefore, LimitAfter;
        internal AgentFlag FlagsBefore, FlagsAfter;
    }

    internal static bool MissionContextWritten;

    private static void Info(string line)
    {
        try { CreatureBanditDiag.Logger?.LogInfo(line); }
        catch { /* never break a spawn */ }
    }

    internal static void MissionContextOnce(Mission mission)
    {
        if (MissionContextWritten) return;
        MissionContextWritten = true;
        try
        {
            Info(Line("mission", mission.CurrentTime, 0, "scene", Name(mission.SceneName), "mode", mission.Mode.ToString(),
                "fieldBattle", B(mission.IsFieldBattle), "campaign", B(Campaign.Current != null),
                "playerSide", mission.PlayerTeam?.Side.ToString() ?? "-",
                "spiderBehavior", B(mission.GetMissionBehavior<SpiderMissionBehavior>() != null),
                "btLogic", B(mission.GetMissionBehavior<BehaviorTreeMissionLogic>() != null),
                "backstop", B(mission.GetMissionBehavior<CreatureBanditMissionBehavior>() != null),
                "diag", B(mission.GetMissionBehavior<CreatureBanditDiagnosticsBehavior>() != null),
                "agents", I(mission.Agents.Count),
                // The damage model the engine resolves: in Custom Battle it must be TaomCustomBattleDamageModel for the
                // creatures' damage-taken rules to apply (SubModule.RegisterCustomBattleModels adds it).
                "damageModel", MissionGameModels.Current?.AgentApplyDamageModel?.GetType().Name ?? "-",
                "tuning", Name(CreatureBanditTuning.Current.Describe())));
        }
        catch (Exception e)
        {
            Info(Line("mission", float.NaN, 0, "error", Name(e.GetType().Name + ": " + e.Message)));
        }
    }

    internal static void Begin(int serial, float time, IAgentOriginBase origin, AgentBuildData data, EquipmentElement creature,
        EquipmentElement harness, Vec3 position, Vec2 direction, string frameSource, bool isAlarmed)
    {
        try
        {
            Info(Line("spawn-begin", time, serial, "troop", origin.Troop?.StringId ?? "-", "item", creature.Item?.StringId ?? "-",
                "harness", harness.Item?.StringId ?? "-", "origin", origin.GetType().Name,
                "combatant", Name(origin.BattleCombatant?.Name?.ToString()), "side", data.AgentTeam?.Side.ToString() ?? "-",
                "formationClass", data.AgentFormation?.FormationIndex.ToString() ?? "-",
                "slot", I(data.AgentFormationTroopSpawnIndex) + "/" + I(data.AgentFormationTroopSpawnCount),
                "pos", F(position.x) + "," + F(position.y) + "," + F(position.z), "dir", F(direction.x) + "," + F(direction.y),
                "frame", frameSource, "alarmed", B(isAlarmed), "next", "SpawnMonster"));
        }
        catch (Exception e)
        {
            Info(Line("spawn-begin", time, serial, "error", Name(e.GetType().Name + ": " + e.Message)));
        }
    }

    internal static void Built(int serial, float time, Agent agent, bool weaponFlagAtCreate, bool weaponState)
    {
        try
        {
            Info(Line("spawn-built", time, serial, "idx", I(agent.Index), "monster", agent.Monster?.StringId ?? "-",
                "cwwAtCreate", B(weaponFlagAtCreate), "weaponState", weaponState ? "alloc" : "null",
                "hookSites", I(CreatureBanditDiag.CreationHookSites),
                "actionSet", agent.Monster?.ActionSetCode ?? "-", "isMount", B(agent.IsMount), "isHuman", B(agent.IsHuman),
                "controller", agent.Controller.ToString(), "hp", F(agent.Health), "hpMax", F(agent.HealthLimit),
                "scale", F(agent.AgentScale), "capsuleRadius", F(agent.Monster?.BodyCapsuleRadius ?? float.NaN),
                "flags", agent.GetAgentFlags().ToString().Replace(", ", "+"), "team", agent.Team?.Side.ToString() ?? "none",
                "character", agent.Character?.StringId ?? "none", "next", "wire"));
        }
        catch (Exception e)
        {
            Info(Line("spawn-built", time, serial, "error", Name(e.GetType().Name + ": " + e.Message)));
        }
    }

    /// <summary>
    /// Route A's durable breadcrumb, written immediately before Mountable is cleared. A crash within a frame or two of
    /// it points at a native path that treats the creature differently once it is not a mount.
    /// </summary>
    internal static void Unmount(int serial, float time, Agent agent)
    {
        try
        {
            Info(Line("routeA-unmount", time, serial, "flagsBefore", agent.GetAgentFlags().ToString().Replace(", ", "+"),
                "team", agent.Team?.Side.ToString() ?? "none", "pelvisBone", I(agent.Monster?.PelvisBoneIndex ?? -1),
                "next", "RemoveMountWithoutRider+SetAgentFlags"));
        }
        catch (Exception e)
        {
            Info(Line("routeA-unmount", time, serial, "error", Name(e.GetType().Name + ": " + e.Message)));
        }
    }

    internal static string RouteAName(CreatureRouteA decision) => decision switch
    {
        CreatureRouteA.On => "on",
        CreatureRouteA.SkippedNoWeaponState => "skipped:no-weapon-state",
        CreatureRouteA.SkippedWeaponFlagLive => "skipped:cww-live",
        CreatureRouteA.SkippedNoTeam => "skipped:no-team",
        _ => "skipped:no-pelvis-bone",
    };

    internal static void Wired(int serial, float time, Mission mission, Agent agent, IAgentOriginBase origin, string steps,
        string listeners, HealthTrace health, string routeA)
    {
        try
        {
            var main = mission.MainAgent;
            Info(Line("spawn-wired", time, serial, "routeA", routeA, "tuning", Name(CreatureBanditTuning.Current.Describe()),
                "cwwLive", B((agent.GetAgentFlags() & AgentFlag.CanWieldWeapon) != 0), "isMount", B(agent.IsMount),
                "steps", steps, "listeners", listeners,
                "team", agent.Team?.Side.ToString() ?? "none", "character", agent.Character?.StringId ?? "none",
                "originBound", B(agent.Origin == origin), "fingerprint", B(Hooks.CreatureBanditAgents.Is(agent)),
                "hpBefore", F(health.Before), "hpTroop", F(health.TroopValue), "hpAfter", F(health.After),
                "hpMaxBefore", F(health.LimitBefore), "hpMaxAfter", F(health.LimitAfter),
                "flagsBefore", health.FlagsBefore.ToString().Replace(", ", "+"),
                "flagsAfter", health.FlagsAfter.ToString().Replace(", ", "+"),
                "watch", agent.CurrentWatchState.ToString(), "aiControlled", B(agent.IsAIControlled),
                "commonAI", B(agent.CommonAIComponent != null), "morale", F(agent.CommonAIComponent?.Morale ?? float.NaN),
                "humanAI", B(agent.HumanAIComponent != null), "formation", agent.Formation == null ? "none" : "SET",
                "missionEquipment", B(agent.Equipment != null), "tree", B(agent.GetBehaviorTree() != null),
                "proximityMap", B(mission.IsAgentInProximityMap(agent)),
                "looseMountList", B(mission.MountsWithoutRiders.Any(pair => pair.Key == agent)),
                "enemyOfPlayer", main == null ? "-" : B(agent.IsEnemyOf(main)),
                "playerEnemyOf", main == null ? "-" : B(main.IsEnemyOf(agent)),
                "fieldBattle", B(mission.IsFieldBattle), "gateReach", F(SpiderConfig.BiteAttackRange),
                "gateHalfCone", F(SpiderConfig.BiteConeAngleDegrees / 2f), "strikeRadius", F(SpiderConfig.StrikeRadius)));
        }
        catch (Exception e)
        {
            Info(Line("spawn-wired", time, serial, "error", Name(e.GetType().Name + ": " + e.Message)));
        }
    }
}
