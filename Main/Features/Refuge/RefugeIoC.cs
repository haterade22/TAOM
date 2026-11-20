using System.Runtime.CompilerServices;
using DryIoc;
using TAOM.Features.FieldCamp;
using TAOM.Features.Refuge.Hooks;

namespace TAOM.Features.Refuge;

public static class RefugeIoC
{
    public static void RegisterRefugeFeature(IContainer container)
    {
        container.Register<IRefugeSettingsProvider, RefugeSettingsProvider>(Reuse.Singleton);
        container.Register<IWardenService, WardenService>(Reuse.Singleton);
        container.Register<IRefugeVisualService, NullRefugeVisualService>(Reuse.Singleton);

        container.Register<IRefugeService, RefugeService>(Reuse.Singleton);
        container.RegisterDelegate<IRefugeBook>(r => (IRefugeBook)r.Resolve<IRefugeService>(), Reuse.Singleton);
        container.Register<IRefugeDefenseService, RefugeDefenseService>(Reuse.Singleton);

        container.Register<ICampOverlayContributor, RefugeCampContributor>(Reuse.Singleton);
    }

    internal static void InitializePatchStatics(IContainer container)
    {
        var refugeService = container.Resolve<IRefugeService>();
        // ADR-013: the clan-screen patch type initializer names ClanPartiesVM, which a dedicated server cannot load at
        // boot. The patch is [ClientOnly] and never applies there, so its statics stay unset.
        if (!TAOM.Composition.ClientComposition.IsDedicatedServer())
            InitializeClanScreenPatch(refugeService, container.Resolve<IRefugeBook>());
        RefugeEncounterPatch.Initialize(
            refugeService,
            container.Resolve<TAOM.Features.Enlistment.IEnlistmentStateQuery>(),
            container.Resolve<TAOM.Adapters.IGameMenuAdapter>(),
            container.Resolve<TAOM.Features.CoopInterop.ICoopSessionProvider>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeClanScreenPatch(IRefugeService refuges, IRefugeBook book)
    {
        RefugeClanScreenPatch.Initialize(refuges);
        RefugeClanRowRefreshPatch.Initialize(book);
    }
}
