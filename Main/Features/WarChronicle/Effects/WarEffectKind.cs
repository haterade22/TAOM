namespace TAOM.Features.WarChronicle.Effects;

/// <summary>
/// What a timed War of the Ring effect scales. Saved by NAME (see WarChronicleSaveCodec), so a later
/// release can append kinds such as garrison recruitment without breaking a save; never rename one.
/// The values are dense from zero because <see cref="WarEffectService"/> bakes one array slot per kind.
/// </summary>
public enum WarEffectKind
{
    /// <summary>The daily volunteer probability of a settlement owned by the kingdom.</summary>
    VolunteerRate = 0,

    /// <summary>The extra daily escape roll of the kingdom's captured lords.</summary>
    PrisonerEscape = 1,
}
