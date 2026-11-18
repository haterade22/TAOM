using DryIoc;

namespace TAOM.Features.ImeGuard;

public static class ImeGuardIoC
{
    /// <summary>Core registers the null window adapter; the client replaces it (TaomClientComposition).</summary>
    public static void RegisterImeGuardFeature(IContainer container)
    {
        container.Register<IImeWindowAdapter, NullImeWindowAdapter>(Reuse.Singleton);
        container.Register<IImeGuardSettingsProvider, ImeGuardSettingsProvider>(Reuse.Singleton);
        container.Register<ImeGuardService>(Reuse.Singleton);
    }
}
