namespace TAOM.Features.Warg.BehaviorTreeElements;

/// <summary>
/// The "no enemy close" branch of a mounted warg. That branch wins every pass while no enemy is within 60 m, so "enter rage mode",
/// which holds the other close, never runs, and a rider whose rage is open would keep no formation until an enemy returned.
/// </summary>
public class CloseOpenRage : CleanIfEnemyDied
{
    protected override bool RageIsOver() => RageAttackStartTime.GetValue() != null;
}
