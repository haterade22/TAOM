using TaleWorlds.CampaignSystem;
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

    private EquipmentIssueResult _lastResult;

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

        starter.AddDialogLine(
            "taom_enlist_qm_issued",
            "taom_enlist_qm_answer",
            "close_window",
            "{=taom_enlist_qm_issued}See the quartermaster's wagon — your kit is in your baggage.",
            () => _lastResult == EquipmentIssueResult.Issued,
            null,
            112);

        starter.AddDialogLine(
            "taom_enlist_qm_refused",
            "taom_enlist_qm_answer",
            "close_window",
            "{=taom_enlist_qm_refused}You have drawn your due for your rank already. Earn the next stripe first.",
            null,
            null,
            111);
    }

    private static string PartnerId() => Hero.OneToOneConversationHero?.StringId;

    private void OnKitRequested()
    {
        if (!_coopSession.IsAuthority)
        {
            _lastResult = EquipmentIssueResult.PartyUnavailable;
            return;
        }

        // Culture read at issuance time from the commander — never cached.
        var cultureId = _commander.GetCultureId(_store.Record.CommanderHeroId);
        var assignment = _contentStore.Record.Assignment;
        var rank = (EnlistmentRank)(int)_contentStore.Record.Rank;
        _lastResult = _equipment.IssueForRank(cultureId, assignment, rank);
        _logger?.LogInfo($"[Enlistment] quartermaster issue: {_lastResult} "
            + $"(culture={cultureId}, assignment={assignment}, rank={rank})");
    }
}
