using DryIoc;

namespace TAOM.Features.AlignmentRecruitment;

public static class RecruitmentAlignmentIoC
{
    public static void RegisterAlignmentRecruitmentFeature(IContainer container)
    {
        container.Register<IRecruitmentAlignmentConfigProvider, RecruitmentAlignmentConfigProvider>(Reuse.Singleton);
        container.Register<IRecruitmentAlignmentSettingsProvider, RecruitmentAlignmentSettingsProvider>(Reuse.Singleton);
        container.Register<IRecruitmentAlignmentService, RecruitmentAlignmentService>(Reuse.Singleton);
    }
}
