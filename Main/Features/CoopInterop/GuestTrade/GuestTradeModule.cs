using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Guest trade (#1329, docs/features/coop-interop.md): a co-op guest buys from the Elite Emissary and uses the
/// armoury through the host. TAOM.CoopCompat attaches the relay; with no relay the client is not a guest and the
/// screens keep TAOM's refusal. On the co-op server it also rolls and hands over lord's materials for co-op players
/// (<see cref="GuestLadderFindBehavior"/>).
/// </summary>
internal sealed class GuestTradeModule : TaomFeatureModule
{
    public override string Id => "GuestTrade";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<GuestTradeClient>(Reuse.Singleton);
        registrator.Register<IGuestTradeHeroAdapter, GuestTradeHeroAdapter>(Reuse.Singleton);
        registrator.Register<IGuestTradeShops, GuestTradeShops>(Reuse.Singleton);
        registrator.Register<GuestTradeHost>(Reuse.Singleton);
        registrator.Register<GuestLadderFindService>(Reuse.Singleton);
    }

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new GuestLadderFindBehavior(
            r.Resolve<GuestLadderFindService>(), r.Resolve<ISpecialResourceEventAdapter>(), r.Resolve<IModLogger>())),
    };

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
