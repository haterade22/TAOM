using TaleWorlds.CampaignSystem;
using TAOM.Adapters;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>
/// Registers the cheat menu's game menus at session launch (<see cref="CheatMenuMenus"/>), and from the tick attaches the map
/// view that reads the map key (MapScreen.Instance does not exist at session launch and the screen is rebuilt on every load,
/// the realm borders' pattern). No state, nothing saved.
/// </summary>
public sealed class CheatMenuCampaignBehavior : CampaignBehaviorBase
{
    private readonly CheatMenuService _service;
    private readonly ICheatMenuSettingsProvider _settings;
    private readonly ICheatWorldAdapter _world;
    private readonly ICheatMenuMapViewAdapter _mapView;

    public CheatMenuCampaignBehavior(CheatMenuService service, ICheatMenuSettingsProvider settings, ICheatWorldAdapter world,
        ICheatMenuMapViewAdapter mapView)
    {
        _service = service;
        _settings = settings;
        _world = world;
        _mapView = mapView;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => CheatMenuMenus.Register(starter, _service, _settings, _world);

    internal void OnTick(float dt) => _mapView.EnsureAttached();
}
