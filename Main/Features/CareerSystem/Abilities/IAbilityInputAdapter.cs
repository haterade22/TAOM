namespace TAOM.Features.CareerSystem.Abilities;

// Boundary for the V-key polling -- keeps AbilityActivationController testable without
// reaching into static TaleWorlds.InputSystem.Input. Pattern mirrors TimeAcceleration's
// IMapInputAdapter.
public interface IAbilityInputAdapter
{
    bool IsActivationKeyPressed();

    // Issue #382 — display name of the activation key for the energy bar's key chip.
    // Single-sourced with the polled key so a future rebind changes both together.
    string ActivationKeyName { get; }
}
