using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// Career sync (docs/features/coop-interop.md, "Career sync"): a co-op player's career goes to the co-op server, which
/// checks it, keeps it in its save and passes it on. TAOM.CoopCompat resolves the client and the host and attaches the
/// relay; without it nothing resolves them, so single player is unchanged.
/// </summary>
internal sealed class CareerSyncModule : TaomFeatureModule
{
    public override string Id => "CareerSync";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<CareerSyncClient>(Reuse.Singleton);
        registrator.Register<CareerSyncHost>(Reuse.Singleton);
        registrator.Register<ICareerSyncHeroAdapter, CareerSyncHeroAdapter>(Reuse.Singleton);
        registrator.Register<ICareerSyncNotice, CareerSyncNotice>(Reuse.Singleton);
        registrator.RegisterDelegate(_ => new CoopSettingsProvider(), Reuse.Singleton);
    }
}
