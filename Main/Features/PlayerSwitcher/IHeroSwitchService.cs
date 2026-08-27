using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Executes a <see cref="SwitchPlan"/> as an ordered sequence of adapter calls. Never throws out:
/// a failure is logged and reported, and the player continues as the character they created.
/// </summary>
public interface IHeroSwitchService
{
    SwitchOutcome Execute(SwitchPlan plan);
}
