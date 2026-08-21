namespace TAOM.Features.HeroRace;

/// <summary>
/// MCM boundary for the HeroRace feature. Trivial bridge by design: validation lives in
/// <see cref="EyeHeightAdjustment"/> so it can be unit tested without spinning up MCM, matching the
/// SettlementNameplateFade pattern.
/// </summary>
public interface IHeroRaceSettingsProvider
{
    /// <summary>Master toggle for the dwarf eye-height override. Off restores vanilla eye height.</summary>
    bool EyeHeightEnabled { get; }

    /// <summary>
    /// Offset in metres applied to a dwarf eye height relative to the human baseline. Raw slider
    /// value; callers sanitise via <see cref="EyeHeightAdjustment.ClampAdjuster"/>.
    /// </summary>
    float DwarfEyeHeightAdjuster { get; }
}
