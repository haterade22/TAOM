using DryIoc;

namespace TAOM.Features.Warg;

public static class WargIoC
{
    public static void RegisterWargFeature(IContainer container)
    {
        container.Register<IWargAttackService, WargAttackService>(Reuse.Transient);
    }
}
