using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.CheatScreenGuard.Hooks;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>
/// Cheat-screen guard (docs/features/cheat-screen-guard.md): with vanilla cheat mode on, the own inventory lists every item
/// and the party screen every troop (gigabytes of memory with TAOM's content). Two transpilers gate the one cheat-mode
/// read in each opener behind an MCM setting, default off. Applied at GameInit, before any campaign screen can open; the
/// step after the category writes the install line from both swap counts.
/// </summary>
internal sealed class CheatScreenGuardModule : TaomFeatureModule
{
    // No number: a 1.4.x-only feature, so it takes no number the 1.5.x line could also use.
    internal const string PatchCategory = "Patch_CheatScreenGuard";

    private static readonly PatchCategoryDecl[] Categories = { new(PatchCategory, ApplyPhase.GameInit) };

    public override string Id => "CheatScreenGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICheatScreenGuardSettingsProvider, CheatScreenGuardSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICheatScreenGuardService, CheatScreenGuardService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        CheatScreenGuardCalls.Initialize(resolver.Resolve<ICheatScreenGuardService>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.GameInit)
            resolver.Resolve<ICheatScreenGuardService>().LogInstall(
                CheatScreenGuardSwaps.LastInventorySwapped, CheatScreenGuardSwaps.LastPartySwapped);
    }
}
