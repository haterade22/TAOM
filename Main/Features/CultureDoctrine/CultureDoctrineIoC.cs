using DryIoc;

namespace TAOM.Features.CultureDoctrine;

public static class CultureDoctrineIoC
{
    public static void RegisterCultureDoctrineFeature(IContainer container)
    {
        container.Register<ICultureDoctrineConfigProvider, CultureDoctrineConfigProvider>(Reuse.Singleton);
        container.Register<ICultureDoctrineSettingsProvider, CultureDoctrineSettingsProvider>(Reuse.Singleton);
    }
}
