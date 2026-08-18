using DryIoc;

namespace TAOM.Features.AiPartySize;

public static class AiPartySizeIoC
{
    public static void RegisterAiPartySizeFeature(IContainer container)
    {
        container.Register<IAiPartySizeService, AiPartySizeService>(Reuse.Singleton);
    }
}
