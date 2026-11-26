namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the wanderer allegiance rule. Keeps the pure
/// <see cref="WandererAllegianceService"/> free of MCM and JSON plumbing.
/// </summary>
public interface IWandererAllegianceSettingsProvider
{
    bool IsEnabled { get; }
    WandererAllegianceScope Scope { get; }

    /// <summary>New and swapped wanderers stand only in towns of their own side (<see cref="WandererTownSideService"/>). MCM only; on when MCM is absent.</summary>
    bool PlacesWanderersBySide { get; }
}
