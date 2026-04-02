using DryIoc;

namespace TAOM.Features.MainMenuCustomizer;

public static class MainMenuCustomizerIoC
{
    public static void RegisterMainMenuCustomizerFeature(IContainer container)
    {
        container.Register<IModuleMenuAdapter, ModuleMenuAdapter>(Reuse.Singleton);
        container.Register<IMainMenuCustomizerService, MainMenuCustomizerService>(Reuse.Singleton);
    }
}
