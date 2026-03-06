using DryIoc;
using TAOM.Adapters;
using TAOM.Features.BannerInjection.Hooks;

namespace TAOM.Features.BannerInjection;

public static class BannerInjectionIoC
{
    public static void RegisterBannerInjectionFeature(IContainer container)
    {
        container.Register<IKingdomBannerAdapter, KingdomBannerAdapter>(Reuse.Singleton);
        container.Register<IClanBannerAdapter, ClanBannerAdapter>(Reuse.Singleton);
        container.Register<IBannerExclusionService, BannerExclusionService>(Reuse.Singleton);
        container.Register<IBannerConfigProvider, BannerConfigProvider>(Reuse.Singleton);
        container.Register<IBannerInjectionService, BannerInjectionService>(Reuse.Singleton);

        container.Register<IOnBannerEditorDone, BannerEditorDoneHook>(Reuse.Singleton);
        var hook = container.Resolve<IOnBannerEditorDone>();
        GauntletBannerEditorScreen_OnDone_Patch.Initialize(hook);
    }
}
