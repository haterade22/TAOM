using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;          // InquiryData, InformationManager (moved here from Core in 1.4.5)
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Quests;

/// <summary>
/// Entry trigger for career tier quests (thin — mirrors TOR's SimpleCareerQuestBehavior). On
/// session launch + daily, if the player's career has an authored quest for the lowest not-yet-done
/// tier and none is already running, it offers the quest via an inquiry; accepting starts a
/// <see cref="CareerQuest"/>. A declined quest is remembered (flat-dict SyncData) so it is not
/// re-offered. Careers/tiers with no authored quest are simply skipped (level gate handles them).
/// </summary>
public class CareerQuestCampaignBehavior : CampaignBehaviorBase
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerQuestService _questService;
    private readonly IModLogger _logger;

    // heroStringId -> csv of declined quest-def ids (persisted, flat-dict pattern).
    private Dictionary<string, string> _declined = new Dictionary<string, string>();

    // In-memory: an offer inquiry is currently open — don't stack another.
    private bool _offerPending;

    public CareerQuestCampaignBehavior(ICareerDataService dataService, ICareerQuestService questService, IModLogger logger)
    {
        _dataService = dataService;
        _questService = questService;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_taom_cq_declined", ref _declined);
        if (_declined == null) _declined = new Dictionary<string, string>();
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => TryOfferNext();
    private void OnDailyTick() => TryOfferNext();

    private void TryOfferNext()
    {
        if (_offerPending) return;
        var hero = Hero.MainHero;
        if (hero == null) return;

        var careerId = _dataService.GetCareerStringId(hero.StringId);
        if (string.IsNullOrEmpty(careerId)) return;

        if (AnyActiveCareerQuest()) return;   // one career quest at a time

        for (int tier = 1; tier <= 3; tier++)
        {
            if (_dataService.IsTierUnlocked(hero.StringId, tier)) continue;   // quest already completed this tier
            var def = _questService.GetQuestFor(careerId, tier);
            if (def == null) continue;                                        // no authored quest — level gate handles it
            if (IsDeclined(hero.StringId, def.Id)) continue;                  // player passed on it
            Offer(def, hero);
            return;
        }
    }

    private void Offer(CareerQuestDefinition def, Hero hero)
    {
        _offerPending = true;
        var title = Text(def.TitleKey, "{=taom_cq_title}Career Quest");
        var body = Text(def.DescriptionKey, "{=taom_cq_body}Prove yourself.");
        var accept = new TextObject("{=taom_cq_accept}Accept").ToString();
        var decline = new TextObject("{=taom_cq_decline}Not now").ToString();

        try
        {
            InformationManager.ShowInquiry(new InquiryData(
                title.ToString(), body.ToString(), true, true, accept, decline,
                affirmativeAction: () => OnAccept(def, hero),
                negativeAction: () => OnDecline(def, hero)),
                pauseGameActiveState: false);
        }
        catch (Exception ex)
        {
            _offerPending = false;   // Codex F2 — don't get stuck if the inquiry fails to open before a callback
            _logger.LogError($"CareerQuest: failed to show offer for '{def.Id}': {ex.Message}");
        }
    }

    private void OnAccept(CareerQuestDefinition def, Hero hero)
    {
        _offerPending = false;
        try
        {
            var quest = new CareerQuest("taom_cq_" + def.Id, hero, def);
            quest.StartQuest();
            _logger.LogInfo($"CareerQuest: started '{def.Id}' (tier {def.Tier}) for {hero.Name}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerQuest: failed to start '{def.Id}': {ex.Message}");
        }
    }

    private void OnDecline(CareerQuestDefinition def, Hero hero)
    {
        _offerPending = false;
        MarkDeclined(hero.StringId, def.Id);
        _logger.LogInfo($"CareerQuest: '{def.Id}' declined by {hero.Name}");
    }

    private bool AnyActiveCareerQuest()
    {
        var qm = Campaign.Current?.QuestManager;
        if (qm == null) return false;
        foreach (var q in qm.Quests)
        {
            if (q is CareerQuest cq && cq.IsOngoing) return true;
        }
        return false;
    }

    private bool IsDeclined(string heroId, string defId)
    {
        return _declined.TryGetValue(heroId, out var csv)
            && ("," + csv + ",").IndexOf("," + defId + ",", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void MarkDeclined(string heroId, string defId)
    {
        if (IsDeclined(heroId, defId)) return;
        _declined[heroId] = _declined.TryGetValue(heroId, out var csv) && !string.IsNullOrEmpty(csv)
            ? csv + "," + defId
            : defId;
    }

    private static TextObject Text(string key, string fallback)
        => new TextObject(string.IsNullOrEmpty(key) ? fallback : key);
}
