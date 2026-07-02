using TAOM.Features.ElephantLike;

namespace TAOM.Features.Elephant;

/// <summary>
/// Marker interface for the war-elephant's attack service — the shared elephant-like decision surface
/// (<see cref="IElephantLikeAttackService"/>) registered and injected per-creature (IoC keys on this type;
/// <c>TaomAgentStatCalculateModel</c> takes it alongside the Mûmakil's + spider's). The full attack-model
/// contract (engage gate, deterministic cooldowns, per-kind damage bands) is documented on the base interface.
/// </summary>
public interface IElephantAttackService : IElephantLikeAttackService
{
}
