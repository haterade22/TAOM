using BehaviorTrees;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.BehaviorTreeElements;

/// <summary>
/// Passes once deployment is over (<see cref="CreatureBanditRules.MayFight"/>). Vanilla pauses the soldiers of every
/// formation for the deployment screen and hides the defenders when the player attacks, but a creature bandit is in no
/// formation and is no human, so neither reaches it; this gate holds its hunt and its strikes instead. Main thread.
/// </summary>
public class CreatureMayFightDecorator : BTReturnFalseDecorator
{
    public override bool Evaluate()
    {
        Mission mission = Mission.Current;
        return mission != null && CreatureBanditRules.MayFight(mission.AllowAiTicking, mission.IsTeleportingAgents);
    }
}
