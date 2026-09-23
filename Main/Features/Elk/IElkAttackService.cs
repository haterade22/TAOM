using TAOM.Features.ElephantLike;

namespace TAOM.Features.Elk;

/// <summary>
/// Marker for the great elk's attack service: the shared elephant-like decision surface
/// (<see cref="IElephantLikeAttackService"/>), registered and injected per creature, so IoC keys on this type. The elk
/// fires only the antler charge; see <see cref="ElkBehaviorTree"/>.
/// </summary>
public interface IElkAttackService : IElephantLikeAttackService
{
}
