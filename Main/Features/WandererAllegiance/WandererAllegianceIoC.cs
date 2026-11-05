using DryIoc;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Registers Wanderer Allegiance (#575). The dialog behavior is registered as a concrete singleton
/// and resolved by <see cref="WandererAllegianceModule"/>'s behavior decl at campaign start.
/// Depends on <c>IAlignmentService</c> (ExecutionIoC) and
/// <c>INamedCompanionConfigProvider</c> (NamedCompanionIoC); DryIoc resolves lazily, so registration
/// order against those does not matter.
/// </summary>
public static class WandererAllegianceIoC
{
    public static void RegisterWandererAllegianceFeature(IRegistrator container)
    {
        container.Register<IWandererAllegianceConfigProvider, WandererAllegianceConfigProvider>(Reuse.Singleton);
        container.Register<IWandererAllegianceSettingsProvider, WandererAllegianceSettingsProvider>(Reuse.Singleton);
        container.Register<IWandererAllegianceService, WandererAllegianceService>(Reuse.Singleton);
        container.Register<Hooks.WandererAllegianceDialogBehavior>(Reuse.Singleton);
    }
}
