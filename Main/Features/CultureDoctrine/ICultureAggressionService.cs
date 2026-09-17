using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine;

/// <summary>
/// The melee half of a culture's doctrine: the <see cref="CultureAggression"/> profile a
/// soldier's AI decision values are scaled by, answered per culture StringId. Called from the
/// agent-stat models on the main thread at spawn and on the async AI thread on every agent
/// property refresh an order change triggers, so the answer is a dictionary lookup behind a bool
/// over immutable data, and never logs or allocates.
/// </summary>
public interface ICultureAggressionService
{
    /// <summary>The culture's profile, or <see cref="CultureAggression.Vanilla"/> when the
    /// doctrine or its aggression tier is off or the culture has no row.</summary>
    CultureAggression Profile(string? cultureId);
}
