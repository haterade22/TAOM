using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Features.HallSeats;

/// <summary>
/// Hall Seats (docs/features/hall-seats.md): the lord's hall spawns only as many lords as it has lord spots, so the rest
/// are not stacked on one point in a wall of armour. One spawn pass is <c>MissionAgentHandler.SpawnLocationCharacters</c>:
/// <see cref="BeginPass"/> at its start, <see cref="ShouldHoldBack"/> for each character it spawns, <see cref="EndPass"/>
/// when it returns or throws. The plan is made on the first spawn of the pass, after <c>LocationCharactersAreReadyToSpawn</c>
/// has added the hall's other characters. A held-back lord is not spawned; nothing is moved or removed from the campaign.
/// A spawn outside a pass (a lord walking in mid-visit) is never held back. Main thread only.
/// </summary>
public sealed class HallSeatsService
{
    private readonly IHallSeatsAdapter _adapter;
    private readonly IModLogger _logger;

    private object? _handler;
    private bool _planned;
    private HallSeatPlan? _plan;

    public HallSeatsService(IHallSeatsAdapter adapter, IModLogger logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>True while a spawn pass is open.</summary>
    public bool InPass => _handler != null;

    /// <summary>Opens a pass. Returns false (and opens nothing) when the feature is off or a pass is already open.</summary>
    public bool BeginPass(object missionAgentHandler, bool enabled)
    {
        if (!enabled || missionAgentHandler == null || _handler != null)
            return false;

        _handler = missionAgentHandler;
        _planned = false;
        _plan = null;
        return true;
    }

    public void EndPass()
    {
        _handler = null;
        _planned = false;
        _plan = null;
    }

    /// <summary>True when this location character is a lord the hall has no seat for in this pass.</summary>
    public bool ShouldHoldBack(object locationCharacter)
    {
        if (_handler == null || locationCharacter == null)
            return false;

        if (!_planned)
        {
            _planned = true;
            _plan = MakePlan(_handler);
        }

        return _plan != null && _plan.IsHeldBack(locationCharacter);
    }

    private HallSeatPlan? MakePlan(object handler)
    {
        try
        {
            var hall = _adapter.ReadLordsHall(handler);
            if (hall == null || hall.Lords.Count == 0)
                return null;

            var plan = HallSeatPlanner.Plan(hall.Lords, hall.FreeLordSpots, hall.SpotsTakenByOthers);
            if (plan.HeldBack.Count > 0)
            {
                _logger.LogInfo($"[HallSeats] {hall.SceneName}: {hall.Lords.Count} lords, {hall.FreeLordSpots} free lord spots, " +
                                $"{hall.SpotsTakenByOthers} taken by others; seated {plan.Seated}, held back {plan.HeldBack.Count}");
            }

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HallSeats] could not read the lord's hall, every lord spawns as in vanilla: {ex}");
            return null;
        }
    }
}
