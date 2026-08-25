using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Presentation;

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
    /// Take shore leave and open the settlement's own menu (field report 1). The player is already
    /// physically inside — this is what stops the wait menu swallowing the town.
    /// </summary>
    void TakeTownLeave();

    /// <summary>
    /// Ask to be released. Owns the whole decision — verdict, the popup that states the cost,
    /// and the discharge itself — so the menu behaviour stays a registration shell.
    /// </summary>
    void RequestRelease(double nowDays);

    /// <summary>Tell the player what asking for work produced. "Nothing right now" is an answer.</summary>
    void ReportDutyRequest(Duties.DutyRequestResult result);

    /// <summary>
    /// The column has stopped somewhere: offer the pass while it is actually stopped. Idempotent
    /// per settlement stop, so a re-follow does not re-ask.
    /// </summary>
    void OfferTownLeave(string settlementId, double nowHours);
}

public sealed class EnlistmentWaitMenuPresenter : IEnlistmentWaitMenuPresenter
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentService _service;
    private readonly IInquiryAdapter _inquiry;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IServiceStatusService _status;
    private readonly IEnlistmentPlayerActionService _actions;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IEnlistmentFeatureSettingsProvider _settings;
    private readonly IModLogger _logger;

    /// <summary>
    /// Which settlement we last offered a pass for. Session state, never persisted: see
    /// <see cref="OfferTownLeave"/>.
    /// </summary>
    private string _lastOfferedSettlementId;

    /// <summary>Campaign hour of the last offer, so a cycling commander cannot spam the modal.</summary>
    private double? _lastOfferedAtHours;

    /// <summary>Minimum campaign hours between offers. One in-game day.</summary>
    private const double OfferCooldownHours = 24.0;

    public EnlistmentWaitMenuPresenter(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IEnlistmentDialogGateService gate,
        IEnlistmentService service,
        IInquiryAdapter inquiry,
        ICoopSessionProvider coopSession,
        IEnlistmentPlayerActionService actions,
        IGameMenuAdapter gameMenu,
        IServiceStatusService status,
        IEnlistmentFeatureSettingsProvider settings,
        IModLogger logger)
    {
        _store = store;
        _commander = commander;
        _gate = gate;
        _service = service;
        _inquiry = inquiry;
        _coopSession = coopSession;
        _actions = actions;
        _gameMenu = gameMenu;
        _status = status;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Force a push on menu init. Invalidate first: the cached model is almost always identical
    /// to what is on screen, so a plain RefreshIfChanged would no-op and leave whatever text the
    /// previous menu had.
    /// </summary>
    public void RefreshWaitText()
    {
        _status.Invalidate();
        _status.RefreshIfChanged();
    }

    /// <summary>
    /// THE POPUP IS THE PAUSE. InformationManager.ShowInquiry halts the game while it is up, which
    /// is the entire reason this is a modal and not a toast: the wait menu runs at
    /// UnstoppableFastForward, so a lord's few-hour town stop is about two real seconds and the
    /// menu option cannot be clicked in that time (#512).
    ///
    /// Deliberately NOT done by touching TimeControlMode. Hand-editing time control on a wait-menu
    /// path is the #506 freeze class, which is why taom.time_status and taom.rescue_time exist.
    /// </summary>
    public void OfferTownLeave(string settlementId, double nowHours)
    {
        if (!_coopSession.IsAuthority)
            return;

        // Once per stop. In-memory ON PURPOSE: a save/reload mid-stop re-asking is harmless and
        // self-healing, whereas persisting it would buy save-compat surface for nothing.
        if (string.IsNullOrEmpty(settlementId) || settlementId == _lastOfferedSettlementId)
            return;

        // AND a cooldown, because per-settlement alone is not enough. A commander cycling three
        // towns changes the id every few seconds (measured 2026-08-25: ten transitions in three
        // real minutes), so an id-keyed latch would pop a modal on nearly every one of them. The
        // dwell holds the placement; this holds the question.
        if (_lastOfferedAtHours.HasValue && nowHours - _lastOfferedAtHours.Value < OfferCooldownHours)
            return;

        _lastOfferedSettlementId = settlementId;
        _lastOfferedAtHours = nowHours;

        if (!_settings.OfferLeaveOnArrival)
            return;

        // Ask the same gate the menu option asks, so the popup can never offer what the option
        // would refuse — including the already-on-leave case.
        if (!_actions.CanTakeTownLeave())
            return;

        _inquiry.ShowTwoOptionInquiry(
            "taom_enlist_arrival_title", "The column halts",
            "taom_enlist_arrival_body",
            "Your company has stopped at {SETTLEMENT}. Your sergeant will look the other way if you want a few hours to yourself.",
            "taom_enlist_arrival_take", "Take your leave",
            "taom_enlist_arrival_stay", "Stay with the company",
            TakeTownLeave, null,
            // `?.` on the SNAPSHOT, not just the name: GetSnapshot returns null for a commander
            // who no longer resolves, and this runs on an arrival that may race his death.
            "SETTLEMENT", _commander.GetSnapshot(_store.Record.CommanderHeroId)?.SettlementName ?? settlementId);

        _logger?.LogInfo($"[Enlistment] offered shore leave on arrival at '{settlementId}'");
    }

    public void TakeTownLeave()
    {
        if (!_actions.TakeTownLeave())
        {
            // SAY SOMETHING. The refusal became reachable when the pass started requiring a real
            // settlement encounter (#510) — before that, the menu option's own condition had
            // already ruled out every failure, so the silent return was dead code. A button that
            // does nothing at all reads as a broken mod, not as a refusal.
            _inquiry.ShowMessage("taom_enlist_leave_refused",
                "You cannot take your leave here just now.", null, null);
            return;
        }

        // Vanilla decides WHICH settlement menu applies; we only ask which one the player is
        // standing in. With the pass now held, EnlistmentMenuService lets these three through
        // untouched, so this is an ordinary menu activation rather than a bypass.
        var menuId = _gameMenu.CurrentSettlementMenuId;
        if (!string.IsNullOrEmpty(menuId))
            _gameMenu.EnsureMenuOpen(menuId);
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
                // Term served — an honourable release. Routed through the gate rather than
                // hardcoding PlayerRequest: the in-person dialog path already asks the gate,
                // and if an early-exit cost is ever added there this path would silently
                // bypass it. DischargeService owns the menu exit and settlement hand-back.
                _service.RequestDischarge(_gate.ClassifyLeaveReason(nowDays));
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
            // NOT taom_enlist_release_desert — that key belongs to the in-person dialog line and
            // carries different words. One key with two English strings translates to whichever
            // one happened to be registered, in both places.
            "taom_enlist_release_desert_option", "Desert the company",
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

    public void ReportDutyRequest(Duties.DutyRequestResult result)
    {
        // DutyAssigned deliberately says nothing: the duty runtime already shows its own popup or
        // toast, and a second 'you got work' message on top of it reads as a double-fire.
        switch (result)
        {
            case Duties.DutyRequestResult.AlreadyOnDuty:
                _inquiry.ShowMessage("taom_enlist_duty_already",
                    "You already have your orders. See them done first.", null, null);
                return;
            case Duties.DutyRequestResult.NoWorkAvailable:
                _inquiry.ShowMessage("taom_enlist_duty_none",
                    "Nothing for you at present. Stay sharp — that changes.", null, null);
                return;
        }
    }
}
