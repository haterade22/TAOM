using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Teams.Hooks;

namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>
/// Team practice fights (#1528, docs/features/team-practice.md): in a town of the Free Peoples the practice fight at the
/// training grounds is a team fight. Three patches on vanilla's practice controller and one on its post-fight line
/// (Patch174), two dialogue lines of the Training Master, one MCM switch. No model, no setting beyond the switch and no
/// save data. Needs <c>IAlignmentService</c> (ExecutionIoC) and the two co-op providers, registered elsewhere.
/// </summary>
internal sealed class TeamPracticeModule : TaomFeatureModule
{
    internal const string PatchCategory = ArenaPracticeFightMissionController_TeamPractice_Patch.Category;

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(_ => new TeamPracticeDialogBehavior()),
    };

    public override string Id => "TeamPractice";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ITeamPracticeSettingsProvider, TeamPracticeSettingsProvider>(Reuse.Singleton);
        registrator.Register<TeamPracticeService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        ArenaPracticeFightMissionController_TeamPractice_Patch.Initialize(
            resolver.Resolve<TeamPracticeService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
