namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the wanderer allegiance rule. Keeps the pure
/// <see cref="WandererAllegianceService"/> free of MCM and JSON plumbing.
/// </summary>
public interface IWandererAllegianceSettingsProvider
{
    bool IsEnabled { get; }
    WandererAllegianceScope Scope { get; }
}
