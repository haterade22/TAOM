using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin boundary (ADR-002): converts MapEvent start/end into party-id calls on
/// <see cref="IServiceBattleService"/>. Commander involvement is checked against the
/// event's involved parties by StringId; all battle logic lives in the service.
/// Stateless (no SyncData); world mutations host-only.
/// </summary>
public class EnlistmentBattleBehavior : CampaignBehaviorBase
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IServiceBattleService _battle;
    private readonly ICoopSessionProvider _coopSession;

    public EnlistmentBattleBehavior(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IServiceBattleService battle,
        ICoopSessionProvider coopSession)
    {
        _store = store;
        _commander = commander;
        _battle = battle;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!_coopSession.IsAuthority || !_store.Record.IsEnlisted || mapEvent == null)
            return;

        var commanderPartyId = FindCommanderPartyIdIn(mapEvent);
        if (commanderPartyId == null)
            return;

        _battle.OnCommanderBattleStarted(
            commanderPartyId,
            attackerParty?.MobileParty?.StringId,
            defenderParty?.MobileParty?.StringId);
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (!_coopSession.IsAuthority || !_store.Record.IsEnlisted || mapEvent == null)
            return;

        // Either the commander was in it, or we are in battle state (our own event ends
        // count too — e.g. the commander's party was wiped mid-event).
        if (_store.Record.State == Domain.EnlistmentState.EnlistedBattle
            || FindCommanderPartyIdIn(mapEvent) != null)
        {
            _battle.OnCommanderBattleEnded();
        }
    }

    private string FindCommanderPartyIdIn(MapEvent mapEvent)
    {
        var commanderPartyId = _commander.GetSnapshot(_store.Record.CommanderHeroId).PartyId;
        if (string.IsNullOrEmpty(commanderPartyId))
            return null;

        foreach (var involved in mapEvent.InvolvedParties)
        {
            if (involved?.MobileParty?.StringId == commanderPartyId)
                return commanderPartyId;
        }
        return null;
    }
}
