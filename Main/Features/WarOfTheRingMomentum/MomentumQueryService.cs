using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

public class MomentumQueryService : IMomentumQueryService
{
    private readonly IMomentumStateStore _stateStore;
    private readonly IMomentumSettingsProvider _settings;

    public MomentumQueryService(
        IMomentumStateStore stateStore,
        IMomentumSettingsProvider settings)
    {
        _stateStore = stateStore;
        _settings = settings;
    }

    public bool HasWarStarted => _stateStore.State.HasWarStarted;
    public bool HasWarEnded => _stateStore.State.HasWarEnded;
    public WarOutcome Victor => _stateStore.State.Victor;

    public float InternalMomentum => _stateStore.State.InternalMomentum;

    public int SliderValue
    {
        get
        {
            // RELATIVE balance of the two sides' momentum, mapped to [-100, +100].
            // POSITIVE = FREE ahead (bar fills right, toward the green end); negative = Evil.
            //
            // Deliberately NOT the old "-internal / victoryThreshold × 100": in a long war the
            // accumulated momentum grows many times past the victory threshold (trimmed-at-cap
            // events never subtract, and the player gate can hold the war open), so the
            // threshold-normalized value permanently clamped to one end and the bar never moved.
            // A ratio keeps the bar alive and readable regardless of magnitude.
            long freeM = Math.Max(0L, _stateStore.State.Free.SideMomentum);
            long evilM = Math.Max(0L, _stateStore.State.Evil.SideMomentum);
            long total = freeM + evilM;
            if (total == 0L)
                return 0;

            long value = (freeM - evilM) * 100L / total;
            return (int)Math.Max(-100L, Math.Min(100L, value));
        }
    }

    public int VictoryThreshold => _settings.VictoryThreshold;

    public IReadOnlyList<string> GetKingdomIds(MomentumSide side) =>
        _stateStore.State.GetSide(side).KingdomIds;

    public IReadOnlyList<MomentumEvent> GetEvents(MomentumSide side, MomentumActionType type) =>
        _stateStore.State.GetSide(side).GetEvents(type).ToList();

    public MomentumTotalStats GetTotalStats(MomentumSide side) =>
        _stateStore.State.GetSide(side).TotalStats;

    public event Action MomentumChanged
    {
        add => _stateStore.MomentumChanged += value;
        remove => _stateStore.MomentumChanged -= value;
    }
}
