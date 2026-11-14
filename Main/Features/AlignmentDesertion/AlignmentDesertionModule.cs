using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Alignment desertion (docs/features/alignment-desertion.md): the services, behavior and models stay wired in
/// <see cref="AlignmentDesertionIoC"/> and <c>SubModule.cs</c>; this module adds the prisoner conformity prefix (#1189,
/// Patch150), so a prisoner stack the recruit guard blocks no longer takes a party's conformity tick. No model,
/// behavior, setting or save data of its own.
/// </summary>
internal sealed class AlignmentDesertionModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch150_DesertionConformity";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "AlignmentDesertion";

    public override void InitializeStatics(IResolver resolver) =>
        Patch150_ConformityTick_Prefix.Initialize(resolver.Resolve<IDesertionRecruitGuard>(),
            resolver.Resolve<ICoopSessionProvider>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
