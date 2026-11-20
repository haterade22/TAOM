namespace TAOM.Features.Stamina.Domain;

/// <summary>Which code strips an Exhausted soldier's input.</summary>
public enum StaminaLockPath
{
    /// <summary>The soldier is not locked.</summary>
    None = 0,

    /// <summary>A player controls the soldier: the strip at the end of the controller's input build does it.</summary>
    PlayerInput = 1,

    /// <summary>The AI controls the soldier: StaminaInputComponent strips in OnAIInputSet.</summary>
    AiInput = 2,
}

/// <summary>What the mission tick must do to a soldier when his lock path changes.</summary>
public readonly struct StaminaLockStep
{
    public StaminaLockStep(bool startAiStrip, bool stopAiStrip)
    {
        StartAiStrip = startAiStrip;
        StopAiStrip = stopAiStrip;
    }

    /// <summary>
    /// Claim the AI input callback for the soldier. The tick also keeps the input component on him, as his last
    /// component and switched on, on every look while he stays on the AI path.
    /// </summary>
    public bool StartAiStrip { get; }

    /// <summary>Switch the component's strip off and give the AI input callback claim back.</summary>
    public bool StopAiStrip { get; }
}

/// <summary>
/// The lock decisions of the Exhausted state. The path follows the soldier's controller now, not who he was at spawn:
/// Seat of Seeing, Long Defeat and the Fords control hand a hero to the AI and back during a battle.
/// </summary>
public static class StaminaLockRules
{
    /// <summary>
    /// A soldier who uses a game object (he works a siege engine, or raises a siege ladder) is never locked: the engine
    /// owns his input there. A soldier who climbs a ladder is most likely not in that state, and is locked as on the ground.
    /// </summary>
    public static StaminaLockPath PathFor(bool exhausted, bool aiControlled, bool usingGameObject)
    {
        if (!exhausted || usingGameObject)
            return StaminaLockPath.None;
        return aiControlled ? StaminaLockPath.AiInput : StaminaLockPath.PlayerInput;
    }

    public static StaminaLockStep Step(StaminaLockPath before, StaminaLockPath after) =>
        new StaminaLockStep(
            startAiStrip: after == StaminaLockPath.AiInput && before != StaminaLockPath.AiInput,
            stopAiStrip: before == StaminaLockPath.AiInput && after != StaminaLockPath.AiInput);
}
