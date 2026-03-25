using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.Diplomacy;

public static class DiplomacyIoC
{
    public static void RegisterDiplomacyFeature(IContainer container)
    {
        container.Register<IAllianceAdapter, AllianceAdapter>(Reuse.Singleton);
        container.Register<IDiplomacyConfigProvider, DiplomacyConfigProvider>(Reuse.Singleton);
        container.Register<IDiplomacyService, DiplomacyService>(Reuse.Singleton);
    }
}
