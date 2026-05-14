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
    private static readonly KingdomSiegeMessages DefaultMessages = new KingdomSiegeMessages
    {
        Title = "{attacker} is besieging {settlement}!",
        Body = "Will you answer the call to defend? You have {days} days to reach the settlement.",
        AcceptButton = "Help Defend",
        AcceptMessage = "You have pledged to defend {settlement}. Ride now!",
        RewardMessage = "You answered the call! +{influence} influence, +{relation} relation."
    };

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

    internal KingdomSiegeMessages GetMessages(string factionId)
    {
        if (!string.IsNullOrEmpty(factionId) &&
            _config.KingdomMessages.TryGetValue(factionId, out var messages))
            return messages;
        return DefaultMessages;
    }

    private static string Resolve(string template, string settlement, string attacker,
        int days, int influence, int relation)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return template
            .Replace("{settlement}", settlement)
            .Replace("{attacker}", attacker)
            .Replace("{days}", days.ToString())
            .Replace("{influence}", influence.ToString())
            .Replace("{relation}", relation.ToString());
    }

    public void OnSiegeStarted(ISiegeEventAdapter siege)
    {
        if (!IsWatchedSiege(siege))
            return;

        // Phase 9b #132 — was a silent try/catch that assigned default(CampaignTime) (= campaign
        // epoch, instantly past) on ANY exception, guaranteeing the event self-destructed on the
        // next hourly tick before the player could respond. Replaced with logged catch + Never
        // fallback: if DaysFromNow throws (no Campaign.Current — only realistic in test envs), the
        // event persists until OnSiegeEnded fires rather than vanishing on the next tick. That's
        // a strictly better failure mode than the prior silent self-destruct.
        CampaignTime deadline;
        try
        {
            deadline = CampaignTime.DaysFromNow(_settings.SiegeDefenseResponseDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[SiegeDefense] CampaignTime.DaysFromNow failed (test env or pre-campaign): {ex.Message}");
            deadline = CampaignTime.Never;
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
            var msgs = GetMessages(siege.DefenderFactionId);

            var title = Resolve(msgs.Title, settlementName, attackerName, days, 0, 0);
            var body = Resolve(msgs.Body, settlementName, attackerName, days, 0, 0);
            var acceptMsg = Resolve(msgs.AcceptMessage, settlementName, attackerName, days, 0, 0);

            InformationManager.ShowInquiry(new InquiryData(
                titleText: title,
                text: body,
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: msgs.AcceptButton,
                negativeText: "Ignore",
                affirmativeAction: () =>
                {
                    evt.PlayerAccepted = true;
                    TrackSettlement(evt.SettlementId);
                    InformationManager.DisplayMessage(new InformationMessage(acceptMsg));
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

    public void Reset()
    {
        // Phase 9b #132 R1 — clear singleton state on new campaign start (same process).
        // VisualTracker registrations are scoped to Campaign.Current and are released when the
        // campaign tears down; no untrack pass needed here.
        if (_activeEvents.Count > 0)
            _logger.LogInfo($"[SiegeDefense] Reset clearing {_activeEvents.Count} stale events from prior campaign");
        _activeEvents.Clear();
    }

    public Dictionary<string, string> SnapshotForSave()
    {
        var snapshot = new Dictionary<string, string>();
        foreach (var kvp in _activeEvents)
        {
            var evt = kvp.Value;
            // Store remaining hours from save-time Now (CampaignTime's internal _numTicks is not
            // accessible — ToDays exists but loses precision for sub-day deadlines).
            // Format: defenderFactionId|remainingHours|accepted|rewardClaimed
            float remainingHours = evt.Deadline.RemainingHoursFromNow;
            snapshot[kvp.Key] = $"{evt.DefenderFactionId}|{remainingHours.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{(evt.PlayerAccepted ? 1 : 0)}|{(evt.RewardClaimed ? 1 : 0)}";
        }
        return snapshot;
    }

    public void RestoreFromSave(Dictionary<string, string> snapshot)
    {
        _activeEvents.Clear();
        if (snapshot == null) return;

        foreach (var kvp in snapshot)
        {
            var parts = kvp.Value?.Split('|');
            if (parts == null || parts.Length < 4) continue;

            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var remainingHours)) continue;
            var accepted = parts[2] == "1";
            var rewardClaimed = parts[3] == "1";

            // Same fallback strategy as OnSiegeStarted: if HoursFromNow throws (no Campaign.Current),
            // use Never rather than default(=campaign-zero=instantly-past).
            CampaignTime deadline;
            try { deadline = CampaignTime.HoursFromNow(remainingHours); }
            catch { deadline = CampaignTime.Never; }

            var evt = new ActiveSiegeDefenseEvent
            {
                SettlementId = kvp.Key,
                DefenderFactionId = parts[0] ?? "",
                Deadline = deadline,
                PlayerAccepted = accepted,
                RewardClaimed = rewardClaimed
            };
            _activeEvents[kvp.Key] = evt;

            // Re-register VisualTracker for pending events the player accepted but hasn't claimed yet.
            if (accepted && !rewardClaimed)
                TrackSettlement(kvp.Key);
        }
        _logger.LogInfo($"[SiegeDefense] Restored {_activeEvents.Count} active siege events from save");
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

        var msgs = GetMessages(evt.DefenderFactionId);
        var rewardMsg = Resolve(msgs.RewardMessage, evt.SettlementId, "", 0,
            _config.RewardInfluence, _config.RewardRelation);
        InformationManager.DisplayMessage(new InformationMessage(rewardMsg, Color.FromUint(0xFF00FF00)));

        _logger.LogInfo($"[SiegeDefense] Reward granted for defending {evt.SettlementId}: +{_config.RewardInfluence} influence, +{_config.RewardRelation} relation");
    }
}
