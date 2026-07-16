using DryIoc;

namespace TAOM.Features.PrisonerRecruitment;

public static class PrisonerRecruitmentIoC
{
    public static void RegisterPrisonerRecruitmentFeature(IContainer container)
    {
        container.Register<IPrisonerRecruitmentSettingsProvider, PrisonerRecruitmentSettingsProvider>(Reuse.Singleton);
        container.Register<IPrisonerRecruitmentMoraleService, PrisonerRecruitmentMoraleService>(Reuse.Singleton);
    }
}
