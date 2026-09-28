using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Registers Armour Acquisition (docs/features/armour-acquisition.md) through its feature module. Every
/// registration is a singleton: the gate service is rebuilt in place at each game init, and the campaign
/// state is reset by the campaign behavior whenever a session starts without loading it. DryIoc resolves
/// lazily, so the order against CultureMarketplace (the stock gate's consumer) and SpecialResources (the
/// spender) does not matter.
/// </summary>
public static class ArmourAcquisitionIoC
{
    public static void RegisterArmourAcquisitionFeature(IRegistrator container)
    {
        container.Register<IArmourAcquisitionConfigProvider, ArmourAcquisitionConfigProvider>(Reuse.Singleton);
        container.Register<IArmourClassTableProvider, ArmourClassTableProvider>(Reuse.Singleton);
        container.Register<IArmourAcquisitionSettingsProvider, ArmourAcquisitionSettingsProvider>(Reuse.Singleton);
        container.Register<IArmourItemCatalogAdapter, ArmourItemCatalogAdapter>(Reuse.Singleton);
        container.Register<IArmouryTownAdapter, ArmouryTownAdapter>(Reuse.Singleton);
        container.Register<IArmourGateService, ArmourGateService>(Reuse.Singleton);
        container.Register<ArmourAcquisitionState>(Reuse.Singleton);
        container.Register<VisitingArmourerService>(Reuse.Singleton);
        container.Register<ArmouryLevelService>(Reuse.Singleton);
        container.Register<ArmourStockSweepService>(Reuse.Singleton);
        container.Register<IMarketplaceStockGate, ArmourMarketplaceGate>(Reuse.Singleton);
        container.Register<IArmouryPlayerAdapter, ArmouryPlayerAdapter>(Reuse.Singleton);
        container.Register<ArmouryUpgradeService>(Reuse.Singleton);
        container.Register<LordHarnessService>(Reuse.Singleton);
        container.Register<Hooks.ArmouryPresenter>(Reuse.Singleton);
    }
}
