using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentRecruitment.Hooks;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Alignment recruitment (#286, docs/features/alignment-recruitment.md): the services stay registered in
/// <see cref="RecruitmentAlignmentIoC"/>; this module adds the AI trip-scoring postfix (#887, Patch114) so a lord
/// does not plan a recruiting trip that <c>TaomVolunteerModel</c> would then block. No model, behavior, setting or save data.
/// </summary>
internal sealed class AlignmentRecruitmentModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch114_AlignmentRecruitmentScoring";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "AlignmentRecruitment";

    public override void InitializeStatics(IResolver resolver) =>
        Patch114_AiVolunteerEstimate_Postfix.Initialize(resolver.Resolve<IRecruitmentAlignmentService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
