using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

/// <summary>
/// The "no rider" branch of a warg whose rider left it alive in the middle of a rage: the rage branch is never entered again
/// (it needs a rider), so nothing else would give the rider his formation back. Does nothing unless a rage is open.
/// </summary>
public class RestoreDismountedRider : CleanIfEnemyDied
{
    protected override bool RageIsOver()
    {
        if (RageAttackStartTime.GetValue() == null) return false;
        // A rage that took the player's controller stored no formation, and FinishRageMode ends it, on a remount or at the warg's
        // death. A rage that took only his formation gives it back here like any other.
        Agent rider = ValidStoredRider(Rider.GetValue());
        return rider == null || !rider.IsPlayerControlled || AgentsFormation.GetValue() != null;
    }
}
