using TAOM.Features.ElephantLike;

namespace TAOM.Features.Mumakil;

/// <summary>
/// Marker interface for the Mûmakil's attack service — the shared elephant-like decision surface
/// (<see cref="IElephantLikeAttackService"/>) registered and injected per-creature (IoC keys on this type;
/// <c>TaomAgentStatCalculateModel</c> takes it alongside the elephant's + spider's). The Mûmakil is a scaled-up
/// elephant and shares the attack model 1-for-1; the full contract is documented on the base interface.
/// </summary>
public interface IMumakilAttackService : IElephantLikeAttackService
{
}
