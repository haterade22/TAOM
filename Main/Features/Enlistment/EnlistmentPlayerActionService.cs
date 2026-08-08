using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Features.Enlistment;

public class EnlistmentPlayerActionService : IEnlistmentPlayerActionService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IMapConversationAdapter _conversation;
    private readonly IDutyOrchestrationService _duties;
    private readonly IModLogger _logger;

    public EnlistmentPlayerActionService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IMapConversationAdapter conversation,
        IDutyOrchestrationService duties,
        IModLogger logger)
    {
        _store = store;
        _commander = commander;
        _conversation = conversation;
        _duties = duties;
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

        // Detached on duty the player is a free agent — they can ride up and click the lord.
        if (record.State == EnlistmentState.EnlistedDetachedOnDuty)
            return TalkToCommanderResult.OnDuty;

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

    public DutyRequestResult RequestDutyNow(double nowDays, double hourOfDay) =>
        _duties.RequestDutyNow(nowDays, hourOfDay);
}
