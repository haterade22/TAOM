using TaleWorlds.InputSystem;

namespace TAOM.Features.CareerSystem.Abilities;

public class AbilityInputAdapter : IAbilityInputAdapter
{
    public bool IsActivationKeyPressed() => Input.IsKeyPressed(InputKey.V);
}
