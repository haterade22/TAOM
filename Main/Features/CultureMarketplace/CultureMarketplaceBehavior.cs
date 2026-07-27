using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureMarketplace.Domain;

namespace TAOM.Features.CultureMarketplace;

/// <summary>
/// Thin entry point — wires Campaign tick events and delegates to services + adapters.
/// Per ADR-002 (under 150 lines, no inline business logic), the three daily-tick passes
/// live in services:
///   ① <see cref="ICultureMarketplaceMaintenanceService.EnsureGuaranteedStock"/> (cap-bypass)
///   ② <see cref="ICultureMarketplaceMaintenanceService.FilterForeignCultureItems"/> (capped)
///   ③ <see cref="ICultureMarketplaceInjectionService.SelectItems"/> (weighted-random)
/// </summary>
public class CultureMarketplaceBehavior : CampaignBehaviorBase
{
    private readonly ICultureItemPoolService _poolService;
    private readonly ICultureMarketplaceInjectionService _injection;
    private readonly ICultureMarketplaceMaintenanceService _maintenance;
    private readonly ITownRosterAdapter _townAdapter;
    private readonly MarketplaceTuning _tuning;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();

    // Codex review 2026-05-20 (C4): if BuildPools throws, the prior code retried on every
    // daily tick (~200 towns × daily) forever, spamming the log. Stop after 3 attempts.
    private const int MaxPoolBuildAttempts = 3;
    private int _failedAttempts;
    private bool _poolBuilt;
    private bool _gaveUp;

    // Deep-review 2026-05-21 (Data Flow #8): OnNewGameCreatedPartialFollowUpEvent fires for
    // i ∈ [0, 99]. One-shot flag prevents re-running the uncapped initial sweep 98 times.
    private bool _initialSweepDone;

    // Log-hygiene: an owner-less town is a rare/static condition; warn once per settlement
    // rather than on every daily tick.
    private readonly HashSet<string> _warnedNoOwnerCulture = new(StringComparer.OrdinalIgnoreCase);

    public CultureMarketplaceBehavior(
        ICultureItemPoolService poolService,
        ICultureMarketplaceInjectionService injection,
        ICultureMarketplaceMaintenanceService maintenance,
        ITownRosterAdapter townAdapter,
        MarketplaceTuning tuning,
        IModLogger logger)
    {
        _poolService = poolService;
        _injection = injection;
        _maintenance = maintenance;
        _townAdapter = townAdapter;
        _tuning = tuning;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUp);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No SyncData — injected/filtered changes live in vanilla Settlement.ItemRoster
        // which the engine already serializes.
    }

    private void OnNewGameCreated(CampaignGameStarter starter) => EnsurePoolBuilt();
    private void OnGameLoaded(CampaignGameStarter starter) => EnsurePoolBuilt();

    /// <summary>
    /// Vanilla VillageGoodProductionCampaignBehavior.DistributeInitialItemsToTowns runs at
    /// i==1. The cleanup filter runs ONCE at i≥2 (guarded by `_initialSweepDone`); the event
    /// otherwise fires for i ∈ [0, 99] which would otherwise produce 98 redundant invocations.
    /// </summary>
    private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
    {
        if (_initialSweepDone) return;
        if (i < 2) return;
        EnsurePoolBuilt();
        if (!_poolBuilt) return;

        try
        {
            var totalRemoved = 0;
            foreach (var settlement in Campaign.Current?.Settlements ?? throw new InvalidOperationException("Campaign.Current is null in OnNewGameCreatedPartialFollowUp"))
            {
                if (settlement == null || !settlement.IsTown) continue;
                var cultureId = _townAdapter.GetCurrentCultureId(settlement);
                if (string.IsNullOrEmpty(cultureId)) continue;
                totalRemoved += _maintenance.FilterForeignCultureItems(settlement, cultureId, removalCap: int.MaxValue);
            }
            _initialSweepDone = true;
            if (totalRemoved > 0)
                _logger.LogInfo($"[CultureMarketplace] Initial-seed filter swept {totalRemoved} foreign-culture item(s) across all towns");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] Initial-seed filter sweep failed: {ex.Message}");
            _initialSweepDone = true;
        }
    }

    private void EnsurePoolBuilt()
    {
        if (_poolBuilt || _gaveUp) return;
        try
        {
            _poolService.BuildPools();
            _poolBuilt = true;
            _logger.LogInfo($"[CultureMarketplace] Pool ready: {_poolService.CultureCount} cultures, {_poolService.TotalItemCount} items");
        }
        catch (Exception ex)
        {
            _failedAttempts++;
            _logger.LogError($"[CultureMarketplace] Pool build failed (attempt {_failedAttempts}/{MaxPoolBuildAttempts}): {ex.Message}");
            if (_failedAttempts >= MaxPoolBuildAttempts)
            {
                _gaveUp = true;
                _logger.LogError($"[CultureMarketplace] Pool build failed {MaxPoolBuildAttempts} times — feature is inert for the rest of this session");
            }
        }
    }

    private void OnDailyTickSettlement(Settlement settlement)
    {
        if (settlement == null || !settlement.IsTown) return;
        if (_gaveUp) return;
        if (!_poolBuilt) EnsurePoolBuilt();
        if (!_poolBuilt) return;

        var cultureId = _townAdapter.GetCurrentCultureId(settlement);
        if (string.IsNullOrEmpty(cultureId))
        {
            var sid = _townAdapter.GetSettlementId(settlement);
            if (_warnedNoOwnerCulture.Add(sid))
                _logger.LogDebug($"[CultureMarketplace] Skip {sid}: no owner culture");
            return;
        }

        var topUp = _maintenance.EnsureGuaranteedStock(settlement, cultureId);
        var removed = _maintenance.FilterForeignCultureItems(settlement, cultureId, _tuning.MaxFilterRemovalsPerTick);

        var rosterCount = _townAdapter.GetRosterDistinctItemCount(settlement);
        var picks = _injection.SelectItems(cultureId, rosterCount, _rng);
        var added = 0;
        for (var i = 0; i < picks.Count; i++)
        {
            if (_townAdapter.AddItem(settlement, picks[i], 1))
                added++;
        }

        // Log-hygiene: only record a town when this pass INJECTED something. `removed` is
        // deliberately not part of the gate — foreign-item strip is steady-state housekeeping, not
        // an event: vanilla restocks cross-cultural goods every day and the filter strips them
        // again, forever, at a flat ~3.6/town/day. Including it made the gate inert (83% of the
        // 45,080 lines in the 2026-07-26 session log were emitted for `removed` alone, with nothing
        // injected). The count still prints on every line that survives, so no visibility is lost.
        if (added > 0 || topUp > 0)
            _logger.LogDebug($"[CultureMarketplace] {_townAdapter.GetSettlementId(settlement)} ({cultureId}): rosterCount={rosterCount}, picks={picks.Count}, +{added} injected, +{topUp} guaranteed, -{removed} foreign");
    }
}
