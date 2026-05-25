using DryIoc;

namespace TAOM.Features.SettlementNameplateFade;

public static class NameplateFadeIoC
{
    public static void RegisterSettlementNameplateFadeFeature(IContainer container)
    {
        container.Register<INameplateFadeSettingsProvider, NameplateFadeSettingsProvider>(Reuse.Singleton);
        container.Register<INameplateFadeService, NameplateFadeService>(Reuse.Singleton);
    }
}
