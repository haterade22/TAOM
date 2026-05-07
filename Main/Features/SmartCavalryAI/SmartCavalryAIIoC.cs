using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.SmartCavalryAI;

public static class SmartCavalryAIIoC
{
    public static void RegisterSmartCavalryAIFeature(IContainer container)
    {
        container.Register<ISmartCavalryAISettingsProvider, SmartCavalryAISettingsProvider>(Reuse.Singleton);
        container.Register<IBattlefieldQueryAdapter, BattlefieldQueryAdapter>(Reuse.Singleton);
        container.Register<ICavalryPathPlanner, CavalryPathPlanner>(Reuse.Singleton);
        container.Register<ICavalryChargeService, CavalryChargeService>(Reuse.Singleton);
    }
}
