namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// The ranged part of a hero kit (#1361). The provider has made every value finite and in range; a bad value
/// reads as 0, false or "" (docs/features/hero-abilities.md).
/// </summary>
public sealed class HeroRangedConfig
{
    // Writes 0 to the AI aim-error properties for the whole mission.
    public bool ZeroAimError { get; set; }

    // His arrows may pass a shield that blocks them (the flag, and a damage floor above the engine's threshold).
    public bool PierceShields { get; set; }

    // Chance 0 to 1 that an unblocked arrow hit to the head kills a victim the kill filter accepts.
    public float HeadshotKillChance { get; set; }

    // The same for any other body part.
    public float BodyKillChance { get; set; }

    // Item id of the extra arrow ("" = none), a lowercase id of a-z, 0-9 and _.
    public string ExtraArrowItem { get; set; } = "";

    // Metres, at most 20: an extra arrow goes to an enemy this near the primary target.
    public float ExtraArrowSpreadMeters { get; set; }

    // Metres, at most 150: the farthest target an extra arrow reaches.
    public float ExtraArrowRangeMeters { get; set; }

    // Metres, at most 10: an enemy soldier this near is a melee threat, and he steps away (#1415). 0 = never.
    public float KeepDistanceMeters { get; set; }

    // Metres, at most 12: how far one step takes him; about what a run covers in the 2.5 s hold. 0 = never.
    public float StepBackMeters { get; set; }
}
