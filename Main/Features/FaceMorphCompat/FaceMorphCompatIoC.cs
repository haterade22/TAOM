using DryIoc;

namespace TAOM.Features.FaceMorphCompat;

public static class FaceMorphCompatIoC
{
    public static void RegisterFaceMorphCompatFeature(IContainer container)
    {
        container.Register<IFaceMorphCompatService, FaceMorphCompatService>(Reuse.Singleton);
    }
}
