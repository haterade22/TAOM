using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Quests;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Thin boundary for "The Lord's Harness" quest (ADR-002): offers it on entering an eligible town and starts
/// it as a CareerQuest (the career-quest shell tracks and saves it); when it completes, the quest's owner
/// may claim a lord's harness at an armoury. Every decision is <see cref="LordHarnessService"/>'s.
/// Main hero, authority only.
/// </summary>
public sealed class LordHarnessQuestBehavior : CampaignBehaviorBase
{
    private readonly LordHarnessService _harness;
    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;
    private readonly IArmouryTownAdapter _towns;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ICareerQuestService _quests;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private bool _offerOpen;

    public LordHarnessQuestBehavior(LordHarnessService harness, IArmourGateService gate, ArmouryLevelService levels,
        IArmouryTownAdapter towns, IArmouryPlayerAdapter player, ICareerQuestService quests,
        IArmourAcquisitionConfigProvider config, ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _harness = harness;
        _gate = gate;
        _levels = levels;
        _towns = towns;
        _player = player;
        _quests = quests;
        _config = config;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    private bool Ready => _gate.IsActive && !_coop.ShouldDeferToHost && !_server.IsDedicatedServer;

    public override void RegisterEvents()
    {
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, OnQuestCompleted);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        // Raised for every party's entry into any settlement: the cheapest rejections first.
        if (party != MobileParty.MainParty || settlement == null || _offerOpen || !settlement.IsTown || !Ready)
            return;
        var heroId = _player.HeroId;
        var townId = settlement.StringId;
        var today = _towns.Today;
        if (!_harness.ShouldOffer(heroId, today, _levels.GetTownLevel(townId),
                _towns.GetCultureId(townId) == _player.CultureId, IsHarnessQuestRunning()))
            return;

        _offerOpen = true;
        try
        {
            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_lh_offer_title}The Lord's Harness").ToString(),
                new TextObject("{=taom_lh_offer_body}The master armourer of {TOWN} eyes your elite harness. \"That is a soldier's armour. A lord's is forged for deeds, not coin. Show me yours, and I will fit you as a lord of our people.\"")
                    .SetTextVariable("TOWN", _towns.GetName(townId)).ToString(),
                true, true, new TextObject("{=taom_lh_offer_accept}Accept the test").ToString(), new TextObject("{=taom_lh_offer_decline}Not now").ToString(),
                () => { _offerOpen = false; Start(heroId); },
                () => { _offerOpen = false; _harness.Decline(heroId, today); }), pauseGameActiveState: true);
        }
        catch (Exception ex)
        {
            // A throwing inquiry would otherwise leave the latch set and no offer ever again this game.
            _offerOpen = false;
            _logger.LogError($"[ArmourAcquisition] The Lord's Harness offer could not be shown ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    private void Start(string heroId)
    {
        var def = _quests.GetQuestById(LordHarnessService.QuestId);
        if (def == null || Hero.MainHero == null)
        {
            _logger.LogError($"[ArmourAcquisition] The Lord's Harness has no career-quest definition '{LordHarnessService.QuestId}'.");
            return;
        }
        try
        {
            new CareerQuest("taom_cq_" + def.Id, Hero.MainHero, def).StartQuest();
            _harness.OnAccepted(heroId);
            _logger.LogInfo($"[ArmourAcquisition] The Lord's Harness started for {heroId}.");
        }
        catch (Exception ex)
        {
            // Nothing is recorded before the quest starts, so the offer simply comes again.
            _logger.LogError($"[ArmourAcquisition] The Lord's Harness could not start ({ex.GetType().Name}: {ex.Message}); it will be offered again.");
        }
    }

    private void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (quest is not CareerQuest careerQuest || careerQuest.CareerQuestDefId != LordHarnessService.QuestId)
            return;
        var owner = careerQuest.OwnerHeroStringId;
        if (!_harness.OnQuestEnded(owner, detail == QuestBase.QuestCompleteDetails.Success))
            return;
        _logger.LogInfo($"[ArmourAcquisition] The Lord's Harness is ready to claim for {owner}.");
        if (owner != _player.HeroId || _server.IsDedicatedServer)
            return;
        InformationManager.DisplayMessage(new InformationMessage(new TextObject(
                "{=taom_lh_ready}Your deeds are known. Any master armourer, at a level {LORD} armoury, will now fit you with your lord's harness.")
            .SetTextVariable("LORD", _config.GetConfig().LordLevel).ToString(), Colors.Green));
    }

    /// <summary>Any running harness quest, whoever's: a second would share the first's quest id.</summary>
    private static bool IsHarnessQuestRunning() =>
        Campaign.Current?.QuestManager?.Quests?.Any(q => q is CareerQuest careerQuest && careerQuest.IsOngoing
            && careerQuest.CareerQuestDefId == LordHarnessService.QuestId) == true;
}
