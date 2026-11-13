using DryIoc;
using TAOM.Adapters;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Features.SpecialResources;

public static class SpecialResourcesIoC
{
    public static void RegisterSpecialResourcesFeature(IContainer container)
    {
        container.Register<ISpecialResourceSettingsProvider, SpecialResourceSettingsProvider>(Reuse.Singleton);
        container.Register<ISpecialResourceConfigProvider, SpecialResourceConfigProvider>(Reuse.Singleton);
        container.Register<ISpecialResourceStorageService, SpecialResourceStorageService>(Reuse.Singleton);
        container.Register<ISpecialResourceService, SpecialResourceService>(Reuse.Singleton);
        // The same singleton, seen through the narrow spend the armour acquisition armoury uses.
        container.RegisterDelegate<ISpecialResourceSpender>(r => (ISpecialResourceSpender)r.Resolve<ISpecialResourceService>(), Reuse.Singleton);
        // Transient: it holds the campaign's one-tick desertion grace and log-once latches, and SubModule
        // resolves it once per campaign, so every campaign starts with a fresh one.
        container.Register<SpecialResourceEarningService>(Reuse.Transient);
        container.Register<ISpecialResourceEventAdapter, SpecialResourceEventAdapter>(Reuse.Singleton);
        container.Register<IOnPartyUpgradeResourceCheck, PartyUpgradeResourceCheckHook>(Reuse.Singleton);
        container.Register<IOnRecruitmentResourceGate, RecruitmentResourceGateHook>(Reuse.Singleton);
    }
}
