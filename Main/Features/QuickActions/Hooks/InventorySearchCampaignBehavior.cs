using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.QuickActions.Hooks;

/// <summary>
/// Per-save persistence of the inventory-search-box availability flag. MCM is the
/// authoring source of truth at NEW-game time; <c>SyncData</c> stores the per-save resolved value
/// so subsequent loads use that value (not the current MCM) until the player explicitly toggles
/// MCM mid-game.
/// </summary>
public sealed class InventorySearchCampaignBehavior : CampaignBehaviorBase
{
    private readonly IQuickActionsSettingsProvider _settings;
    private readonly IModLogger _logger;
    private bool _isSearchAvailable = true;
    // Phase 9b #146 — version tag distinguishes "stored true" from "missing key on legacy save".
    // Bump when persistence semantics change.
    //   v0 = pre-#146 / legacy save (no version key stored) — must reconcile against MCM on load.
    //   v1 = #146 onward — `_isSearchAvailable` is authoritative on load, MCM reconciles only
    //        when explicitly toggled mid-game (handled by OnTick).
    private const int CurrentVersion = 1;
    private int _persistedVersion = 0;
    // Tracks the MCM value observed on the prior tick. Mid-game MCM toggle = transition
    // observed != current → user changed their preference; we update the per-save value to match.
    private bool _lastSeenMcmValue = true;

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
        // Phase 9b #146 — Per-save persistence with explicit version tag so we can distinguish a
        // legacy save (no version key, can't tell stored-true from missing) from a new save where
        // _isSearchAvailable is authoritative.
        dataStore.SyncData("TAOM_IsInventorySearchAvailable", ref _isSearchAvailable);
        dataStore.SyncData("TAOM_InventorySearchVersion", ref _persistedVersion);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        _isSearchAvailable = _settings.EnableInventorySearch;
        _persistedVersion = CurrentVersion;
        _lastSeenMcmValue = _settings.EnableInventorySearch;
        if (_settings.IsDebugMode)
            _logger.LogDebug($"[QuickActions] new-game seeded IsSearchAvailable={_isSearchAvailable} from MCM");
    }

    private void OnGameLoaded(CampaignGameStarter starter)
    {
        // Phase 9b #146 — only reconcile against MCM on LEGACY saves. New saves are
        // authoritative; the player's per-save preference is preserved across loads.
        if (_persistedVersion < CurrentVersion)
        {
            // Legacy save (pre-#146): can't distinguish stored-true from missing-key. Reconcile.
            _isSearchAvailable = _settings.EnableInventorySearch;
            _persistedVersion = CurrentVersion;
            if (_settings.IsDebugMode)
                _logger.LogDebug($"[QuickActions] legacy-save migration: reconciled IsSearchAvailable={_isSearchAvailable} from MCM, tagged v{CurrentVersion}");
        }
        else if (_settings.IsDebugMode)
        {
            _logger.LogDebug($"[QuickActions] on-load preserved IsSearchAvailable={_isSearchAvailable} (saved v{_persistedVersion})");
        }
        _lastSeenMcmValue = _settings.EnableInventorySearch;
    }

    private void OnTick(float dt)
    {
        // Phase 9b #146 — mid-game MCM toggle detection. Only update the per-save bool when the
        // user *just changed* the MCM value (transition: _lastSeenMcmValue != current). This
        // preserves "MCM mid-game toggle takes effect" without flattening the per-save preference
        // every tick.
        var currentMcm = _settings.EnableInventorySearch;
        if (currentMcm != _lastSeenMcmValue)
        {
            // User toggled MCM this tick — propagate to per-save and update the observation.
            _isSearchAvailable = currentMcm;
            _lastSeenMcmValue = currentMcm;
            if (_settings.IsDebugMode)
                _logger.LogDebug($"[QuickActions] MCM toggle detected: IsSearchAvailable={_isSearchAvailable}");
        }
    }
}
