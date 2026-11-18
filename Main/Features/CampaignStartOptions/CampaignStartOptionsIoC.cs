using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.CampaignStartOptions;

public static class CampaignStartOptionsIoC
{
    public static void RegisterCampaignStartOptionsFeature(IContainer container)
    {
        container.Register<IAdvancedStartDataAdapter, AdvancedStartDataAdapter>(Reuse.Singleton);
        container.Register<ICampaignStartDefaults, CampaignStartDefaultsProvider>(Reuse.Singleton);

        container.Register<CampaignStartOptionsStore>(Reuse.Singleton);
        container.RegisterMapping<ICampaignStartOptions, CampaignStartOptionsStore>();
        container.Register<ICampaignStartSession, CampaignStartSession>(Reuse.Singleton);

        container.Register<ICampaignStartNoticeAdapter, CampaignStartNoticeAdapter>(Reuse.Singleton);
        container.Register<CampaignStartMismatchNotice>(Reuse.Singleton);
        container.Register<CampaignOptionEditor>(Reuse.Singleton);
    }
}
