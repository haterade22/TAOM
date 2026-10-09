namespace TAOM.Features.WarChronicle;

public interface IWarChronicleSettingsProvider
{
    /// <summary>
    /// Scales every War of the Ring catch-up and event effect: 0 turns them off, 1 is the authored
    /// size, 2 doubles them. Always finite and within 0 to 2.
    /// </summary>
    float WarEffectStrength { get; }

    /// <summary>
    /// The MCM switch for the rally (catch-up effects for AI kingdoms that are losing the war). Off removes
    /// every rally effect at the next daily tick; the rally's data file has its own <c>enabled</c> flag.
    /// </summary>
    bool WarRallyEnabled { get; }
}
