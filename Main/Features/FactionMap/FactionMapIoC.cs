using DryIoc;
using TAOM.Adapters;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Features.FactionMap;

public static class FactionMapIoC
{
    public static void RegisterFactionMapFeature(IContainer container)
    {
        container.Register<ICultureObjectAdapter, CultureObjectAdapter>(Reuse.Singleton);

        container.Register<IFactionConfigProvider, FactionConfigProvider>(Reuse.Singleton);
        container.Register<IFactionRegistryService, FactionRegistryService>(Reuse.Singleton);
        container.Register<ICultureResolverService, CultureResolverService>(Reuse.Singleton);
        container.Register<ILandmarkService, LandmarkService>(Reuse.Singleton);
        container.Register<IFactionSelectionService, FactionSelectionService>(Reuse.Singleton);
        container.Register<IFactionHoverService, FactionHoverService>(Reuse.Singleton);
        container.Register<ICultureSettingService, CultureSettingService>(Reuse.Singleton);
        container.Register<ICultureStageProgressionService, CultureStageProgressionService>(Reuse.Singleton);

        container.Register<IOnCultureStageViewCreated, CultureStageViewCreatedHook>(Reuse.Singleton);
        container.Register<IOnCultureStageViewTick, CultureStageViewTickHook>(Reuse.Singleton);
        container.Register<IOnCultureStageViewFinalize, CultureStageViewFinalizeHook>(Reuse.Singleton);

        var createHook = container.Resolve<IOnCultureStageViewCreated>();
        CultureStageView_Constructor_Patch.Initialize(createHook);

        var tickHook = container.Resolve<IOnCultureStageViewTick>();
        CultureStageView_Tick_Patch.Initialize(tickHook);

        var finalizeHook = container.Resolve<IOnCultureStageViewFinalize>();
        CultureStageView_Finalize_Patch.Initialize(finalizeHook);
    }
}
