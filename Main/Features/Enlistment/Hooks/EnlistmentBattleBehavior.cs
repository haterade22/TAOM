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
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IServiceMaintenanceService _maintenance;
    private readonly ICoopSessionProvider _coopSession;
    private readonly TAOM.Core.Logging.IModLogger _diag;

    public EnlistmentBattleBehavior(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IServiceBattleService battle,
        IEnlistmentReconciler reconciler,
        IServiceMaintenanceService maintenance,
        ICoopSessionProvider coopSession,
        TAOM.Core.Logging.IModLogger diag)
    {
        _store = store;
        _commander = commander;
        _battle = battle;
        _reconciler = reconciler;
        _maintenance = maintenance;
        _coopSession = coopSession;
        _diag = diag;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

        // The reconciler is a singleton and RegisterEvents runs once per campaign session, so
        // detach first — otherwise a second session double-subscribes and joins twice.
        _reconciler.BattleJoinRequested -= OnBattleJoinRequested;
        _reconciler.BattleJoinRequested += OnBattleJoinRequested;

        // The pump raises the SAME event shape, so there remains exactly one hero-id -> party-id ->
        // TryJoinCommanderBattle implementation rather than two that can drift apart.
        _maintenance.BattleJoinRequested -= OnBattleJoinRequested;
        _maintenance.BattleJoinRequested += OnBattleJoinRequested;
    }

    // Hourly recovery path. The reconciler only knows the commander HERO id; the battle service
    // works in party ids, so the conversion happens here at the boundary (ADR-002).
    private void OnBattleJoinRequested(string commanderHeroId)
    {
        if (!_coopSession.IsAuthority || !_store.Record.IsEnlisted)
            return;

        var partyId = _commander.GetPartyId(commanderHeroId);
        if (string.IsNullOrEmpty(partyId))
            return;

        _battle.TryJoinCommanderBattle(partyId);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
    {
        if (!_coopSession.IsAuthority || !_store.Record.IsEnlisted || mapEvent == null)
            return;

        var commanderPartyId = FindCommanderPartyIdIn(mapEvent);
        if (commanderPartyId == null)
        {
            // DEBUG, not INFO: this fires for EVERY map event in the world, and at accelerated
            // campaign speed that was 3674 lines in one 32-minute session — 61% of the whole log,
            // each one synchronously flushed. It stays available for diagnosis (it is what proved
            // the commander was in 0 of 456 events) without drowning the signal.
            _diag?.LogDebug(
                $"[EnlistDiag] map event started, commander NOT in it — commanderHero='{_store.Record.CommanderHeroId}' " +
                $"resolvedParty='{_commander.GetPartyId(_store.Record.CommanderHeroId) ?? "NONE"}' " +
                $"attacker='{attackerParty?.MobileParty?.StringId ?? attackerParty?.Name?.ToString() ?? "?"}' " +
                $"defender='{defenderParty?.MobileParty?.StringId ?? defenderParty?.Name?.ToString() ?? "?"}' " +
                $"involved={CountInvolved(mapEvent)}");
            return;
        }

        _diag?.LogInfo($"[EnlistDiag] map event started WITH the commander ('{commanderPartyId}') — entering join pipeline");

        // attackerParty/defenderParty are deliberately NOT forwarded: for a siege the defender is
        // a settlement PartyBase with no MobileParty, so its id is null and the encounter could
        // never be seeded. The service resolves the event's side leaders itself instead.
        _battle.OnCommanderBattleStarted(commanderPartyId);
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

    private static int CountInvolved(MapEvent mapEvent)
    {
        var n = 0;
        foreach (var _ in mapEvent.InvolvedParties) n++;
        return n;
    }

    private string FindCommanderPartyIdIn(MapEvent mapEvent)
    {
        // GetPartyId, not GetSnapshot: this runs for EVERY map event in the world while
        // enlisted, and the full snapshot walks Culture/Clan/MapFaction/Settlement and
        // allocates through Name.ToString() for data no caller here reads.
        var commanderPartyId = _commander.GetPartyId(_store.Record.CommanderHeroId);
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
