using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle.Hooks;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.CoopInterop;
using TAOM.Features.Relations;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// Bonds of Battle (docs/features/bonds-of-battle.md): relation earned on campaign. Three entry points share one settings
/// group and the shared seam for new relation code (<see cref="IRelationGrantService"/>): a campaign behavior at the end
/// of each player battle, Patch106 on the Donate Troops screen's Done, and a barter model for the overpay cap. Nothing is
/// saved. Patch106 targets a campaign party screen, so it applies at GameInit like the other campaign-menu patches.
/// Overlap check, 2026-10-18: no other TAOM code fills the <c>BarterModel</c> slot. Since #1516 Patch26 also postfixes
/// <c>PartyScreenLogic.DoneLogic</c>; neither postfix reads what the other writes. The seam's registration belongs to
/// <c>RelationsModule</c>; never register it a second time here (a second unkeyed default makes every resolve throw, Main/IoC.cs).
/// </summary>
internal sealed class BondsOfBattleModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch106_BondsOfBattle";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new BondsOfBattleBehavior(
            r.Resolve<BattleRelationService>(),
            r.Resolve<TroopHandoverService>(),
            r.Resolve<IBattleRelationAdapter>(),
            r.Resolve<IBondsOfBattleSettingsProvider>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),
    };

    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<BarterModel, TaomBarterModel>(ModelTarget.Campaign,
            resolver => new TaomBarterModel(resolver.Resolve<BarterOverpayService>())),
    };

    public override string Id => "BondsOfBattle";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IBondsOfBattleSettingsProvider, BondsOfBattleSettingsProvider>(Reuse.Singleton);
        registrator.Register<IBattleRelationAdapter, BattleRelationAdapter>(Reuse.Singleton);
        registrator.Register<IBondsNoticeAdapter, BondsNoticeAdapter>(Reuse.Singleton);
        // IRelationGrantService and IHeroRelationAdapter, the shared seam for new relation code, are registered by RelationsModule.
        registrator.Register<BattleRelationService>(Reuse.Singleton);
        registrator.Register<TroopHandoverService>(Reuse.Singleton);
        registrator.Register<BarterOverpayService>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
