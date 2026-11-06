using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg.BehaviorTreeElements;

/// <summary>
/// The "no rider" branch of a warg whose rider left it alive in the middle of a rage: the rage branch is never entered again
/// (it needs a rider), so nothing else would give the rider his formation back. Does nothing unless a rage is open.
/// </summary>
public class RestoreDismountedRider : CleanIfEnemyDied
{
    protected override bool ShouldClose()
    {
        if (RageAttackStartTime.GetValue() == null) return false;
        // A player's rage with no stored formation may hold his controller, and FinishRageMode ends that, on a remount or at the
        // warg's death. One that took his formation gives it back here like any other.
        Agent rider = ValidStoredRider(Rider.GetValue());
        return rider == null || !rider.IsPlayerControlled || AgentsFormation.GetValue() != null;
    }
}
