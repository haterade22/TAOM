namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// The answer to "will the wanderer the player is talking to take the player's coin?" Two refusal
/// values rather than one bool because the dialogue speaks a different line for each direction.
/// </summary>
public enum WandererHireVerdict
{
    /// <summary>Vanilla hiring proceeds untouched.</summary>
    Allowed,

    /// <summary>A Free-culture wanderer refusing an Evil-aligned player.</summary>
    RefusedByFreeWanderer,

    /// <summary>An Evil-culture wanderer refusing a Free-aligned player.</summary>
    RefusedByEvilWanderer,
}
