using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Guest trade (#1329, docs/features/coop-interop.md): a co-op guest buys from the Elite Emissary and uses the
/// armoury through the host. TAOM.CoopCompat attaches the relay; with no relay the client is not a guest and the
/// screens keep TAOM's refusal.
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
    }
}
