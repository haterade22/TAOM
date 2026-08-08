using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin dialog registration (ADR-002): the enlist oath chain and the in-person discharge
/// line, both off <c>hero_main_options</c>. All eligibility lives in
/// <see cref="IEnlistmentDialogGateService"/>; the oath consequence funnels through
/// <see cref="IEnlistmentService"/> (petition + oath in one conversation — the two-step
/// service API stays for a later settlement-menu petition path). After the conversation
/// ends, the menu behavior's ConversationEnded handler asserts the wait menu.
/// </summary>
public class EnlistmentDialogBehavior : CampaignBehaviorBase
{
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentService _service;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IModLogger _logger;

    public EnlistmentDialogBehavior(
        IEnlistmentDialogGateService gate,
        IEnlistmentService service,
        IPlayerPartyAdapter playerParty,
        ICoopSessionProvider coopSession,
        IModLogger logger)
    {
        _gate = gate;
        _service = service;
        _playerParty = playerParty;
        _coopSession = coopSession;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_enlist_offer",
            "hero_main_options",
            "taom_enlist_oath_ask",
            "{=taom_enlist_offer}I wish to enlist under your command.",
            () => _gate.CanEnlistWith(PartnerId()) == EnlistGateResult.Ok,
            null,
            110);

        starter.AddDialogLine(
            "taom_enlist_oath_ask",
            "taom_enlist_oath_ask",
            "taom_enlist_oath_answer",
            "{=taom_enlist_oath_ask}Then swear it: your blade at my side, my bread in your pack, until your term is served. Do you swear?",
            null,
            null,
            110);

        starter.AddPlayerLine(
            "taom_enlist_oath_swear",
            "taom_enlist_oath_answer",
            "taom_enlist_welcome",
            "{=taom_enlist_oath_swear}I swear it.",
            null,
            OnOathSworn,
            110);

        starter.AddPlayerLine(
            "taom_enlist_oath_decline",
            "taom_enlist_oath_answer",
            "lord_pretalk",
            "{=taom_enlist_oath_decline}On second thought — not yet.",
            null,
            null,
            110);

        starter.AddDialogLine(
            "taom_enlist_welcome",
            "taom_enlist_welcome",
            "close_window",
            "{=taom_enlist_welcome}Welcome to the column, soldier. Fall in.",
            null,
            null,
            110);

    }

    // Boundary conversion only: the conversation partner's id.
    private static string PartnerId() => Hero.OneToOneConversationHero?.StringId;

    private void OnOathSworn()
    {
        if (!_coopSession.IsAuthority)
            return;
        var commanderId = PartnerId();
        var petition = _service.SubmitPetition(commanderId);
        if (petition != PetitionResult.Accepted)
        {
            _logger?.LogWarning($"[Enlistment] oath consequence: petition rejected ({petition}) for {commanderId}");
            return;
        }
        var oath = _service.CompleteOath(commanderId, _playerParty.GetMainHeroId(), CampaignTime.Now.ToDays);
        if (oath != OathResult.Sworn)
            _logger?.LogWarning($"[Enlistment] oath consequence: CompleteOath returned {oath} for {commanderId}");
    }

}
