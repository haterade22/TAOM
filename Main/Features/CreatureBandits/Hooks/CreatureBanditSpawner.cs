using System;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// Engine glue that spawns a creature troop as a riderless creature agent (#692), for
/// <see cref="Patch93_CreatureBanditSpawn"/>. Verified in game, not unit-tested; the decisions live in
/// <see cref="CreatureBanditRules"/>.
///
/// <c>Mission.SpawnMonster</c> is the engine's own free-mount path (v1.5.3 <c>Mission.cs:4448-4465</c>): it builds
/// the agent from the item's Monster with no humanoid skin and no rider, then <c>BuildAgent(agent, null)</c>, which
/// leaves it with no team, no formation, no origin and no character. This class then adds what a troop needs, in an
/// order where no listener sees a half-wired creature:
/// <list type="bullet">
/// <item><b>Origin</b>, the troop's own <c>IAgentOriginBase</c>. SandBox's <c>BattleAgentLogic.OnAgentRemoved</c> gates
/// only on the origin, so the death reaches the party roster and the spawn logic's removed count, which is what lets
/// <c>IsSideDepleted</c> end the battle.</item>
/// <item><b>Character</b>, the troop. Without it the first kill NREs in <c>TroopUpgradeTracker.CheckUpgradedCount</c>
/// (<c>character.IsHero</c>, no null check), and kill XP needs both characters. The setter overwrites Health and both
/// health limits from the troop (<c>Agent.cs:1437-1439</c>), so they are set again, to the creature's tuned hit points
/// (<see cref="CreatureBanditTuning"/>; the Monster's own is shared with the ridden spider). From here
/// <c>CreatureBanditAgents.Is</c> recognises it, so the guards, panic and rout blocks apply.</item>
/// <item><b>No flight flags</b> (RunsAwayWhenHit, CanWander, CanGetScared, CanRear).</item>
/// <item><b>Team</b>: <c>SetTeam</c> raises <c>OnAgentTeamChanged</c> on every behavior (<c>Agent.cs:2211-2231</c>),
/// and a listener must find the creature complete.</item>
/// <item><b>Route A</b> last, only after the team step succeeded: Mountable cleared and the creature unlisted from the
/// loose mounts, so soldiers' native targeting sees an enemy (<see cref="CreatureRouteAUnmount"/>). It needs the native
/// weapon state, which <c>SpawnMonster</c> allocates only because <see cref="CreatureWeaponStateScope"/> is armed around
/// it (<see cref="Patch93_CreatureBanditWeaponState"/>).</item>
/// <item><b>No formation.</b> A riderless mount in a formation crashed twice in the June spider work
/// (<c>rca-spider-troop-2026-06-04.md:28, 61</c>).</item>
/// </list>
/// Each step is guarded on its own, so one failure does not skip the rest. <c>SpawnMonster</c> raises no
/// <c>OnAgentBuild</c> (only <c>SpawnAgent</c> does, <c>Mission.cs:4383, 4397</c>), so it is raised here, after the
/// wiring, as vanilla raises it after its own: that is what attaches the spider's tree and lets
/// <c>BattleAgentLogic</c> track the troop. The diagnostics breadcrumbs are durable INFO lines, the last one written
/// immediately before <c>SpawnMonster</c> (whose render preload is the June crash site).
/// </summary>
internal static class CreatureBanditSpawner
{
    /// <summary>The riderless creature, or null to fall back to the vanilla spawn (a husk on its mount). Never throws.</summary>
    // HandleProcessCorruptedStateExceptions lets the catch see a native AccessViolation from PreloadForRendering
    // (the June render crash class) and fall back instead of taking the process down, as the June spawner did.
    [HandleProcessCorruptedStateExceptions]
    [SecurityCritical]
    internal static Agent? TrySpawn(Mission mission, IAgentOriginBase origin, AgentBuildData buildData,
        EquipmentElement creature, EquipmentElement harness, bool isAlarmed)
    {
        float time = mission.CurrentTime;
        string troopId = origin.Troop?.StringId ?? "-";
        Interlocked.Increment(ref CreatureBanditDiag.SpawnsAttempted);
        CreatureDiagSpawn.MissionContextOnce(mission);
        var record = CreatureBanditDiag.Reserve(troopId, time);

        if (!TryGetSpawnFrame(mission, buildData, out Vec3 position, out Vec2 direction, out string frameSource))
            return Fallback(record, time, "no-spawn-frame", frameSource);

        CreatureDiagSpawn.Begin(record.Serial, time, origin, buildData, creature, harness, position, direction, frameSource, isAlarmed);
        Agent agent;
        bool weaponFlagAtCreate;
        CreatureWeaponStateScope.Arm();
        try
        {
            agent = mission.SpawnMonster(creature, harness, in position, in direction);
        }
        catch (Exception e)
        {
            // A throw after the native creation call (the render preload, the June crash site) leaves a native agent this
            // code never receives. It still falls back, so the troop is spawned and counted and its side can deplete;
            // the warning says the orphan exists. Never seen in play.
            return Fallback(record, time, "spawnmonster-threw", e.GetType().Name + ": " + e.Message,
                agentCreated: CreatureWeaponStateScope.ReachedCreation);
        }
        finally
        {
            weaponFlagAtCreate = CreatureWeaponStateScope.WeaponFlagAtCreate;
            CreatureWeaponStateScope.Disarm();
        }
        if (agent == null)
            return Fallback(record, time, "spawnmonster-null", "-");

        // From here the agent exists, so a failure must not fall back (that would spawn the troop twice).
        CreatureBanditDiag.Bind(agent, record);
        Interlocked.Increment(ref CreatureBanditDiag.SpawnsBuilt);
        CreatureDiagSpawn.Built(record.Serial, time, agent, weaponFlagAtCreate, CreatureRouteAUnmount.HasWeaponState(agent));

        var health = new CreatureDiagSpawn.HealthTrace();
        string steps = Wire(mission, agent, origin, buildData.AgentTeam, isAlarmed, record, time, ref health);
        string listeners = NotifyAgentBuilt(mission, agent);
        CreatureDiagSpawn.Wired(record.Serial, time, mission, agent, origin, steps, listeners, health, record.RouteA);
        return agent;
    }

    private static string Wire(Mission mission, Agent agent, IAgentOriginBase origin, Team team, bool isAlarmed,
        CreatureDiagRecord record, float time, ref CreatureDiagSpawn.HealthTrace health)
    {
        var steps = new StringBuilder();
        Step(steps, "origin", () => agent.Origin = origin);

        health.Before = agent.Health;
        health.LimitBefore = agent.HealthLimit;
        float troopHealth = float.NaN;
        float hitPoints = CreatureBanditTuning.Current.HitPoints;
        Step(steps, "character", () =>
        {
            agent.Character = origin.Troop;
            troopHealth = agent.Health;
            agent.BaseHealthLimit = hitPoints;
            agent.HealthLimit = hitPoints;
            agent.Health = hitPoints;
        });
        health.TroopValue = troopHealth;
        health.After = agent.Health;
        health.LimitAfter = agent.HealthLimit;

        health.FlagsBefore = agent.GetAgentFlags();
        Step(steps, "flags", () => agent.SetAgentFlags(CreatureBanditRules.WithoutFlightFlags(agent.GetAgentFlags())));
        health.FlagsAfter = agent.GetAgentFlags();

        bool teamSet = Step(steps, "team", () => agent.SetTeam(team, sync: false)) && agent.Team != null;
        Step(steps, "alarm", () =>
        {
            if (isAlarmed && agent.IsAIControlled)
                agent.SetWatchState(Agent.WatchState.Alarmed);
        });

        record.RouteA = "error";
        Step(steps, "routeA", () =>
        {
            var decision = CreatureRouteAUnmount.Apply(mission, agent, teamSet, () => CreatureDiagSpawn.Unmount(record.Serial, time, agent));
            record.RouteA = CreatureDiagSpawn.RouteAName(decision);
            if (decision == CreatureRouteA.On) record.UnmountTime = time;
        });
        CreatureBanditDiag.NoteRouteA(record.RouteA);
        return steps.ToString();
    }

    private static bool Step(StringBuilder steps, string name, Action action)
    {
        if (steps.Length > 0) steps.Append(',');
        try
        {
            action();
            steps.Append(name).Append(":ok");
            return true;
        }
        catch (Exception e)
        {
            steps.Append(name).Append(":").Append(e.GetType().Name);
            CreatureBanditLog.Logger?.LogError($"[CreatureBandits] Wiring step '{name}' threw {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// The frame vanilla would give the troop: its explicit position when it has one (console spawns), else its
    /// slot in the formation's deployment (the public helper vanilla's SpawnAgent uses). A zero or invalid facing
    /// becomes forward: the native SetInitialFrame normalises it, and a NaN frame crashes the render preload.
    /// </summary>
    private static bool TryGetSpawnFrame(Mission mission, AgentBuildData data, out Vec3 position, out Vec2 direction,
        out string source)
    {
        position = Vec3.Invalid;
        direction = Vec2.Forward;
        source = "none";
        if (data.AgentInitialPosition.HasValue)
        {
            position = data.AgentInitialPosition.Value;
            direction = data.AgentInitialDirection.GetValueOrDefault(Vec2.Forward);
            source = "initial-position";
        }
        else if (data.AgentFormation != null && data.AgentTeam != null)
        {
            mission.GetTroopSpawnFrameWithIndex(data, data.AgentFormationTroopSpawnIndex,
                data.AgentFormationTroopSpawnCount, out position, out direction);
            source = "formation-slot";
        }

        if (!direction.IsValid || !direction.IsNonZero())
        {
            direction = Vec2.Forward;
            source += "+forward";
        }
        return position.IsValid;
    }

    private static string NotifyAgentBuilt(Mission mission, Agent agent)
    {
        int count = 0;
        var failures = new StringBuilder();
        foreach (MissionBehavior behavior in mission.MissionBehaviors)
        {
            count++;
            try
            {
                behavior.OnAgentBuild(agent, null);
            }
            catch (Exception e)
            {
                if (failures.Length > 0) failures.Append('+');
                failures.Append(behavior.GetType().Name).Append(':').Append(e.GetType().Name);
                CreatureBanditLog.Logger?.LogError($"[CreatureBandits] OnAgentBuild({behavior.GetType().Name}) threw for a " +
                    $"creature: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
        return count + (failures.Length == 0 ? "/ok" : "/failed:" + failures);
    }

    private static Agent? Fallback(CreatureDiagRecord record, float time, string reason, string detail, bool agentCreated = false)
    {
        Interlocked.Increment(ref CreatureBanditDiag.SpawnFallbacks);
        record.Fate = "fallback:" + reason;
        record.EndTime = time;
        CreatureBanditLog.Logger?.LogWarning($"[CreatureBandits] Spawn of '{record.TroopId}' fell back to the vanilla husk " +
            $"on its mount: {reason} ({detail})" + (agentCreated ? "; the native agent had already been created" : ""));
        return null;
    }
}
