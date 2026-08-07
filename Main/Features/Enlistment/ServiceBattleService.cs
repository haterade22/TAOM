using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class ServiceBattleService : IServiceBattleService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IEncounterAdapter _encounter;
    private readonly IServiceAttachmentService _attachment;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IModLogger _logger;

    public ServiceBattleService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IEncounterAdapter encounter,
        IServiceAttachmentService attachment,
        IGameMenuAdapter gameMenu,
        IModLogger logger)
    {
        _store = store;
        _machine = machine;
        _encounter = encounter;
        _attachment = attachment;
        _gameMenu = gameMenu;
        _logger = logger;
    }

    public void OnCommanderBattleStarted(string commanderPartyId) =>
        TryJoin(commanderPartyId, "MapEventStarted");

    // Hourly recovery: the reconciler saw the commander fighting while the player was not in the
    // event. Without this the single MapEventStarted edge is the only chance to ever join, so a
    // miss (save-load mid-battle, a throw, enlisting into an already-running fight) stranded the
    // player in the column permanently.
    public void TryJoinCommanderBattle(string commanderPartyId) =>
        TryJoin(commanderPartyId, "hourly recovery");

    private void TryJoin(string commanderPartyId, string trigger)
    {
        if (_store.Record.State != EnlistmentState.EnlistedAttached)
        {
            _logger?.LogInfo($"[Enlistment] commander battle ({trigger}) ignored — state is {_store.Record.State}, not EnlistedAttached");
            return;
        }

        var side = _encounter.GetPartyBattleSide(commanderPartyId);
        if (side == null)
        {
            _logger?.LogInfo($"[Enlistment] commander battle ({trigger}) ignored — no battle side for '{commanderPartyId}'");
            return;
        }

        if (!_encounter.IsCommanderBattleJoinable(commanderPartyId, side.Value))
            return;

        // Ordering is load-bearing and mirrors the donor's proven sequence:
        //   state (redirect-exempt) -> presence -> position -> encounter -> join -> menu.
        // Presence must be restored BEFORE any encounter work: the engine skips inactive parties
        // in encounter detection, and a party that acquires a MapEventSide without a live
        // PlayerEncounter + open "encounter" menu freezes its map event permanently (the engine
        // ticks every map event EXCEPT the player's, which only advances via PlayerEncounter).
        _machine.TryTransition(EnlistmentState.EnlistedBattle);
        _attachment.RestorePresence();
        _attachment.SyncPosition(_store.Record.CommanderHeroId);

        if (_encounter.EnsureEncounterAgainst(commanderPartyId))
        {
            // Attacker only. MapEvent.AddInvolvedPartyInternal converts a siege ASSAULT to
            // SiegeOutside when a defender joins with CurrentSettlement == null — so leaving the
            // settlement before a defender join would rewrite the battle type for every
            // participant, turning an assault on the walls into a field fight outside them.
            if (side.Value == PartyBattleSide.Attacker)
                _encounter.LeaveSettlementIfUnderSiege();

            if (_encounter.JoinBattle(side.Value))
            {
                // The menu is not cosmetic: the engine ticks every map event EXCEPT the player's,
                // which advances only through PlayerEncounter.Update, driven from this menu. A
                // verified join with no menu freezes the event permanently — and the hourly
                // recovery cannot rescue it, because the player IS in a map event by then, so
                // Assess reports Attached rather than BattleJoinRequired. Roll back instead.
                if (_gameMenu.EnsureMenuOpen("encounter"))
                {
                    _logger?.LogInfo($"[Enlistment] joined commander battle on side {side.Value} ({trigger})");
                    return;
                }

                _logger?.LogError("[Enlistment] joined the battle but could not open the encounter menu — rolling back to avoid freezing the map event");
            }
        }

        // Rollback (the donor's wasHiddenServiceMode contract): never strand a visible, active
        // main party in battle state after a failed join. Finish() first — leaving an orphaned
        // PlayerEncounter behind blocks the main party from ever entering another encounter.
        _logger?.LogWarning($"[Enlistment] could not join commander battle ({trigger}) — rolling back to parked service mode");
        _encounter.Finish(false);

        // Load-bearing for recovery: if this transition were ever rejected, state would stay
        // EnlistedBattle while the party is parked, and the entry guard above would block every
        // future join with no runtime signal. Force it rather than fail silently.
        if (!_machine.TryTransition(EnlistmentState.EnlistedAttached))
        {
            _logger?.LogError("[Enlistment] rollback transition to EnlistedAttached was rejected — forcing state to avoid stranding service");
            _store.Record.State = EnlistmentState.EnlistedAttached;
        }

        _attachment.EnsureParked(_store.Record.CommanderHeroId);
    }

    public void OnCommanderBattleEnded()
    {
        if (_store.Record.State != EnlistmentState.EnlistedBattle)
            return;

        // Loot/aftermath menus run inside the still-open encounter; flipping to Attached
        // now would let the menu guard eat them. The reconciler and wait-menu init close
        // the loop once the encounter is gone.
        if (_encounter.HasCurrent)
            return;

        _machine.TryTransition(EnlistmentState.EnlistedAttached);
        _attachment.EnsureParked(_store.Record.CommanderHeroId);
    }
}
