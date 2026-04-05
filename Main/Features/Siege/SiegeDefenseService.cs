using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Siege.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.Siege;

public class SiegeDefenseService : ISiegeDefenseService
{
    private readonly ISiegeDefenseSettingsProvider _settings;
    private readonly IPlayerContextAdapter _playerContext;
    private readonly IModLogger _logger;
    private readonly SiegeDefenseConfig _config;
    private readonly Dictionary<string, ActiveSiegeDefenseEvent> _activeEvents = new Dictionary<string, ActiveSiegeDefenseEvent>();

    public IReadOnlyDictionary<string, ActiveSiegeDefenseEvent> ActiveEvents => _activeEvents;

    public SiegeDefenseService(
        ISiegeDefenseConfigProvider configProvider,
        ISiegeDefenseSettingsProvider settings,
        IPlayerContextAdapter playerContext,
        IModLogger logger)
    {
        _settings = settings;
        _playerContext = playerContext;
        _logger = logger;
        _config = configProvider.LoadConfig();
    }

    public bool IsWatchedSiege(ISiegeEventAdapter siege)
    {
        if (!_settings.EnableSiegeDefenseEvents)
            return false;

        if (!siege.IsTown)
            return false;

        if (_activeEvents.ContainsKey(siege.SettlementId))
            return false;

        var playerKingdomId = _playerContext.GetPlayerKingdomId();
        if (!string.IsNullOrEmpty(playerKingdomId) && siege.DefenderFactionId == playerKingdomId)
            return true;

        if (_config.WatchedSettlementIds.Contains(siege.SettlementId))
            return true;

        return false;
    }

    public void OnSiegeStarted(ISiegeEventAdapter siege)
    {
        if (!IsWatchedSiege(siege))
            return;

        CampaignTime deadline;
        try
        {
            deadline = CampaignTime.DaysFromNow(_settings.SiegeDefenseResponseDays);
        }
        catch
        {
            deadline = default;
        }

        var evt = new ActiveSiegeDefenseEvent
        {
            SettlementId = siege.SettlementId,
            DefenderFactionId = siege.DefenderFactionId,
            Deadline = deadline,
            PlayerAccepted = false,
            RewardClaimed = false
        };

        _activeEvents[siege.SettlementId] = evt;
        _logger.LogInfo($"[SiegeDefense] Tracked siege at {siege.SettlementId} — deadline in {_settings.SiegeDefenseResponseDays} days");

        try
        {
            var settlementName = siege.SettlementName;
            var attackerName = siege.AttackerName;
            var days = _settings.SiegeDefenseResponseDays;

            InformationManager.ShowInquiry(new InquiryData(
                titleText: $"{attackerName} is besieging {settlementName}!",
                text: $"Will you answer the call to defend? You have {days} days to reach the settlement.",
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: "Help Defend",
                negativeText: "Ignore",
                affirmativeAction: () =>
                {
                    evt.PlayerAccepted = true;
                    TrackSettlement(evt.SettlementId);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You have pledged to defend {settlementName}. Ride now!"));
                    _logger.LogInfo($"[SiegeDefense] Player accepted defense of {evt.SettlementId}");
                },
                negativeAction: () => { }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[SiegeDefense] ShowInquiry unavailable outside game: {ex.Message}");
        }
    }

    public void OnHourlyTick()
    {
        var expiredKeys = _activeEvents
            .Where(kvp => kvp.Value.Deadline.IsPast && !kvp.Value.RewardClaimed)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _logger.LogInfo($"[SiegeDefense] Event for {key} expired");
            if (_activeEvents[key].PlayerAccepted)
                UntrackSettlement(key);
            _activeEvents.Remove(key);
        }

        var playerSettlementId = MobileParty.MainParty?.CurrentSettlement?.StringId ?? "";
        if (string.IsNullOrEmpty(playerSettlementId))
            return;

        foreach (var evt in _activeEvents.Values
            .Where(e => e.PlayerAccepted && !e.RewardClaimed && !e.Deadline.IsPast)
            .ToList())
        {
            if (evt.SettlementId != playerSettlementId)
                continue;

            var settlement = Settlement.Find(evt.SettlementId);
            if (settlement?.SiegeEvent == null)
                continue;

            GrantReward(evt);
        }
    }

    public void OnSiegeEnded(string settlementId)
    {
        if (_activeEvents.TryGetValue(settlementId, out var evt))
        {
            _logger.LogInfo($"[SiegeDefense] Siege ended for {settlementId}, removing event");
            if (evt.PlayerAccepted)
                UntrackSettlement(settlementId);
            _activeEvents.Remove(settlementId);
        }
    }

    private void TrackSettlement(string settlementId)
    {
        try
        {
            var settlement = Settlement.Find(settlementId);
            if (settlement != null)
                Campaign.Current.VisualTrackerManager.RegisterObject(settlement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[SiegeDefense] TrackSettlement failed for {settlementId}: {ex.Message}");
        }
    }

    private void UntrackSettlement(string settlementId)
    {
        try
        {
            var settlement = Settlement.Find(settlementId);
            if (settlement != null)
                Campaign.Current.VisualTrackerManager.RemoveTrackedObject(settlement, forceRemove: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[SiegeDefense] UntrackSettlement failed for {settlementId}: {ex.Message}");
        }
    }

    private void GrantReward(ActiveSiegeDefenseEvent evt)
    {
        evt.RewardClaimed = true;
        UntrackSettlement(evt.SettlementId);

        Hero.MainHero.Clan.Influence += _config.RewardInfluence;

        var defender = Kingdom.All.FirstOrDefault(k => k.StringId == evt.DefenderFactionId);
        if (defender?.Leader != null && defender.Leader != Hero.MainHero)
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                Hero.MainHero, defender.Leader, _config.RewardRelation);
        }

        var defenderName = defender?.Name?.ToString() ?? "defenders";
        InformationManager.DisplayMessage(new InformationMessage(
            $"[TAOM] You answered the call! +{_config.RewardInfluence} influence, +{_config.RewardRelation} relation with {defenderName}.",
            Color.FromUint(0xFF00FF00)));

        _logger.LogInfo($"[SiegeDefense] Reward granted for defending {evt.SettlementId}: +{_config.RewardInfluence} influence, +{_config.RewardRelation} relation");
    }
}
