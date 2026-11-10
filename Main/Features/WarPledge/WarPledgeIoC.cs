using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.WarPledge;

/// <summary>
/// Registers War Pledge (#1113) through <see cref="WarPledgeModule"/>. The store is one instance for the
/// service, the behavior, and the two hand-wired services that read it on every call:
/// <c>AlignmentService</c> and <c>DiplomacyService</c> take it as a constructor argument, and the container
/// resolves them only after every module has registered. The other dependencies (the alignment, diplomacy,
/// War of the Ring, momentum and War Chronicle services, the co-op providers, the loggers) are registered
/// earlier in <c>IoC.Configure</c>.
/// </summary>
public static class WarPledgeIoC
{
    public static void RegisterWarPledgeFeature(IRegistrator container)
    {
        container.Register<WarPledgeStore>(Reuse.Singleton);
        container.Register<IWarPledgeConfigProvider, WarPledgeConfigProvider>(Reuse.Singleton);
        container.Register<IWarPledgeSettings, WarPledgeSettingsProvider>(Reuse.Singleton);
        container.Register<IWarPledgeWorldAdapter, WarPledgeWorldAdapter>(Reuse.Singleton);
        container.Register<WarPledgeService>(Reuse.Singleton);
    }
}
