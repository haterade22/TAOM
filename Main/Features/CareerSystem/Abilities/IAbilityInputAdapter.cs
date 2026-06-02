namespace TAOM.Features.CareerSystem.Abilities;

// Boundary for the V-key polling -- keeps AbilityActivationController testable without
// reaching into static TaleWorlds.InputSystem.Input. Pattern mirrors TimeAcceleration's
// IMapInputAdapter.
public interface IAbilityInputAdapter
{
    bool IsActivationKeyPressed();
}
