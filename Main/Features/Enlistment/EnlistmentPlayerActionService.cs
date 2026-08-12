using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Features.Enlistment;

public class EnlistmentPlayerActionService : IEnlistmentPlayerActionService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IMapConversationAdapter _conversation;
    private readonly IDutyOrchestrationService _duties;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IMobilePartyAttachmentAdapter _attachment;
    private readonly IModLogger _logger;

    public EnlistmentPlayerActionService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IMapConversationAdapter conversation,
        IDutyOrchestrationService duties,
        ICoopSessionProvider coopSession,
        IMobilePartyAttachmentAdapter attachment,
        IModLogger logger)
    {
        _store = store;
        _commander = commander;
        _conversation = conversation;
        _duties = duties;
        _coopSession = coopSession;
        _attachment = attachment;
        _logger = logger;
    }

    public TalkToCommanderResult CanTalkToCommander()
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return TalkToCommanderResult.NotEnlisted;

        // Ordered by how badly getting it wrong hurts. InBattle first: opening a conversation
        // during a battle tears the PlayerEncounter the battle service seeded, and that encounter
        // is the ONLY thing advancing the player's map event (the engine ticks every map event
        // except the player's). Losing it freezes the battle with no way back.
        if (record.State == EnlistmentState.EnlistedBattle)
            return TalkToCommanderResult.InBattle;

        var commander = _commander.GetSnapshot(record.CommanderHeroId);
        if (commander == null || !commander.Exists || !commander.IsAlive
            || commander.IsPrisoner || !commander.HasParty || !commander.PartyIsActive)
        {
            return TalkToCommanderResult.CommanderUnavailable;
        }

        if (!_conversation.CanOpenConversation)
            return TalkToCommanderResult.NotOnMap;

        return TalkToCommanderResult.Opened;
    }

    public TalkToCommanderResult TalkToCommander()
    {
        // Re-run the gate. A menu option's condition and its consequence are separated by at least
        // a frame, and a commander battle starting in that gap is exactly the case where acting on
        // the stale answer costs the player the whole fight.
        var verdict = CanTalkToCommander();
        if (verdict != TalkToCommanderResult.Opened)
        {
            _logger?.LogInfo($"[Enlistment] talk-to-commander declined: {verdict}");
            return verdict;
        }

        if (_conversation.OpenWithHero(_store.Record.CommanderHeroId))
            return TalkToCommanderResult.Opened;

        _logger?.LogWarning($"[Enlistment] could not open a conversation with '{_store.Record.CommanderHeroId}' — the engine refused");
        return TalkToCommanderResult.CommanderUnavailable;
    }

    /// <summary>
    /// CO-OP: host-only. The daily duty tick is already host-gated; this explicit menu path
    /// reaches the same orchestration — starting field duties, spawning target parties,
    /// presenting reward-bearing inquiries and mutating the content record — so a client
    /// running it locally would fork shared campaign state.
    /// </summary>
    public DutyRequestResult RequestDutyNow(double nowDays, double hourOfDay)
    {
        if (!_coopSession.IsAuthority)
            return DutyRequestResult.NoWorkAvailable;

        return _duties.RequestDutyNow(nowDays, hourOfDay);
    }

    public bool CanTakeTownLeave()
        => TownLeavePolicy.CanTakeLeave(
            _store.Record.State, InsideSettlement(), _store.Record.OnTownLeave);

    public bool TakeTownLeave()
    {
        // Re-check rather than trust the condition that drew the option: a frame passes between a
        // menu option's condition and its consequence, and the column can start marching in it —
        // the same reasoning TalkToCommander documents above.
        if (!CanTakeTownLeave())
            return false;

        _store.Record.OnTownLeave = true;
        _logger?.LogInfo("[Enlistment] shore leave granted — the settlement menu is the player's until the column moves");
        return true;
    }

    /// <summary>
    /// Presence read, guarded. This runs from a menu-option condition (every frame the wait menu is
    /// up) and from the maintenance pump, so a throw here would be a per-frame throw — and the
    /// honest answer when presence cannot be read is "not in a settlement", which closes the gate
    /// rather than opening it.
    /// </summary>
    private bool InsideSettlement()
    {
        try { return _attachment.GetPresenceFlags().IsInSettlement; }
        catch { return false; }
    }
}
