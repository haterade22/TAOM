using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Entry-point-layer presenter for the wait menu's text and the service-status inquiry.
/// Exists so the menu behavior stays under the ADR-002 ceiling and so NOTHING allocates
/// per menu tick: the wait TextObject is built once per menu init (commander names don't
/// change mid-service), never per frame.
/// </summary>
public interface IEnlistmentWaitMenuPresenter
{
    /// <summary>Resolve the commander name once and push the wait text. Called from menu init only.</summary>
    void RefreshWaitText();

    void ShowServiceStatus();

    /// <summary>
    /// Ask to be released. Owns the whole decision — verdict, the popup that states the cost,
    /// and the discharge itself — so the menu behaviour stays a registration shell.
    /// </summary>
    void RequestRelease(double nowDays);
}

public sealed class EnlistmentWaitMenuPresenter : IEnlistmentWaitMenuPresenter
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentService _service;
    private readonly IInquiryAdapter _inquiry;
    private readonly ICoopSessionProvider _coopSession;

    private readonly TextObject _waitText = new TextObject(
        "{=taom_enlist_wait_text}You serve in {COMMANDER}'s company. The column moves at your commander's pace.");

    public EnlistmentWaitMenuPresenter(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IEnlistmentDialogGateService gate,
        IEnlistmentService service,
        IInquiryAdapter inquiry,
        ICoopSessionProvider coopSession)
    {
        _store = store;
        _commander = commander;
        _gate = gate;
        _service = service;
        _inquiry = inquiry;
        _coopSession = coopSession;
    }

    public void RefreshWaitText()
    {
        var commanderName = _commander.GetSnapshot(_store.Record.CommanderHeroId).Name ?? "";
        _waitText.SetTextVariable("COMMANDER", commanderName);
        MBTextManager.SetTextVariable("TAOM_ENLISTMENT_WAIT_TEXT", _waitText);
    }

    public void ShowServiceStatus()
    {
        var record = _store.Record;
        var commanderName = _commander.GetSnapshot(record.CommanderHeroId).Name ?? "";
        var body = new TextObject(
            "{=taom_enlist_status_body}Commander: {COMMANDER}. Enlisted on day {ENLISTED_DAY}; contract ends on day {CONTRACT_END}.");
        body.SetTextVariable("COMMANDER", commanderName);
        body.SetTextVariable("ENLISTED_DAY", ((int)(record.EnlistedAtDay ?? 0)).ToString());
        body.SetTextVariable("CONTRACT_END", ((int)(record.ContractEndDay ?? 0)).ToString());
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_enlist_status_title}Service record").ToString(),
            body.ToString(),
            true, false,
            new TextObject("{=taom_enlist_ok}Understood").ToString(), null,
            null, null));
    }

    public void RequestRelease(double nowDays)
    {
        // CO-OP: host-only, like every other discharge path. A client running this locally would
        // restore its own presence and clear its record while the host stayed enlisted.
        if (!_coopSession.IsAuthority)
            return;

        var request = _gate.EvaluateReleaseRequest(nowDays);
        switch (request.Verdict)
        {
            case ReleaseVerdict.RefusedInBattle:
                // A toast, not a popup: this is "not this second", and a modal mid-battle is
                // itself an interruption the player did not ask for.
                _inquiry.ShowMessage(
                    "taom_enlist_release_in_battle",
                    "There is fighting to be done. Ask again once the field is quiet.",
                    null, null);
                return;

            case ReleaseVerdict.RefusedTooSoon:
                PromptDesertion(request.DaysOwed);
                return;

            default:
                // Term served — an honourable release. DischargeService owns the menu exit and
                // the settlement hand-back (INV-D1), so there is nothing to do here afterwards.
                _service.RequestDischarge(DischargeReason.PlayerRequest);
                return;
        }
    }

    /// <summary>
    /// State the cost in the popup itself, with the real number of days. The player has to be
    /// able to make this decision from the text in front of them — a bare "are you sure?" is
    /// how someone forfeits their arrears without ever knowing they had any.
    /// </summary>
    private void PromptDesertion(int daysOwed)
    {
        _inquiry.ShowTwoOptionInquiry(
            "taom_enlist_release_refused_title", "Your term is not served",
            "taom_enlist_release_refused_body",
            "Your commander holds you to your oath — {DAYS} more days are owed. Leaving now is desertion: you forfeit the pay still owed to you, and your commander will not forget it.",
            "taom_enlist_release_desert", "Desert the company",
            "taom_enlist_release_stay", "Stay and serve",
            ConfirmDesertion,
            null,
            "DAYS", daysOwed.ToString());
    }

    /// <summary>
    /// Runs on a LATER frame, from the inquiry callback — outside the menu-option consequence
    /// that checked authority. The gate at the option site does not cover this moment, so it is
    /// re-checked here rather than assumed.
    /// </summary>
    private void ConfirmDesertion()
    {
        if (!_coopSession.IsAuthority)
            return;

        _service.RequestDischarge(DischargeReason.Desertion);
    }
}
