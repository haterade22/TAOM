using TaleWorlds.CampaignSystem;
using TAOM.Adapters;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>
/// Registers the cheat menu's game menus at session launch (<see cref="CheatMenuMenus"/>), and from the tick attaches the map
/// view that reads the map key (MapScreen.Instance does not exist at session launch and the screen is rebuilt on every load,
/// the realm borders' pattern). Saves one flag: the "Unlimited party" switch (<see cref="ICheatPartyRules"/>).
/// </summary>
public sealed class CheatMenuCampaignBehavior : CampaignBehaviorBase
{
    internal const string UnlimitedPartyKey = "_taom_cheat_unlimitedParty";

    private readonly CheatMenuService _service;
    private readonly ICheatMenuSettingsProvider _settings;
    private readonly ICheatWorldAdapter _world;
    private readonly ICheatMenuMapViewAdapter _mapView;
    private readonly ICheatPartyRules _partyRules;

    public CheatMenuCampaignBehavior(CheatMenuService service, ICheatMenuSettingsProvider settings, ICheatWorldAdapter world,
        ICheatMenuMapViewAdapter mapView, ICheatPartyRules partyRules)
    {
        _service = service;
        _settings = settings;
        _world = world;
        _mapView = mapView;
        _partyRules = partyRules;
        // The rules are a process-wide singleton and this behavior is built for every campaign: a new campaign starts with the
        // switch off, and a loaded save sets it in SyncData (a save from before the switch has no key and keeps it off).
        _partyRules.UnlimitedParty = false;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, (_, _, _, _) => _service.OnPlayerCharacterChanged());
    }

    public override void SyncData(IDataStore dataStore)
    {
        var unlimited = _partyRules.UnlimitedParty;
        dataStore.SyncData(UnlimitedPartyKey, ref unlimited);
        _partyRules.UnlimitedParty = unlimited;
    }

    // Main thread, after SyncData on a load: a switch saved on is turned off outside single player and refreshes the size cache.
    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        CheatMenuMenus.Register(starter, _service, _settings, _world);
        _service.ReconcileAtSessionLaunch();
    }

    internal void OnTick(float dt) => _mapView.EnsureAttached();
}
