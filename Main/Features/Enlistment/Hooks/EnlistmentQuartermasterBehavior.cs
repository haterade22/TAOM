using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin dialog registration for drawing the service kit from the commander (once per rank, into
/// party inventory). The kit carries weapons as well as armour since #525, and the player's
/// ASSIGNMENT selects which kit: an Archer draws a bow rather than a sword.
///
/// Culture and assignment are both read LIVE at issuance (culture-conversion rule) rather than
/// cached; rank maps ordinal-for-ordinal from the content record onto the equipment enum (both
/// ladders are Recruit..Sergeant, pinned by test).
/// </summary>
public class EnlistmentQuartermasterBehavior : CampaignBehaviorBase
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _contentStore;
    private readonly IEnlistmentDialogGateService _gate;
    private readonly IEnlistmentEquipmentService _equipment;
    private readonly ICommanderLordAdapter _commander;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IModLogger _logger;

    private QuartermasterAnswer _lastAnswer;

    public EnlistmentQuartermasterBehavior(
        IEnlistmentStore store,
        IEnlistmentContentStore contentStore,
        IEnlistmentDialogGateService gate,
        IEnlistmentEquipmentService equipment,
        ICommanderLordAdapter commander,
        ICoopSessionProvider coopSession,
        IModLogger logger)
    {
        _store = store;
        _contentStore = contentStore;
        _gate = gate;
        _equipment = equipment;
        _commander = commander;
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
            "taom_enlist_qm_request",
            "hero_main_options",
            "taom_enlist_qm_answer",
            "{=taom_enlist_qm_request}I would draw my service kit from the quartermaster.",
            () => _gate.CanRequestDischargeFrom(PartnerId()),
            OnKitRequested,
            112);

        // One line per conditioned answer (#829), all at 112.
        AddAnswer("taom_enlist_qm_issued",
            "{=taom_enlist_qm_issued}See the quartermaster's wagon — your kit is in your baggage.",
            () => _lastAnswer == QuartermasterAnswer.Issued);
        AddAnswer("taom_enlist_qm_refused",
            "{=taom_enlist_qm_refused}You have drawn your due for your rank already. Earn the next stripe first.",
            () => _lastAnswer == QuartermasterAnswer.AlreadyDrawn);
        AddAnswer("taom_enlist_qm_refused_stores",
            "{=taom_enlist_qm_refused_stores}The quartermaster has nothing in his stores for your company.",
            () => _lastAnswer == QuartermasterAnswer.EmptyStores);
        AddAnswer("taom_enlist_qm_refused_host",
            "{=taom_enlist_qm_refused_host}Only the host's quartermaster can issue kit in this campaign.",
            () => _lastAnswer == QuartermasterAnswer.HostOnly);

        // CannotLoad is the one reply with no condition; it sits below the conditioned lines, so it
        // also answers any answer that has no line of its own.
        starter.AddDialogLine(
            "taom_enlist_qm_refused_load",
            "taom_enlist_qm_answer",
            "close_window",
            "{=taom_enlist_qm_refused_load}Your kit cannot be loaded into your baggage just now. Ask me again later.",
            null,
            null,
            100);

        void AddAnswer(string id, string text, ConversationSentence.OnConditionDelegate condition) =>
            starter.AddDialogLine(id, "taom_enlist_qm_answer", "close_window", text, condition, null, 112);
    }

    private static string PartnerId() => Hero.OneToOneConversationHero?.StringId;

    private void OnKitRequested()
    {
        if (!_coopSession.IsAuthority)
        {
            _lastAnswer = QuartermasterAnswer.HostOnly;
            return;
        }

        // Culture read at issuance time from the commander — never cached.
        var cultureId = _commander.GetCultureId(_store.Record.CommanderHeroId);
        var assignment = _contentStore.Record.Assignment;
        var rank = (EnlistmentRank)(int)_contentStore.Record.Rank;
        var result = _equipment.IssueForRank(cultureId, assignment, rank);
        _lastAnswer = QuartermasterAnswerPolicy.For(result);
        _logger?.LogInfo($"[Enlistment] quartermaster issue: {result} "
            + $"(culture={cultureId}, assignment={assignment}, rank={rank})");
    }
}
