using TaleWorlds.CampaignSystem;
using TAOM.Adapters;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>Registers the cheat menu's game menus at session launch (<see cref="CheatMenuMenus"/>). No state, nothing saved.</summary>
public sealed class CheatMenuCampaignBehavior : CampaignBehaviorBase
{
    private readonly CheatMenuService _service;
    private readonly ICheatMenuSettingsProvider _settings;
    private readonly ICheatWorldAdapter _world;

    public CheatMenuCampaignBehavior(CheatMenuService service, ICheatMenuSettingsProvider settings, ICheatWorldAdapter world)
    {
        _service = service;
        _settings = settings;
        _world = world;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => CheatMenuMenus.Register(starter, _service, _settings, _world);
}
