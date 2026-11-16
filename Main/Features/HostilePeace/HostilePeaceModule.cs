using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.HostilePeace.Hooks;

namespace TAOM.Features.HostilePeace;

/// <summary>
/// Hostile Peace (docs/features/hostile-peace.md): Free and Dark Power realms (the Hostile tier of <c>diplomacy.json</c>) stay at
/// peace until the War of the Ring, but do not act as friends. Four blocks, one switch each in MCM and
/// <c>hostile_peace.json</c>: A, no caravan trade in each other's towns (Patch156, postfix on <c>CaravansCampaignBehavior.CanTradeWith</c>);
/// B, no trade agreement (<c>TaomTradeAgreementModel</c>); C, the player meets their settlements as at war
/// (<c>TaomSettlementAccessModel</c>); D, the player's kingdom does not ally across the Free/Evil line (<c>PlayerAllianceGate</c>). The two models are game models that <c>SubModule.RegisterDiplomacyAndConflict</c> adds by
/// hand. Nothing is saved. Overlap check, 2026-11-15: TAOM overrides neither <c>TradeAgreementModel</c> nor
/// <c>SettlementAccessModel</c>; CaravanTrade's postfix on <c>CanTradeWith</c> only lifts war blocks. <c>IDiplomacyService</c>
/// (<c>DiplomacyIoC</c>), <c>IPathService</c> and <c>IModLogger</c> are registered elsewhere.
/// </summary>
internal sealed class HostilePeaceModule : TaomFeatureModule
{
    // CaravansCampaignBehavior lives in TaleWorlds.CampaignSystem, which loads with the process.
    internal const string PatchCategory = "Patch156_HostilePeace";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "HostilePeace";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHostilePeaceConfigProvider, HostilePeaceConfigProvider>(Reuse.Singleton);
        registrator.Register<IHostilePeaceSettingsProvider, HostilePeaceSettingsProvider>(Reuse.Singleton);
        registrator.Register<HostilePeaceService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch156_CaravanTrade_HostilePeace.Initialize(resolver.Resolve<HostilePeaceService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
