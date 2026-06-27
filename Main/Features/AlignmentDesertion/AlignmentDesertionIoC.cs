using DryIoc;

namespace TAOM.Features.AlignmentDesertion;

public static class AlignmentDesertionIoC
{
    public static void RegisterAlignmentDesertionFeature(IContainer container)
    {
        container.Register<IAlignmentDesertionConfigProvider, AlignmentDesertionConfigProvider>(Reuse.Singleton);
        container.Register<IAlignmentDesertionSettingsProvider, AlignmentDesertionSettingsProvider>(Reuse.Singleton);
        container.Register<IAlignmentDesertionService, AlignmentDesertionService>(Reuse.Singleton);
    }
}
