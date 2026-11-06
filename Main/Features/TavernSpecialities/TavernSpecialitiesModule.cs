using System.Collections.Generic;
using TAOM.Composition;

namespace TAOM.Features.TavernSpecialities;

/// <summary>
/// Tavern Specialities (#937, docs/features/tavern-specialities.md): gives the tavern maid default dishes in a town
/// whose culture vanilla has none for, by pre-setting them in a prefix. One patch; no model, behavior, setting or save data.
/// </summary>
internal sealed class TavernSpecialitiesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch111_TavernSpecialities";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "TavernSpecialities";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
