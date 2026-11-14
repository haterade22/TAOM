using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu.Hooks;

namespace TAOM.Features.CheatMenu;

/// <summary>
/// Cheat Menu (docs/features/cheat-menu.md): with the MCM switch on and in single player, a "Cheats" button on the campaign
/// map's bar opens three game menus of cheats (teleport, gold, special resource, influence, renown, troops, relations, heal),
/// which the <c>taom.cheat_*</c> console commands share through <see cref="CheatMenuService"/>. One patch (Patch151, the
/// button), one behavior (the menus), nothing saved. The button element lives in TAOM.Client (ADR-013).
/// </summary>
internal sealed class CheatMenuModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch151_CheatMenu";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CheatMenuCampaignBehavior(
            r.Resolve<CheatMenuService>(),
            r.Resolve<ICheatMenuSettingsProvider>(),
            r.Resolve<ICheatWorldAdapter>())),
    };

    public override string Id => "CheatMenu";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICheatMenuSettingsProvider, CheatMenuSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICheatWorldAdapter, CheatWorldAdapter>(Reuse.Singleton);
        registrator.Register<ICheatMenuHostAdapter, CheatMenuHostAdapter>(Reuse.Singleton);
        registrator.Register<CheatMenuService>(Reuse.Singleton);
        registrator.Register<CheatMenuOpener>(Reuse.Singleton);
        registrator.Register<ICheatMenuNavigationElementFactory, NullCheatMenuNavigationElementFactory>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch151_CheatMenuNavigation.Initialize(
            resolver.Resolve<CheatMenuOpener>(),
            resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
