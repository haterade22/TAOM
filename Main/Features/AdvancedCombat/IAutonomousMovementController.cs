using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// The rage-mode player controller as the Warg code drives it (ADR-013). The controller is a MissionView, so it
/// lives in TAOM.Client. Core finds it with Mission.GetMissionBehavior of this interface, which returns the first
/// behavior that implements it: the engine's [DefaultView] instance, the one the class lookup returned before
/// (docs/features/warg-combat.md). Null in a mission without it: a dedicated server, or TAOM.Client missing.
/// An engine-boundary interface: its callers are BT tasks and a MissionLogic that already hold the Agent.
/// </summary>
public interface IAutonomousMovementController : IMissionBehavior
{
    Agent? TargetAgent { get; set; }
    Vec3 CustomLookDir { get; set; }
    void Disable();
    /// <summary>
    /// True: vanilla's MissionMainAgentController off, then this on (rage). False: this controller off, and vanilla's back on unless
    /// the Seat of Seeing is up, whose leave turns it on.
    /// </summary>
    void SwitchMainAgentController(bool switchToSpecialController);
    /// <summary>
    /// True from the swap into the rage until the swap back: this controller, not vanilla's, holds the hero. Set only by
    /// <see cref="SwitchMainAgentController"/>. Not the controller's activation flag, which defaults to true and turns off only at
    /// the first mission tick. The Seat of Seeing stays out of a rage (docs/features/seat-of-seeing/build-spec.md 5.5).
    /// </summary>
    bool HoldsHero { get; }
}
