using DryIoc;

namespace TAOM.Features.NazgulFamily;

public static class NazgulFamilyIoC
{
    public static void RegisterNazgulFamilyFeature(IContainer container)
    {
        container.Register<INazgulRegistry, NazgulRegistry>(Reuse.Singleton);
    }
}
