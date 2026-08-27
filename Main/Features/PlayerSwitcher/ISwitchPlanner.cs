using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Turns a chosen row plus the active policy into the handover contract. Pure function.
/// </summary>
public interface ISwitchPlanner
{
    SwitchPlan Plan(HeroPickRow row, PlayerSwitchPolicy policy, string careerId);
}
