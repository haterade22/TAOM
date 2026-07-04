using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

public class MomentumQueryService : IMomentumQueryService
{
    private readonly IMomentumStateStore _stateStore;
    private readonly IMomentumConfigProvider _configProvider;
    private readonly IMomentumSettingsProvider _settings;

    public MomentumQueryService(
        IMomentumStateStore stateStore,
        IMomentumConfigProvider configProvider,
        IMomentumSettingsProvider settings)
    {
        _stateStore = stateStore;
        _configProvider = configProvider;
        _settings = settings;
    }

    public bool HasWarStarted => _stateStore.State.HasWarStarted;
    public bool HasWarEnded => _stateStore.State.HasWarEnded;
    public WarOutcome Victor => _stateStore.State.Victor;

    public float InternalMomentum => _stateStore.State.InternalMomentum;

    public float DisplayMomentum =>
        _stateStore.State.GetDisplayMomentum(_configProvider.GetConfig().SoftCapDivisor);

    public int SliderValue
    {
        get
        {
            // LOTRAOM parity (MomentumIndicatorItemVM): −1 × internal/threshold × 100.
            // Clamped because momentum can exceed the threshold while the player
            // victory gate holds the war open.
            float threshold = Math.Max(1, VictoryThreshold);
            int raw = -1 * (int)(InternalMomentum / threshold * 100f);
            return Math.Max(-100, Math.Min(100, raw));
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
