using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.FiefManagement;

/// <summary>
/// The one place that decides whether the fief hub opens, and opens it. F6 (<c>Patch36_MapScreenF6</c>)
/// and the navigation-bar button (<c>TaomFiefsNavigationElement</c>) share this opener and, through
/// <see cref="IFiefHubHostAdapter"/>, the one map-menu gate (<c>MapMenuGate</c>). The button additionally
/// honours vanilla's <c>IsNavigationBarEnabled</c>, which is a property of the bar, not of the hub. A concrete class: a test fakes its collaborators, nothing else implements it.
/// </summary>
public sealed class FiefHubOpener
{
    private readonly IFiefManagementSettingsProvider _settings;
    private readonly IFiefHubService _service;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IFiefHubHostAdapter _host;
    private readonly IModLogger _logger;

    public FiefHubOpener(
        IFiefManagementSettingsProvider settings,
        IFiefHubService service,
        IEnlistmentStateQuery enlistment,
        IFiefHubHostAdapter host,
        IModLogger logger)
    {
        _settings = settings;
        _service = service;
        _enlistment = enlistment;
        _host = host;
        _logger = logger;
    }

    /// <summary>
    /// Cheap and side-effect-free: the navigation button polls it every frame. The order is fixed:
    /// feature toggle, then map state, then fief count, so a busy map never pays for the count.
    /// </summary>
    public FiefHubAvailability GetAvailability()
    {
        if (!_settings.EnableFiefManagement) return FiefHubAvailability.FeatureDisabled;
        if (!_host.IsMapClearForMenu) return FiefHubAvailability.MapBusy;

        // Enlisted-attached service is a modal state of its own (parked at the wait menu; the menu
        // guard owns transitions), so the hub must not be pushed into it. The other service states
        // (duty, grace, captive, commander unavailable) are free-roam and keep the hub.
        if (_enlistment.State == EnlistmentState.EnlistedAttached) return FiefHubAvailability.MapBusy;

        if (_service.Count <= 0) return FiefHubAvailability.NoFiefs;
        return FiefHubAvailability.Available;
    }

    /// <summary>
    /// Opens the hub when <see cref="GetAvailability"/> says <see cref="FiefHubAvailability.Available"/>,
    /// shows the "no fiefs" message for <see cref="FiefHubAvailability.NoFiefs"/>, and stays silent for the
    /// other two. <paramref name="source"/> ("F6", "nav button") only labels the debug log line.
    /// </summary>
    public void TryOpen(string source)
    {
        switch (GetAvailability())
        {
            case FiefHubAvailability.NoFiefs:
                _host.ShowNoFiefsMessage();
                if (_settings.IsDebugMode)
                    _logger.LogInfo($"[FiefManagement] {source} pressed but player owns no fiefs");
                break;
            case FiefHubAvailability.Available:
                if (_settings.IsDebugMode)
                    _logger.LogInfo($"[FiefManagement] {source} - opening fief_hub menu (count={_service.Count})");
                _host.OpenHub();
                break;
        }
    }
}
