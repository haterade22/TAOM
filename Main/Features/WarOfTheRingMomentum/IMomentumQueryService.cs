using System;
using System.Collections.Generic;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Read facade for the UI layer (map slider + popup). The slider sign convention is
/// pinned HERE and only here: <b>positive = Free ahead</b> (the bar fills rightward toward
/// the green end when the Free Peoples lead; negative = Evil, toward red). It is a RATIO of
/// the two sides' momentum, not a victory-threshold fraction, so the bar keeps moving in a
/// long war instead of clamping to one end.
/// </summary>
public interface IMomentumQueryService
{
    bool HasWarStarted { get; }
    bool HasWarEnded { get; }
    WarOutcome Victor { get; }

    /// <summary>Signed internal momentum: positive = Free ahead.</summary>
    float InternalMomentum { get; }

    /// <summary>Map-slider value: −100..+100, POSITIVE = FREE ahead (relative-balance ratio).</summary>
    int SliderValue { get; }

    int VictoryThreshold { get; }

    IReadOnlyList<string> GetKingdomIds(MomentumSide side);
    IReadOnlyList<MomentumEvent> GetEvents(MomentumSide side, MomentumActionType type);
    MomentumTotalStats GetTotalStats(MomentumSide side);

    /// <summary>Forwarded from the state store; the UI's refresh signal.</summary>
    event Action MomentumChanged;
}
