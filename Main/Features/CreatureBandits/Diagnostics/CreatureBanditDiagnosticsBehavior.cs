using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Diagnostics;

/// <summary>
/// Mission entry point of the Creature Bandits diagnostics (#692). Every callback's first statement is a serial
/// lookup (a small concurrent map keyed by agent reference), so battles without creatures pay one lookup per event;
/// callbacks only capture and queue (<see cref="CreatureDiagCapture"/>), and the main-thread tick logs
/// (<see cref="CreatureBanditDiagTicker"/>). Per-mission state resets in OnCreated and after the summary. A throw in
/// the tick disables the diagnostics for the rest of the mission with one ERROR (the MissionPerf heartbeat's rule);
/// the queue then keeps only removals, which the summary applies without writing lines.
/// Temporary, strip after sign-off with the rest of the Diagnostics folder and its module line.
/// </summary>
public class CreatureBanditDiagnosticsBehavior : MissionLogic
{
    private readonly CreatureBanditDiagTicker _ticker = new();
    private bool _disabled;

    public override void OnCreated()
    {
        base.OnCreated();
        CreatureBanditDiag.ResetForMission();
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled) return;
        try
        {
            _ticker.Tick(Mission);
        }
        catch (Exception e)
        {
            _disabled = true;
            CreatureBanditDiag.QueueDisabled = true;
            CreatureBanditDiag.Logger?.LogError($"[CreatureBandits][diag] tick failed, diagnostics off for this mission: " +
                $"{e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow,
        in AttackCollisionData attackCollisionData)
    {
        int victim = CreatureBanditDiag.SerialOf(affectedAgent);
        int attacker = CreatureBanditDiag.SerialOf(affectorAgent);
        if (victim == 0 && attacker == 0) return;
        CreatureDiagCapture.Hit(victim, attacker, affectedAgent, affectorAgent, blow, attackCollisionData);
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        int victim = CreatureBanditDiag.SerialOf(affectedAgent);
        int killer = CreatureBanditDiag.SerialOf(affectorAgent);
        if (victim == 0 && killer == 0) return;
        CreatureDiagCapture.Removed(victim, killer, affectedAgent, affectorAgent, agentState, blow);
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        int serial = CreatureBanditDiag.SerialOf(affectedAgent);
        if (serial == 0) return;
        CreatureDiagCapture.Simple(CreatureDiagEventKind.Deleted, serial, "OnAgentDeleted");
        CreatureBanditDiag.Forget(affectedAgent);
    }

    public override void OnAgentPanicked(Agent affectedAgent)
    {
        int serial = CreatureBanditDiag.SerialOf(affectedAgent);
        if (serial > 0) CreatureDiagCapture.Simple(CreatureDiagEventKind.Panicked, serial, "OnAgentPanicked");
    }

    public override void OnAgentFleeing(Agent affectedAgent)
    {
        int serial = CreatureBanditDiag.SerialOf(affectedAgent);
        if (serial > 0) CreatureDiagCapture.Simple(CreatureDiagEventKind.Fled, serial, "OnAgentFleeing");
    }

    public override void OnAgentMount(Agent agent)
    {
        // Keyed on the mount's reference, which still resolves once RiderAgent is set (the fingerprint no longer does).
        int serial = CreatureBanditDiag.SerialOf(agent.MountAgent);
        if (serial > 0) CreatureDiagCapture.Mounted(serial, agent);
    }

    public override void OnAgentAlarmedStateChanged(Agent agent, Agent.AIStateFlag flag)
    {
        int serial = CreatureBanditDiag.SerialOf(agent);
        if (serial > 0) CreatureDiagCapture.Simple(CreatureDiagEventKind.Alarmed, serial, "OnAgentAlarmedStateChanged", flag.ToString());
    }

    public override void OnMissionResultReady(MissionResult missionResult)
    {
        base.OnMissionResultReady(missionResult);
        _ticker.Result = missionResult == null ? "none"
            : missionResult.PlayerVictory ? "playerVictory" : missionResult.PlayerDefeated ? "playerDefeated" : "other";
        // Also here, not only at teardown: a game closed at the victory screen never tears the mission down.
        WriteSummary("result");
    }

    public override void OnRemoveBehavior()
    {
        WriteSummary("end");
        CreatureBanditDiag.ResetForMission();
        base.OnRemoveBehavior();
    }

    private void WriteSummary(string phase)
    {
        try
        {
            _ticker.WriteSummary(Mission, tickerFailed: _disabled, phase);
        }
        catch (Exception e)
        {
            CreatureBanditDiag.Logger?.LogError($"[CreatureBandits][diag] summary ({phase}) failed: {e.GetType().Name}: {e.Message}");
        }
    }
}
