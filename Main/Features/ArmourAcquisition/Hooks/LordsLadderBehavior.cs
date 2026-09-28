using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.Quests;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Thin boundary for the lord's gear ladder (#693, ADR-002): hands each battle's kills (counted by
/// <see cref="HeroKillCounterMissionLogic"/>) to the running rung's quest, rolls the lord's material find after
/// a battle won, and readies a rung when its quest completes. Every decision is <see cref="LordsLadderService"/>'s;
/// the armoury's ladder rows (<see cref="LadderPresenter"/>) start rungs, take materials and hand out the pieces.
/// Main hero, authority only.
/// </summary>
public sealed class LordsLadderBehavior : CampaignBehaviorBase
{
    private readonly LordsLadderService _ladder;
    private readonly HeroKillTally _tally;
    private readonly IArmourGateService _gate;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;
    private readonly Random _rng = new();

    public LordsLadderBehavior(LordsLadderService ladder, HeroKillTally tally, IArmourGateService gate, IArmouryPlayerAdapter player,
        IArmourAcquisitionConfigProvider config, ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _ladder = ladder;
        _tally = tally;
        _gate = gate;
        _player = player;
        _config = config;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    private bool Ready => _gate.IsActive && !_coop.ShouldDeferToHost && !_server.IsDedicatedServer;

    public override void RegisterEvents()
    {
        // The tally is a process singleton: a campaign starts with none of the last one's kills.
        _tally.Take();
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnQuestCompletedEvent.AddNonSerializedListener(this, OnQuestCompleted);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Fires before the map event commits its loot (PlayerEncounter.DoApplyMapEventResults), so a find goes
    // straight into the party inventory, not onto the loot screen.
    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        var kills = _tally.Take();
        if (mapEvent == null || !Ready)
            return;
        CreditKills(kills);
        var won = mapEvent.WinningSide != BattleSideEnum.None && mapEvent.WinningSide == mapEvent.PlayerSide;
        if (_ladder.RollMaterialDrop(_player.HeroId, won, kills, _player.CultureId, _rng) is not { } find
            || !_player.AddPiece(find.ItemId, null, find.Units))
            return;
        _logger.LogInfo($"[ArmourAcquisition] Lord's material found: {find.Units} x {find.ItemId} ({kills} kill(s) in the battle).");
        InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=taom_lg_drop}Among the spoils you find {AMOUNT}.")
            .SetTextVariable("AMOUNT", ArmouryTexts.Amount(find.Units, _gate.GetName(find.ItemId))).ToString(), Colors.Green));
    }

    // A battle the hero left before it ended raises no battle end for the player; its kills still count.
    private void OnHourlyTick()
    {
        var kills = _tally.Take();
        if (kills > 0 && Ready)
            CreditKills(kills);
    }

    private void CreditKills(int kills)
    {
        if (kills <= 0 || _ladder.QuestToCredit(_player.HeroId) is not { } questId)
            return;
        var quest = LadderQuests.Find(questId, _player.HeroId);
        if (quest == null)
            return;
        quest.AddProgress(CareerQuestObjectiveType.HeroKills, kills);
        _logger.LogInfo($"[ArmourAcquisition] {kills} kill(s) credited to {questId}.");
    }

    private void OnQuestCompleted(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (quest is not CareerQuest careerQuest)
            return;
        var owner = careerQuest.OwnerHeroStringId;
        var step = _ladder.OnQuestEnded(owner, careerQuest.CareerQuestDefId, detail == QuestBase.QuestCompleteDetails.Success);
        if (step == null)
            return;
        _logger.LogInfo($"[ArmourAcquisition] Ladder rung {step.Slot} is ready to claim for {owner}.");
        // The rung is readied even with the switch off (it must survive a toggle); the armoury it points to is not shown then.
        if (owner != _player.HeroId || _server.IsDedicatedServer || !_gate.IsActive)
            return;
        InformationManager.DisplayMessage(new InformationMessage(new TextObject(
                "{=taom_lg_ready}Your deeds are known. A master armourer, at a level {LORD} armoury, will now fit you with your {PIECE}.")
            .SetTextVariable("LORD", _config.GetConfig().LordLevel)
            .SetTextVariable("PIECE", ArmouryTexts.LadderPiece(step.Slot)).ToString(), Colors.Green));
    }
}
