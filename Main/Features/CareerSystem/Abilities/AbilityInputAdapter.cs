using TaleWorlds.InputSystem;

namespace TAOM.Features.CareerSystem.Abilities;

public class AbilityInputAdapter : IAbilityInputAdapter
{
    // Single source for both the poll and the key-chip label (Issue #382).
    private const InputKey ActivationKey = InputKey.V;

    public bool IsActivationKeyPressed() => Input.IsKeyPressed(ActivationKey);

    public string ActivationKeyName => ActivationKey.ToString();
}
