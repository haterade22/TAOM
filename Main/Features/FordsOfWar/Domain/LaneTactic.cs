using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// How a faction's lanes meet the enemy (Mike, 2026-11-07: Isengard and the dwarves should "advance as a formation, get close and
/// then attack"). An enemy inside the charge range makes the lane charge; one inside the close range but outside the charge range
/// makes it close on the enemy as one formation, in the Close arrangement. March is the arrangement everywhere else, a charge
/// included. A tactic that names no arrangement asks for none, so the engine's default (Line) stays; one that names only Close goes
/// back to Line on the other branches. A null charge range is the match's engage range; a null close range, or one inside the
/// charge range, means no close phase.
/// </summary>
internal sealed record LaneTactic(LaneArrangement? March = null, LaneArrangement? Close = null, float? CloseRange = null,
    float? ChargeRange = null)
{
    /// <summary>The tactic of a faction that names none: charge at the engage range, no arrangement order.</summary>
    public static LaneTactic None { get; } = new();

    /// <summary>Metres from the lead to the nearest enemy inside which the lane charges.</summary>
    public float ChargeAt(float engageRange) => ChargeRange ?? engageRange;

    /// <summary>Metres inside which the lane closes on the enemy; never under the charge range, so a smaller one means no close phase.</summary>
    public float CloseAt(float engageRange) => Math.Max(CloseRange ?? 0f, ChargeAt(engageRange));

    /// <summary>
    /// The arrangement for a branch of the lane rules: Close (or March when it names none) while closing, March otherwise, and Line
    /// when only Close is named, so a wall never outlives its close phase.
    /// </summary>
    public LaneArrangement? ArrangementFor(LaneBranch branch) =>
        branch == LaneBranch.Close ? Close ?? March : March ?? (Close != null ? LaneArrangement.Line : null);
}
