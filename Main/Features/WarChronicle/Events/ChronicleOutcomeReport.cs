using System.Collections.Generic;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>One effect row as <see cref="ChronicleOutcomeService.Apply"/> wrote it: the kingdoms it reached, never the ones it named.</summary>
public sealed class ChronicleAppliedEffect
{
    public ChronicleAppliedEffect(string target, IReadOnlyList<string> kingdomIds, WarEffectKind kind, float magnitude, int durationDays)
    {
        Target = target;
        KingdomIds = kingdomIds;
        Kind = kind;
        Magnitude = magnitude;
        DurationDays = durationDays;
    }

    /// <summary>The row's target as the config wrote it: <c>free</c>, <c>evil</c>, <c>attacker</c>, <c>defender</c> or a kingdom id.</summary>
    public string Target { get; }

    /// <summary>The live kingdoms the row was written for; never empty.</summary>
    public IReadOnlyList<string> KingdomIds { get; }

    public WarEffectKind Kind { get; }

    /// <summary>The configured fraction before the War effect strength setting scales it.</summary>
    public float Magnitude { get; }

    public int DurationDays { get; }
}

/// <summary>A momentum award the momentum service accepted.</summary>
public sealed class ChronicleAppliedMomentum
{
    public ChronicleAppliedMomentum(MomentumSide side, int units)
    {
        Side = side;
        Units = units;
    }

    public MomentumSide Side { get; }

    public int Units { get; }
}

/// <summary>
/// What one resolution actually changed, for the event window's effects list. Rows that reached no live kingdom and a
/// refused momentum award are left out, so the window never shows an effect the campaign did not get. Not saved: the
/// announcement that carries it lives until the window shows.
/// </summary>
public sealed class ChronicleOutcomeReport
{
    public static readonly ChronicleOutcomeReport None = new ChronicleOutcomeReport(new ChronicleAppliedEffect[0], null);

    public ChronicleOutcomeReport(IReadOnlyList<ChronicleAppliedEffect> effects, ChronicleAppliedMomentum? momentum)
    {
        Effects = effects;
        Momentum = momentum;
    }

    public IReadOnlyList<ChronicleAppliedEffect> Effects { get; }

    public ChronicleAppliedMomentum? Momentum { get; }

    public bool IsEmpty => Effects.Count == 0 && Momentum == null;
}
