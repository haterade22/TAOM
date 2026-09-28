using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Counts the enemies the player's hero strikes down in a campaign battle for the lord's gear ladder (#693),
/// adding each to <see cref="HeroKillTally"/> as it happens. Added to every mission; it counts only in a
/// mission the player's encounter backs with a map event (a field battle, a siege, a sally out, a hideout, a
/// raid, a naval battle, a town's street fight), never in an arena, a tournament, a plain settlement scene or a
/// custom battle. What counts is <see cref="HeroKillTally.Counts"/>'s.
/// </summary>
public sealed class HeroKillCounterMissionLogic : MissionLogic
{
    private readonly HeroKillTally _tally;
    private readonly bool _countsKnockouts;
    private readonly bool _eligible;
    private Agent? _hero;

    public HeroKillCounterMissionLogic(HeroKillTally tally, bool countsKnockouts, bool eligible)
    {
        _tally = tally;
        _countsKnockouts = countsKnockouts;
        _eligible = eligible;
    }

    /// <summary>
    /// A mission the player's encounter backs with a map event: it exists before the mission opens, and none
    /// exists for an arena, a tournament, a settlement scene or a custom battle (no campaign). The encounter's
    /// IsJoinedBattle is set in the same branch as its map event, so the map event alone decides.
    /// </summary>
    public static bool IsCampaignBattle()
    {
        try
        {
            return Campaign.Current != null && PlayerEncounter.Battle != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override void OnMissionTick(float dt)
    {
        // Cached once: Mission.MainAgent is nulled the moment the hero falls (Mission.OnAgentRemoved, v1.5.3), and a
        // soldier the player then takes control of becomes MainAgent; his kills are not the hero's.
        if (_eligible && _hero == null)
            _hero = Mission.MainAgent;
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        // Can arrive off the main thread (#634). The managed identity checks come first: a removal the hero did
        // not cause reads no native memory (a mount's rider is a managed field), and the native enmity check runs
        // only for the hero's own kills, never on a cached hero whose body the engine has since deleted.
        var hero = _hero;
        if (hero == null || affectedAgent == null || affectorAgent == null
            || (affectorAgent != hero && !(affectorAgent.RiderAgent == hero && affectorAgent.IsMount)))
            return;
        if (HeroKillTally.Counts(agentState == AgentState.Killed, agentState == AgentState.Unconscious, _countsKnockouts)
            && affectedAgent.IsHuman && affectedAgent.IsEnemyOf(hero))
            _tally.Add(1);
    }
}
