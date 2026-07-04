using System;
using System.Collections.Generic;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Read facade for the UI layer (map slider + popup). The slider sign convention is
/// pinned HERE and only here: positive = Evil ahead (LOTRAOM visual parity — the map
/// slider fills rightward toward Mordor), computed as
/// −1 × internal/threshold × 100, clamped to ±100.
/// </summary>
public interface IMomentumQueryService
{
    bool HasWarStarted { get; }
    bool HasWarEnded { get; }
    WarOutcome Victor { get; }

    /// <summary>Signed internal momentum: positive = Free ahead.</summary>
    float InternalMomentum { get; }

    /// <summary>tanh-soft-capped ±100 display value: positive = Free ahead.</summary>
    float DisplayMomentum { get; }

    /// <summary>Map-slider value: −100..+100, POSITIVE = EVIL ahead (victory-progress normalized).</summary>
    int SliderValue { get; }

    int VictoryThreshold { get; }

    IReadOnlyList<string> GetKingdomIds(MomentumSide side);
    IReadOnlyList<MomentumEvent> GetEvents(MomentumSide side, MomentumActionType type);
    MomentumTotalStats GetTotalStats(MomentumSide side);

    /// <summary>Forwarded from the state store; the UI's refresh signal.</summary>
    event Action MomentumChanged;
}
