using DryIoc;

namespace TAOM.Features.MarriageAlignment;

public static class MarriageAlignmentIoC
{
    public static void RegisterMarriageAlignmentFeature(IContainer container)
    {
        container.Register<IMarriageAlignmentConfigProvider, MarriageAlignmentConfigProvider>(Reuse.Singleton);
        container.Register<IMarriageAlignmentSettingsProvider, MarriageAlignmentSettingsProvider>(Reuse.Singleton);
        container.Register<IMarriageAlignmentService, MarriageAlignmentService>(Reuse.Singleton);
    }
}
