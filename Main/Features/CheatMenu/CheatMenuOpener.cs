using TAOM.Adapters;

namespace TAOM.Features.CheatMenu;

public enum CheatMenuAvailability
{
    Off,
    Coop,
    MapBusy,
    Available,
}

/// <summary>Menu and option ids of the cheat menu's game menus.</summary>
public static class CheatMenuIds
{
    public const string NavigationElementId = "taom_cheats";
    public const string Root = "taom_cheat_menu";
    public const string Teleport = "taom_cheat_teleport";
    public const string Troops = "taom_cheat_troops";
}

/// <summary>
/// The one place that decides whether the cheat menu may show and open: the MCM switch, single player, and the free map
/// (the same map-menu gate as the Fiefs hub). The map button asks <see cref="IsShown"/> once when the map bar is built and
/// <see cref="GetAvailability"/> every frame.
/// </summary>
public sealed class CheatMenuOpener
{
    private readonly ICheatMenuSettingsProvider _settings;
    private readonly CheatMenuService _service;
    private readonly ICheatMenuHostAdapter _host;

    public CheatMenuOpener(ICheatMenuSettingsProvider settings, CheatMenuService service, ICheatMenuHostAdapter host)
    {
        _settings = settings;
        _service = service;
        _host = host;
    }

    public bool IsShown => CheatMenuPolicy.IsMenuAvailable(_settings.EnableCheatMenu, _service.IsSinglePlayer);

    public CheatMenuAvailability GetAvailability()
    {
        if (!_settings.EnableCheatMenu) return CheatMenuAvailability.Off;
        if (!_service.IsSinglePlayer) return CheatMenuAvailability.Coop;
        if (!_host.IsMapClearForMenu) return CheatMenuAvailability.MapBusy;
        return CheatMenuAvailability.Available;
    }

    public bool TryOpen()
    {
        if (GetAvailability() != CheatMenuAvailability.Available) return false;
        _host.OpenMenu();
        return true;
    }
}
