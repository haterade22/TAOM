using DryIoc;
using TAOM.Adapters;
using TAOM.Features.RealmBorders.Hooks;

namespace TAOM.Features.RealmBorders;

public static class RealmBordersIoC
{
    public static void RegisterRealmBordersFeature(IRegistrator registrator)
    {
        registrator.Register<IRealmBordersSettings, RealmBordersSettingsProvider>(Reuse.Singleton);
        registrator.Register<RealmPaletteProvider>(Reuse.Singleton);
        registrator.Register<ITerritoryWorker, TaskTerritoryWorker>(Reuse.Singleton);

        registrator.Register<IMapTerrainAdapter, MapTerrainAdapter>(Reuse.Singleton);
        registrator.Register<IRealmMapAdapter, RealmMapAdapter>(Reuse.Singleton);
        registrator.Register<IBorderRenderAdapter, BorderRenderAdapter>(Reuse.Singleton);
        registrator.Register<IRealmNoticeAdapter, RealmNoticeAdapter>(Reuse.Singleton);

        // Singletons: the territory is per map and outlives a campaign; the border service holds the
        // drawn tiles, reset per session by the behavior (Singleton Services Session-Reset rule).
        registrator.Register<RealmTerritoryService>(Reuse.Singleton);
        registrator.Register<RealmBorderService>(Reuse.Singleton);
        registrator.Register<RealmBordersCampaignBehavior>(Reuse.Singleton);
    }
}
