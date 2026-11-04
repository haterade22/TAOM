namespace TAOM.Features.Warg.BehaviorTreeElements;

/// <summary>
/// A rage belongs to the rider it started on. A new rider on the warg closes it, before the rage clears anything on him and before
/// a second start could overwrite the first rider's stored formation.
/// </summary>
public class CloseRageOnNewRider : CleanIfEnemyDied
{
    protected override bool ShouldClose() => RageAttackStartTime.GetValue() != null && Agent.GetValue().RiderAgent != Rider.GetValue();
}
