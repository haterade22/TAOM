using TAOM.Features.ElephantLike;

namespace TAOM.Features.WarRam;

/// <summary>
/// Marker interface for the war ram's attack service, the shared elephant-like decision surface
/// (<see cref="IElephantLikeAttackService"/>) registered and injected per-creature (IoC keys on this
/// type). Unlike the war elephant and Mumakil, the war ram fires only the kick (Trample-kind) attack;
/// see <see cref="WarRamBehaviorTree"/> for why no side-attack branch is wired. The full contract is
/// documented on the base interface.
/// </summary>
public interface IWarRamAttackService : IElephantLikeAttackService
{
}
