using TAOM.Features.CultureDoctrine.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Models;

/// <summary>
/// Custom Battle: the culture's aggression profile as a post-pass on
/// <c>CustomBattleAgentStatCalculateModel.UpdateAgentStats</c>. The campaign's slot is
/// <c>TaomAgentStatCalculateModel</c> (CareerSystem), which carries the same post-pass beside
/// the career and creature rules; this one exists so the Custom Battle A/B measures the melee
/// tier too.
/// </summary>
public class TaomCustomBattleAgentStatCalculateModel : CustomBattleAgentStatCalculateModel
{
    private readonly ICultureAggressionService _aggression;

    public TaomCustomBattleAgentStatCalculateModel(ICultureAggressionService aggression)
    {
        _aggression = aggression;
    }

    public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
    {
        base.UpdateAgentStats(agent, agentDrivenProperties);
        AgentAggressionApplier.Apply(agentDrivenProperties, _aggression.Profile(AgentAggressionApplier.CultureOf(agent)));
    }
}
