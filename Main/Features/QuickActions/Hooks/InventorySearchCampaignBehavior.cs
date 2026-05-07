using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Per-save persistence of the inventory-search-box availability flag. MCM is the
/// authoring source of truth; <c>SyncData</c> stores the per-save resolved value so
/// the next inventory open applies it via <see cref="Patch34_SPInventoryVMSearchApply"/>.
/// </summary>
public sealed class InventorySearchCampaignBehavior : CampaignBehaviorBase
{
    private readonly IQuickActionsSettingsProvider _settings;
    private readonly IModLogger _logger;
    private bool _isSearchAvailable = true;

    public bool IsSearchAvailable => _isSearchAvailable;

    public InventorySearchCampaignBehavior(IQuickActionsSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Per-save persistence; key is TAOM-namespaced to avoid collision with the original module.
        dataStore.SyncData("TAOM_IsInventorySearchAvailable", ref _isSearchAvailable);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        _isSearchAvailable = _settings.EnableInventorySearch;
        if (_settings.IsDebugMode)
            _logger.LogDebug($"[QuickActions] new-game seeded IsSearchAvailable={_isSearchAvailable} from MCM");
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // First-load on a save written before the SyncData key existed: SyncData leaves the
        // field at its default (true). We can't distinguish "stored true" from "missing key";
        // either way, reconcile against MCM so the load is consistent with the user's current
        // preference.
        if (_isSearchAvailable != _settings.EnableInventorySearch)
        {
            _isSearchAvailable = _settings.EnableInventorySearch;
            if (_settings.IsDebugMode)
                _logger.LogDebug($"[QuickActions] on-load reconciled IsSearchAvailable={_isSearchAvailable} from MCM");
        }
    }

    private void OnTick(float dt)
    {
        // Idempotent reconciliation: if the user toggled MCM mid-campaign, keep the per-save
        // bool aligned. CampaignEvents.TickEvent fires every campaign frame (Codex review #36
        // corrected an earlier "hourly" comment). The reconciliation is a single boolean
        // compare so the per-frame cost is negligible; we rely on Patch34_SPInventoryVMSearchApply
        // to apply the value on inventory-open, not on every tick.
        if (_isSearchAvailable != _settings.EnableInventorySearch)
            _isSearchAvailable = _settings.EnableInventorySearch;
    }
}
