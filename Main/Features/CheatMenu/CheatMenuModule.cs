using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu.Battle;
using TAOM.Features.CheatMenu.Hooks;

namespace TAOM.Features.CheatMenu;

/// <summary>
/// Cheat Menu (docs/features/cheat-menu.md): with the MCM switch on and in single player, a "Cheats" button on the campaign
/// map's bar opens three game menus of cheats (teleport, gold, special resource, influence, renown, troops, relations, heal),
/// which the <c>taom.cheat_*</c> console commands share through <see cref="CheatMenuService"/>. One patch (Patch151, the
/// button), one behavior (the menus, and it attaches the map view that reads the map key), one rebindable key (H by default,
/// <see cref="TaomCheatMenuHotKeyCategory"/>), and one saved flag, the "Unlimited party" switch (<see cref="ICheatPartyRules"/>). The button element and the map view live in TAOM.Client (ADR-013).
/// In a mission, Patch152 (TAOM.Client, the Esc menu's "Cheats" item) opens the battle list, which the
/// <c>taom.cheat_battle_*</c> commands share through <see cref="BattleCheatService"/>.
/// </summary>
internal sealed class CheatMenuModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch151_CheatMenu";
    internal const string BattlePatchCategory = "Patch152_BattleCheatMenu";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
        new(BattlePatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CheatMenuCampaignBehavior(
            r.Resolve<CheatMenuService>(),
            r.Resolve<ICheatMenuSettingsProvider>(),
            r.Resolve<ICheatWorldAdapter>(),
            r.Resolve<ICheatMenuMapViewAdapter>(),
            r.Resolve<ICheatPartyRules>())),
    };

    public override string Id => "CheatMenu";

    // The "Unlimited party" switch is saved with the campaign (CheatMenuCampaignBehavior.SyncData).
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICheatMenuSettingsProvider, CheatMenuSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICheatWorldAdapter, CheatWorldAdapter>(Reuse.Singleton);
        registrator.Register<ICheatMenuHostAdapter, CheatMenuHostAdapter>(Reuse.Singleton);
        registrator.Register<ICheatPartyRules, CheatPartyRules>(Reuse.Singleton);
        registrator.Register<CheatMenuService>(Reuse.Singleton);
        registrator.Register<CheatMenuOpener>(Reuse.Singleton);
        registrator.Register<ICheatMenuNavigationElementFactory, NullCheatMenuNavigationElementFactory>(Reuse.Singleton);
        registrator.Register<ICheatMenuMapViewAdapter, NullCheatMenuMapViewAdapter>(Reuse.Singleton);
        registrator.Register<IBattleCheatAdapter, BattleCheatAdapter>(Reuse.Singleton);
        registrator.Register<IBattleCheatHostAdapter, BattleCheatHostAdapter>(Reuse.Singleton);
        registrator.Register<BattleCheatService>(Reuse.Singleton);
        registrator.Register<BattleCheatOpener>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch151_CheatMenuNavigation.Initialize(
            resolver.Resolve<CheatMenuOpener>(),
            resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    /// <summary>
    /// Registers the map key in OnSubModuleLoad, after Native has cleared and filled the key contexts, as the realm borders
    /// do. A failure leaves the key unbound (the button still works); letting it escape would fault the module.
    /// </summary>
    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.ProcessLoad)
            return;
        try
        {
            TaomCheatMenuHotKeyCategory.Register();
        }
        catch (Exception ex)
        {
            resolver.Resolve<IModLogger>().LogWarning(
                $"[CheatMenu] hotkey registration failed, the cheat menu key stays unbound: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
