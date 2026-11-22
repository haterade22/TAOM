using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// The lord's gear ladder at a co-op guest's armoury (docs/features/armour-acquisition.md, item 8): the rows
/// <see cref="ArmouryPresenter"/> lists from the co-op server's view of the guest's rung (<see cref="GuestLadderRows"/>),
/// and the hand-in or claim a row sends to the server. The server decides and changes the party; this only shows.
/// </summary>
public sealed class GuestLadderPresenter
{
    private readonly GuestTradeClient _guestTrade;
    private readonly IArmourGateService _gate;
    private readonly IModLogger _logger;

    public GuestLadderPresenter(GuestTradeClient guestTrade, IArmourGateService gate, IModLogger logger)
    {
        _guestTrade = guestTrade;
        _gate = gate;
        _logger = logger;
    }

    public IEnumerable<InquiryElement> Rows(GuestLadderView? view)
    {
        foreach (var row in GuestLadderRows.Plan(view))
        {
            var piece = ArmouryTexts.LadderPiece(row.View.Slot);
            if (row.Kind == GuestLadderRowKind.Claim)
            {
                yield return new InquiryElement(row,
                    new TextObject("{=taom_lg_claim}Claim your {PIECE}").SetTextVariable("PIECE", piece).ToString(), null, row.Enabled,
                    row.Enabled ? string.Empty : ArmouryTexts.LadderClaimLevel(row.View.LordLevel, piece));
                continue;
            }
            var amount = ArmouryTexts.Amount(row.View.Needed, _gate.GetName(row.View.MaterialId!));
            yield return new InquiryElement(row,
                new TextObject("{=taom_lg_handin}Hand over {AMOUNT} for your {PIECE}").SetTextVariable("AMOUNT", amount)
                    .SetTextVariable("PIECE", piece).ToString(), null, row.Enabled,
                new TextObject("{=taom_lg_handin_tip}You carry {CARRIED} of the {NEEDED} needed.")
                    .SetTextVariable("CARRIED", row.View.Carried).SetTextVariable("NEEDED", row.View.Needed).ToString());
        }
    }

    /// <summary>Acts on a chosen row; false when <paramref name="picked"/> is not one of these rows.</summary>
    public bool TryHandle(object? picked, string townId)
    {
        if (picked is not GuestLadderRow row)
            return false;
        if (row.Kind == GuestLadderRowKind.HandIn)
            ConfirmHandIn(row.View, townId);
        else
            OpenClaim(row.View, townId);
        return true;
    }

    private void ConfirmHandIn(GuestLadderView view, string townId)
    {
        var piece = ArmouryTexts.LadderPiece(view.Slot);
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_lg_handin_title}Hand over the materials?").ToString(),
            new TextObject("{=taom_lg_handin_body}The armourer takes them, and a master armourer at a level {LORD} armoury will fit you with your {PIECE}.")
                .SetTextVariable("LORD", view.LordLevel).SetTextVariable("PIECE", piece).ToString(),
            true, true, new TextObject("{=taom_armoury_confirm_yes}Have it done").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            () => AskHost(new GuestTradeRequest(GuestTradeAction.LadderHandIn, townId, view.Slot.ToString()), view, null), null),
            pauseGameActiveState: true);
    }

    private void OpenClaim(GuestLadderView view, string townId)
    {
        var piece = ArmouryTexts.LadderPiece(view.Slot);
        if (view.Choices.Count == 0)
        {
            Notify(new TextObject("{=taom_lg_claim_none}The armourer has no {PIECE} for your people yet.").SetTextVariable("PIECE", piece).ToString(), Colors.Red);
            return;
        }
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            new TextObject("{=taom_lg_claim_title}Your {PIECE}").SetTextVariable("PIECE", piece).ToString(),
            new TextObject("{=taom_lg_claim_desc}The master armourer will fit you with one of these. Choose it.").ToString(),
            view.Choices.Select(id => new InquiryElement(id, _gate.GetName(id), null)).ToList(), isExitShown: true, 1, 1,
            new TextObject("{=taom_armoury_choose}Choose").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            selected =>
            {
                if (selected.FirstOrDefault()?.Identifier is string id)
                    AskHost(new GuestTradeRequest(GuestTradeAction.LadderClaim, townId, view.Slot.ToString(), null, id), view, id);
            }, _ => { }), pauseGameActiveState: true);
    }

    private void AskHost(GuestTradeRequest request, GuestLadderView view, string? itemId) =>
        GuestTradeTexts.AskHost(_guestTrade, request, reply => ShowOutcome(view, itemId, (GuestLadderOutcome)reply.Status),
            text => Notify(text, Colors.Red));

    private void ShowOutcome(GuestLadderView view, string? itemId, GuestLadderOutcome outcome)
    {
        _logger.LogInfo($"[ArmourAcquisition] Co-op ladder {(itemId == null ? "hand-in" : "claim")} for rung {view.Slot}: {outcome}.");
        var piece = ArmouryTexts.LadderPiece(view.Slot);
        var text = outcome switch
        {
            GuestLadderOutcome.Done when itemId == null => new TextObject("{=taom_lg_handin_done}The armourer takes the materials. Your {PIECE} waits at any level {LORD} armoury.")
                .SetTextVariable("PIECE", piece).SetTextVariable("LORD", view.LordLevel).ToString(),
            GuestLadderOutcome.Done => new TextObject("{=taom_lg_claimed}The master armourer fits you with {ITEM}.").SetTextVariable("ITEM", _gate.GetName(itemId!)).ToString(),
            GuestLadderOutcome.NotEnough => new TextObject("{=taom_lg_handin_short}You no longer carry the materials.").ToString(),
            GuestLadderOutcome.ArmouryLevel => ArmouryTexts.LadderClaimLevel(view.LordLevel, piece),
            GuestLadderOutcome.RungChanged => new TextObject("{=taom_lg_coop_changed}Your lord's gear has changed since you looked. Visit the armoury again.").ToString(),
            GuestLadderOutcome.NotAChoice => new TextObject("{=taom_lg_coop_not_choice}The armourer cannot fit that piece.").ToString(),
            GuestLadderOutcome.Off => new TextObject("{=taom_lg_coop_off}The co-op server has turned off the Lords' Ladder for co-op players.").ToString(),
            _ => GuestTradeTexts.Refusal(GuestTradeRefusal.HostFailed),
        };
        Notify(text, outcome == GuestLadderOutcome.Done ? Colors.Green : Colors.Red);
    }

    private static void Notify(string text, Color color) => InformationManager.DisplayMessage(new InformationMessage(text, color));
}
