namespace TAOM.Features.CareerSystem.Abilities;

// Per-frame ability activation state machine extracted from CareerPerkMissionBehavior.OnMissionTick
// (Issue #102). Owns the V-key polling, ready-state one-shot flag, and charging-message throttle.
// Returns an AbilityActivationResult flags struct that the host MissionBehavior translates into
// InformationManager calls and ExecuteAbilityEffect dispatch -- the controller never touches
// TaleWorlds statics.
public interface IAbilityActivationController
{
    // isControllingCareerHero (Issue #377): false while the player controls another agent
    // (co-op, or a soldier after being wounded). The cooldown still ticks, but V-presses are
    // ignored (no wasted activation, no charging toast) and the ready toast is deferred
    // until control returns.
    AbilityActivationResult Tick(float dt, string heroStringId, bool hasCareer, bool isControllingCareerHero);
    void Reset();
}

// Issue #102 deep-review MED — multi-outcome per tick. Pre-refactor the legacy mission behavior
// could emit BOTH the green "ability ready" toast AND the yellow "activated" toast in the same
// frame (e.g., the ability becomes ready and the player V-presses on that exact same frame); a
// single-outcome enum silently dropped the ready toast. The flags struct preserves the legacy
// UX behavior.
public struct AbilityActivationResult
{
    // Cooldown just finished THIS frame; emit the green "ability ready" toast. Re-arms after each
    // activation. Set independently of Activated -- both can fire on the same frame.
    public bool JustBecameReady;

    // V was pressed THIS frame while the ability was ready; ExecuteAbilityEffect should run AND
    // the yellow "{ability} activated!" toast should display.
    public bool Activated;

    // V was pressed while on cooldown and the 2-second charging-message throttle window allowed
    // a new message; emit the gray "still charging - Ns remaining" toast. Mutually exclusive with
    // Activated by construction (a V-press is either Activated or Charging, never both).
    public bool Charging;
}
