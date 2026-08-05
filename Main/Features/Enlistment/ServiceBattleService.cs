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

    public void OnCommanderBattleStarted(string commanderPartyId, string attackerPartyId, string defenderPartyId)
    {
        if (_store.Record.State != EnlistmentState.EnlistedAttached)
            return;

        var side = _encounter.GetPartyBattleSide(commanderPartyId);
        if (side == null)
            return;

        if (!_encounter.CanMainPartyJoinBattleOf(commanderPartyId, side.Value))
        {
            _logger?.LogInfo($"[Enlistment] commander battle at {commanderPartyId} not joinable by the main party — staying parked");
            return;
        }

        // State first (redirect-exempt), then presence, then encounter/menu work.
        _machine.TryTransition(EnlistmentState.EnlistedBattle);
        _attachment.RestorePresence();

        if (_encounter.HasCurrent && _encounter.EncounteredPartyId != commanderPartyId)
            _encounter.Finish(false);

        if (!_encounter.HasCurrent
            && !string.IsNullOrEmpty(defenderPartyId)
            && !string.IsNullOrEmpty(attackerPartyId))
        {
            _encounter.RestartBattle(defenderPartyId, attackerPartyId);
        }

        if (_encounter.JoinBattle(side.Value))
        {
            _gameMenu.SwitchTo("encounter");
            _logger?.LogInfo($"[Enlistment] joined commander battle on side {side.Value}");
            return;
        }

        // Rollback (the donor's wasHiddenServiceMode contract): never strand a visible,
        // active main party in battle state after a failed join.
        _logger?.LogWarning("[Enlistment] JoinBattle failed — rolling back to parked service mode");
        _machine.TryTransition(EnlistmentState.EnlistedAttached);
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
