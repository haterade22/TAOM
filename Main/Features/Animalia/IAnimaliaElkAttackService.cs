using TAOM.Features.ElephantLike;

namespace TAOM.Features.Animalia;

/// <summary>Marker for the Animalia elk's attack service, so IoC keys it apart from the moose's and the great elk's.</summary>
public interface IAnimaliaElkAttackService : IElephantLikeAttackService
{
}
