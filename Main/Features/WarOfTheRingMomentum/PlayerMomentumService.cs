using TAOM.Adapters;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// LOTRAOM parity source: PlayerMomentumService. Event storage lives in the state
/// store (LOTRAOM never persisted it — the victory gate silently reset on reload;
/// TAOM fixes that), this service owns the policy: the queue caps at 2× the victory
/// minimum, and ties in the strength comparison count as Evil-stronger.
/// </summary>
public class PlayerMomentumService : IPlayerMomentumService
{
    private readonly IMomentumStateStore _stateStore;
    private readonly IMomentumSettingsProvider _settings;
    private readonly IPlayerContextAdapter _playerContext;

    public PlayerMomentumService(
        IMomentumStateStore stateStore,
        IMomentumSettingsProvider settings,
        IPlayerContextAdapter playerContext)
    {
        _stateStore = stateStore;
        _settings = settings;
        _playerContext = playerContext;
    }

    public void RecordPlayerEvent(MomentumActionType actionType)
    {
        var events = _stateStore.PlayerEvents;
        events.Add(actionType);

        int maxEvents = _settings.MinimumPlayerEventsForVictory * 2;
        while (events.Count > maxEvents)
            events.RemoveAt(0);
    }

    public bool HasPlayerMetVictoryRequirement()
    {
        return _stateStore.PlayerEvents.Count >= _settings.MinimumPlayerEventsForVictory;
    }

    public float GetParticipationMultiplier(bool playerInvolved)
    {
        return playerInvolved ? _settings.ParticipationMultiplier : 1.0f;
    }

    public bool IsPlayerOnStrongerSide(MomentumWarState state, float freeStrength, float evilStrength)
    {
        if (state == null)
            return false;

        var playerKingdomId = _playerContext.GetPlayerKingdomId();
        if (string.IsNullOrEmpty(playerKingdomId))
            return false;

        bool playerIsFree = state.Free.ContainsKingdom(playerKingdomId);
        bool playerIsEvil = state.Evil.ContainsKingdom(playerKingdomId);
        if (!playerIsFree && !playerIsEvil)
            return false;

        // LOTRAOM parity: strict >, so an exact tie counts as Evil-stronger.
        bool freeIsStronger = freeStrength > evilStrength;
        return (playerIsFree && freeIsStronger) || (playerIsEvil && !freeIsStronger);
    }
}
