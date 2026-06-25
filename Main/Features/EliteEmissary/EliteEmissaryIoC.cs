using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.EliteEmissary;

public static class EliteEmissaryIoC
{
    public static void RegisterEliteEmissaryFeature(IContainer container)
    {
        container.Register<IEliteEmissaryConfigProvider, EliteEmissaryConfigProvider>(Reuse.Singleton);
        container.Register<IEliteEmissarySettingsProvider, EliteEmissarySettingsProvider>(Reuse.Singleton);
        container.Register<IEliteEmissaryService, EliteEmissaryService>(Reuse.Singleton);
        container.Register<ISettlementOwnerAdapter, SettlementOwnerAdapter>(Reuse.Singleton);
        container.Register<IPlayerPartyAdapter, PlayerPartyAdapter>(Reuse.Singleton);
    }
}
