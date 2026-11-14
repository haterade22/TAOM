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

    /// <summary>
    /// The MCM switch for the chronicle's event chain (#765). Off stops new work: no event opens or
    /// resolves and the army focus is cleared. The saved event states are kept.
    /// </summary>
    bool ChronicleEnabled { get; }

    /// <summary>Whether chronicle events are announced to the player with a popup.</summary>
    bool ChronicleAnnouncements { get; }

    /// <summary>
    /// The army focus multiplier during a siege window: always finite and within 1 to 2.5 (a non-finite
    /// value falls back to the default 1.4).
    /// </summary>
    float ArmyFocusMultiplier { get; }

    /// <summary>Whether a story window offers its mission to the player (#1105 slice 4). Off: no offer; the map and the fate still decide.</summary>
    bool StoryOffersEnabled { get; }
}
