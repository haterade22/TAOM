using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CultureMarketplace;

/// <summary>
/// Thin entry point — wires Campaign tick events and delegates to the injection service.
/// Per ADR-002, no business logic here.
/// </summary>
public class CultureMarketplaceBehavior : CampaignBehaviorBase
{
    private readonly ICultureItemPoolService _poolService;
    private readonly ICultureMarketplaceInjectionService _injection;
    private readonly ITownRosterAdapter _townAdapter;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();

    // Codex review 2026-05-20 (C4): if BuildPools throws, the prior code retried on every
    // daily tick (~200 towns × daily) forever, spamming the log. Stop after 3 attempts
    // and treat the feature as inert for the rest of the session.
    private const int MaxPoolBuildAttempts = 3;
    private int _failedAttempts;
    private bool _poolBuilt;
    private bool _gaveUp;

    public CultureMarketplaceBehavior(
        ICultureItemPoolService poolService,
        ICultureMarketplaceInjectionService injection,
        ITownRosterAdapter townAdapter,
        IModLogger logger)
    {
        _poolService = poolService;
        _injection = injection;
        _townAdapter = townAdapter;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No SyncData — injected items live in vanilla Settlement.ItemRoster which the engine already serializes.
    }

    private void OnNewGameCreated(CampaignGameStarter starter) => EnsurePoolBuilt();
    private void OnGameLoaded(CampaignGameStarter starter) => EnsurePoolBuilt();

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
            _logger.LogDebug($"[CultureMarketplace] Skip {_townAdapter.GetSettlementId(settlement)}: no owner culture");
            return;
        }

        var rosterCount = _townAdapter.GetRosterDistinctItemCount(settlement);
        var picks = _injection.SelectItems(cultureId, rosterCount, _rng);
        if (picks.Count == 0) return;

        var added = 0;
        for (var i = 0; i < picks.Count; i++)
        {
            if (_townAdapter.AddItem(settlement, picks[i], 1))
                added++;
        }

        if (added > 0)
            _logger.LogDebug($"[CultureMarketplace] {_townAdapter.GetSettlementId(settlement)} ({cultureId}): +{added} items");
    }
}
