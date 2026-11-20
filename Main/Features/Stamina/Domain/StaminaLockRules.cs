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

/// <summary>What one look at a soldier adds to the mission's lock counters (the taom.stamina.lock check).</summary>
public readonly struct StaminaTally
{
    public StaminaTally(bool entered, bool recovered, bool startClock, bool entryCounted)
    {
        Entered = entered;
        Recovered = recovered;
        StartClock = startClock;
        EntryCounted = entryCounted;
    }

    /// <summary>The rules made the soldier Exhausted: count an exhaustion.</summary>
    public bool Entered { get; }

    /// <summary>The rules brought back a soldier whose exhaustion was counted: count a recovery.</summary>
    public bool Recovered { get; }

    /// <summary>The time a recovery may take starts at this look.</summary>
    public bool StartClock { get; }

    /// <summary>The soldier is Exhausted and that exhaustion was counted. The cell keeps it for the next look.</summary>
    public bool EntryCounted { get; }
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

    /// <summary>
    /// Only a band the rules decided counts. A band the console forced, or the Fresh of an exempt soldier, is not an
    /// exhaustion or a recovery: counted, a force alone would pass the lock check, and the soldiers it left below the
    /// recovery edge would fail it as stuck the moment they went back to the rules. A soldier the rules take over
    /// while he is Exhausted starts his recovery time then.
    /// </summary>
    public static StaminaTally Tally(StaminaBand before, StaminaBand after, bool byRules, bool wasByRules, bool entryCounted)
    {
        if (after != StaminaBand.Exhausted)
            return new StaminaTally(false, before == StaminaBand.Exhausted && byRules && entryCounted, false, false);
        if (before != StaminaBand.Exhausted)
            return new StaminaTally(byRules, false, true, byRules);
        return new StaminaTally(false, false, byRules && !wasByRules, entryCounted);
    }

    public static StaminaLockStep Step(StaminaLockPath before, StaminaLockPath after) =>
        new StaminaLockStep(
            startAiStrip: after == StaminaLockPath.AiInput && before != StaminaLockPath.AiInput,
            stopAiStrip: before == StaminaLockPath.AiInput && after != StaminaLockPath.AiInput);
}
