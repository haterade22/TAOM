namespace TAOM.Features.Enlistment;

/// <summary>
/// The enlisted verdict for the Order of Battle deployment screen (#576). Consumed by
/// <c>TaomBattleInitializationModel</c> in the rule-4 shape <c>_service.X() ?? base.X()</c>.
/// </summary>
public interface IEnlistmentDeploymentService
{
    /// <summary>
    /// <c>false</c>: the player is an enlisted soldier in someone else's battle, keep the screen
    /// shut. <c>null</c>: not enlisted service (including the player's own battles), vanilla
    /// decides. Never <c>true</c>: this service only ever closes the screen.
    /// </summary>
    bool? CanPlayerSideDeployWithOrderOfBattle();
}
