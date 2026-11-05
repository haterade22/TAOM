using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BardTownGuard.Hooks;

namespace TAOM.Features.BardTownGuard;

/// <summary>
/// Bard Town Guard (#824, docs/features/bard-town-guard.md): stops the tavern musician's conversation condition from
/// throwing in a town whose culture owns no other town. One transpiler patch; no model, behavior, setting or save data.
/// </summary>
internal sealed class BardTownGuardModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch110_BardTownGuard";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "BardTownGuard";

    public override void InitializeStatics(IResolver resolver) =>
        TavernEmployeesCampaignBehavior_BardCondition_BardTownGuard_Patch.Initialize(resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
