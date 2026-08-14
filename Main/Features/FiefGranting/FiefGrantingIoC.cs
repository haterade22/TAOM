using DryIoc;

namespace TAOM.Features.FiefGranting;

public static class FiefGrantingIoC
{
    public static void RegisterFiefGrantingFeature(IContainer container)
    {
        container.Register<IFiefGrantSettingsProvider, FiefGrantSettingsProvider>(Reuse.Singleton);
        container.Register<IFiefGrantPolicyService, FiefGrantPolicyService>(Reuse.Singleton);
    }
}
