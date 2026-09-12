using DryIoc;

namespace TAOM.Features.LordPartyTemplates;

public static class LordPartyTemplatesIoC
{
    public static void RegisterLordPartyTemplatesFeature(IContainer container)
    {
        container.Register<ILordPartyTemplateConfigProvider, LordPartyTemplateConfigProvider>(Reuse.Singleton);
        container.Register<ILordPartyTemplateService, LordPartyTemplateService>(Reuse.Singleton);
    }
}
