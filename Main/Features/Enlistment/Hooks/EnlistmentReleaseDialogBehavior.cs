using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin dialog registration (ADR-002) for asking your commander, in person, to be released —
/// and for the one branch in the whole feature that produces
/// <see cref="DischargeReason.Desertion"/>.
///
/// Split from the oath chain (ADR-003): two genuinely separate conversations that share only
/// the <c>hero_main_options</c> entry, and together exceeded the entry-point ceiling.
/// </summary>
public class EnlistmentReleaseDialogBehavior : CampaignBehaviorBase
{
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentService _service;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IModLogger _logger;

    public EnlistmentReleaseDialogBehavior(
        IEnlistmentDialogGateService gate,
        IEnlistmentService service,
        ICoopSessionProvider coopSession,
        IModLogger logger)
    {
        _gate = gate;
        _service = service;
        _coopSession = coopSession;
        _logger = logger;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_enlist_discharge",
            "hero_main_options",
            "taom_enlist_discharge_answer",
            "{=taom_enlist_discharge}I ask to be released from your service.",
            () => _gate.CanRequestDischargeFrom(PartnerId()),
            null,
            111);

        // Three answers to one question, and the order matters. The verdicts are disjoint, so only
        // one condition can pass — but registration order decides ties, and a tie here would answer
        // "your term is not served" during a battle, sending the player to a desertion prompt when
        // all they needed was to be told to ask again later.
        starter.AddDialogLine(
            "taom_enlist_release_battle",
            "taom_enlist_discharge_answer",
            "close_window",
            "{=taom_enlist_release_battle}Not while there is fighting to be done. Ask me again when the field is quiet.",
            () => ReleaseVerdictNow() == ReleaseVerdict.RefusedInBattle,
            null,
            111);

        starter.AddDialogLine(
            "taom_enlist_release_soon",
            "taom_enlist_discharge_answer",
            "taom_enlist_release_soon_reply",
            "{=taom_enlist_release_soon}You swore to serve, and {DAYS} days of that oath are still owed. I hold you to it.",
            SetDaysOwedIfTooSoon,
            null,
            111);

        starter.AddDialogLine(
            "taom_enlist_discharge_answer",
            "taom_enlist_discharge_answer",
            "close_window",
            "{=taom_enlist_discharge_answer}You have marched well enough. Go, then — you are released.",
            null,
            OnDischargeGranted,
            111);

        // The desert branch. The player is told the cost by the line above before choosing, and
        // this is the ONLY producer of DischargeReason.Desertion in the feature — leaving is never
        // classified as desertion behind the player's back.
        starter.AddPlayerLine(
            "taom_enlist_release_desert",
            "taom_enlist_release_soon_reply",
            "taom_enlist_release_desert_answer",
            "{=taom_enlist_release_desert}Then I am done with your company. I take my leave, oath or no.",
            null,
            null,
            110);

        starter.AddPlayerLine(
            "taom_enlist_release_relent",
            "taom_enlist_release_soon_reply",
            "close_window",
            "{=taom_enlist_release_relent}As you say. I will hold the line.",
            null,
            null,
            111);

        starter.AddDialogLine(
            "taom_enlist_release_desert_answer",
            "taom_enlist_release_desert_answer",
            "close_window",
            "{=taom_enlist_release_desert_answer}Then you are an oathbreaker, and every man here knows it. Get out of my sight.",
            null,
            OnDesertionConfirmed,
            111);
    }

    // Boundary conversion only: the conversation partner's id.
    private static string PartnerId() => Hero.OneToOneConversationHero?.StringId;

    // Per conversation frame, not per campaign tick: re-reading keeps the answer honest if a
    // battle starts mid-conversation.
    private ReleaseVerdict ReleaseVerdictNow() =>
        _gate.EvaluateReleaseRequest(CampaignTime.Now.ToDays).Verdict;

    // Condition AND text setup: the line cannot state the debt unless the number is pushed
    // before it renders, and the condition is the only hook running at that moment.
    private bool SetDaysOwedIfTooSoon()
    {
        var request = _gate.EvaluateReleaseRequest(CampaignTime.Now.ToDays);
        if (request.Verdict != ReleaseVerdict.RefusedTooSoon)
            return false;

        MBTextManager.SetTextVariable("DAYS", request.DaysOwed.ToString());
        return true;
    }

    private void OnDischargeGranted()
    {
        if (!_coopSession.IsAuthority)
            return;
        _service.RequestDischarge(_gate.ClassifyLeaveReason(CampaignTime.Now.ToDays));
    }

    private void OnDesertionConfirmed()
    {
        if (!_coopSession.IsAuthority)
            return;
        _logger?.LogInfo("[Enlistment] player deserted after being refused release in person");
        _service.RequestDischarge(DischargeReason.Desertion);
    }
}
