using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MountReins.Hooks;

namespace TAOM.Features.MountReins;

/// <summary>
/// Mount reins guard (docs/features/mount-reins-guard.md): the engine builds a harness's cloth reins only on a mount
/// whose Monster has a rein skeleton. One transpiler patch; no model, behavior, setting or save data.
/// </summary>
internal sealed class MountReinsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch153_MountReinsGuard";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    internal static IModLogger? Logger { get; private set; }

    public override string Id => "MountReins";

    public override void InitializeStatics(IResolver resolver)
    {
        Logger = resolver.Resolve<IModLogger>();
        MountReinsHook.Initialize(Logger);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
