using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CareerSystem;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// The lord's gear ladder at a town armoury (#693): the rows <see cref="ArmouryPresenter"/> lists above its
/// upgrades (take up the current rung, hand over its lord's materials, claim its piece) and what choosing one
/// does. Every decision is <see cref="LordsLadderService"/>'s. The armoury has already turned a co-op guest away.
/// </summary>
public sealed class LadderPresenter
{
    private enum Kind { Start, HandIn, Claim }

    private sealed record Row(Kind Kind, LadderStep Step);

    private readonly LordsLadderService _ladder;
    private readonly ICareerQuestService _quests;
    private readonly IArmourGateService _gate;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IModLogger _logger;

    public LadderPresenter(LordsLadderService ladder, ICareerQuestService quests, IArmourGateService gate,
        IArmouryPlayerAdapter player, IArmourAcquisitionConfigProvider config, IModLogger logger)
    {
        _ladder = ladder;
        _quests = quests;
        _gate = gate;
        _player = player;
        _config = config;
        _logger = logger;
    }

    /// <summary>The ladder's rows for an armoury of <paramref name="townLevel"/>; none once every rung is claimed.</summary>
    public IEnumerable<InquiryElement> Rows(int townLevel)
    {
        var heroId = _player.HeroId;
        if (_ladder.CurrentStep(heroId) is not { } step)
            yield break;
        var piece = ArmouryTexts.LadderPiece(step.Slot);
        var lordLevel = _config.GetConfig().LordLevel;

        if (_ladder.IsReady(heroId))
        {
            var canClaim = _ladder.CanClaimAt(townLevel);
            yield return new InquiryElement(new Row(Kind.Claim, step),
                new TextObject("{=taom_lg_claim}Claim your {PIECE}").SetTextVariable("PIECE", piece).ToString(), null, canClaim,
                canClaim ? string.Empty : ArmouryTexts.LadderClaimLevel(lordLevel, piece));
            yield break;
        }

        var handIn = _ladder.HandInStatus();
        var price = handIn is { } status ? ArmouryTexts.Amount(status.Needed, _gate.GetName(status.MaterialId)) : null;
        if (_ladder.CanStart(heroId, LadderQuests.Find(step.QuestId, heroId) != null))
            yield return new InquiryElement(new Row(Kind.Start, step),
                new TextObject("{=taom_lg_take}Take up the commission for your {PIECE}").SetTextVariable("PIECE", piece).ToString(),
                null, true, ArmouryTexts.LadderTakeTip(price));
        if (handIn != null && price != null)
        {
            yield return new InquiryElement(new Row(Kind.HandIn, step),
                new TextObject("{=taom_lg_handin}Hand over {AMOUNT} for your {PIECE}").SetTextVariable("AMOUNT", price)
                    .SetTextVariable("PIECE", piece).ToString(), null, handIn.IsEnough,
                new TextObject("{=taom_lg_handin_tip}You carry {CARRIED} of the {NEEDED} needed.")
                    .SetTextVariable("CARRIED", handIn.Carried).SetTextVariable("NEEDED", handIn.Needed).ToString());
        }
    }

    /// <summary>Acts on a chosen row; false when <paramref name="picked"/> is not one of the ladder's.</summary>
    public bool TryHandle(object? picked)
    {
        if (picked is not Row row)
            return false;
        switch (row.Kind)
        {
            case Kind.Start:
                if (!LadderQuests.Start(_quests, row.Step.QuestId, _logger))
                    Notify(new TextObject("{=taom_lg_start_failed}The armourer cannot take up your commission today.").ToString(), Colors.Red);
                break;
            case Kind.HandIn:
                ConfirmHandIn(row.Step);
                break;
            default:
                OpenClaim(row.Step);
                break;
        }
        return true;
    }

    private void ConfirmHandIn(LadderStep step)
    {
        var piece = ArmouryTexts.LadderPiece(step.Slot);
        var lordLevel = _config.GetConfig().LordLevel;
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_lg_handin_title}Hand over the materials?").ToString(),
            new TextObject("{=taom_lg_handin_body}The armourer takes them, and a master armourer at a level {LORD} armoury will fit you with your {PIECE}.")
                .SetTextVariable("LORD", lordLevel).SetTextVariable("PIECE", piece).ToString(),
            true, true, new TextObject("{=taom_armoury_confirm_yes}Have it done").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            () =>
            {
                if (!_ladder.HandIn())
                {
                    Notify(new TextObject("{=taom_lg_handin_short}You no longer carry the materials.").ToString(), Colors.Red);
                    return;
                }
                // The rung is done: a running quest for it completes too (its completion then changes nothing).
                LadderQuests.Find(step.QuestId, _player.HeroId)?.CompleteQuestWithSuccess();
                _logger.LogInfo($"[ArmourAcquisition] Ladder rung {step.Slot} done by materials for {_player.HeroId}.");
                Notify(new TextObject("{=taom_lg_handin_done}The armourer takes the materials. Your {PIECE} waits at any level {LORD} armoury.")
                    .SetTextVariable("PIECE", piece).SetTextVariable("LORD", lordLevel).ToString(), Colors.Green);
            }, null), pauseGameActiveState: true);
    }

    private void OpenClaim(LadderStep step)
    {
        var piece = ArmouryTexts.LadderPiece(step.Slot);
        var choices = _ladder.RewardChoices(step, _player.CultureId);
        if (choices.Count == 0)
        {
            _logger.LogWarning($"[ArmourAcquisition] No piece for ladder rung {step.Slot}, culture '{_player.CultureId}'.");
            Notify(new TextObject("{=taom_lg_claim_none}The armourer has no {PIECE} for your people yet.").SetTextVariable("PIECE", piece).ToString(), Colors.Red);
            return;
        }
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            new TextObject("{=taom_lg_claim_title}Your {PIECE}").SetTextVariable("PIECE", piece).ToString(),
            new TextObject("{=taom_lg_claim_desc}The master armourer will fit you with one of these. Choose it.").ToString(),
            choices.Select(id => new InquiryElement(id, _gate.GetName(id), null)).ToList(), isExitShown: true, 1, 1,
            new TextObject("{=taom_armoury_choose}Choose").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            selected =>
            {
                if (selected.FirstOrDefault()?.Identifier is not string id || !_ladder.Claim(id))
                    return;
                _logger.LogInfo($"[ArmourAcquisition] Ladder rung {step.Slot} claimed by {_player.HeroId}: {id}.");
                Notify(new TextObject("{=taom_lg_claimed}The master armourer fits you with {ITEM}.").SetTextVariable("ITEM", _gate.GetName(id)).ToString(), Colors.Green);
            }, _ => { }), pauseGameActiveState: true);
    }

    private static void Notify(string text, Color color) => InformationManager.DisplayMessage(new InformationMessage(text, color));
}
