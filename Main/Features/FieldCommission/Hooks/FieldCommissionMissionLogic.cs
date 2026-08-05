using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>
/// Thin mission boundary (ADR-002): converts an agent kill into a primitive troop StringId and
/// hands it to <see cref="IFieldCommissionMeritService"/>. Registered unconditionally into
/// <c>SubModule.OnMissionBehaviorInitialize</c> — self-filters here (bug fix (f)): resolves its
/// service via IoC in the ctor (matching <c>BannerBearerAssignmentMissionLogic</c>'s convention,
/// never <c>MissionBehavior</c> per the BehaviorTreeMissionLogic regression rule) and no-ops on
/// every non-campaign mission or co-op client.
/// </summary>
public sealed class FieldCommissionMissionLogic : MissionLogic
{
    private IFieldCommissionMeritService _merit;
    private ICoopSessionProvider _coopSession;
    private bool _active;

    public override void AfterStart()
    {
        base.AfterStart();

        _merit = IoC.Resolve<IFieldCommissionMeritService>();
        _coopSession = IoC.Resolve<ICoopSessionProvider>();
        _active = Campaign.Current != null;
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

        if (!_active || !_coopSession.IsAuthority
            || affectedAgent == null || affectorAgent == null
            || affectedAgent.IsMount || affectorAgent.IsMount
            || (agentState != AgentState.Killed && agentState != AgentState.Unconscious)
            || !affectedAgent.IsEnemyOf(affectorAgent)
            || affectorAgent.Character == null
            || affectorAgent.Team == null
            || Mission.Current?.PlayerTeam == null
            || affectorAgent.Team != Mission.Current.PlayerTeam)
        {
            return;
        }

        // Both PartyAgentOrigin (individual battles) and PartyGroupAgentOrigin (campaign battles)
        // expose the real party via BattleCombatant — checking against MainParty (not merely "is
        // a PartyAgentOrigin") is what excludes allied kills from counting toward our merit.
        if (!(affectorAgent.Origin?.BattleCombatant is PartyBase party) || party != PartyBase.MainParty)
            return;

        _merit.RegisterKill(affectorAgent.Character.StringId);
    }
}
