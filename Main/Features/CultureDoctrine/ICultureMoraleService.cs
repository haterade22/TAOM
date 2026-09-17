namespace TAOM.Features.CultureDoctrine;

/// <summary>
/// The morale half of a culture's doctrine, answered per soldier by culture StringId. Both
/// members are called from the battle morale model: <see cref="CanPanic"/> on the engine's
/// worker threads (<c>CommonAIComponent.OnTickParallel</c> asks <c>CanPanicDueToMorale</c>
/// every tick a soldier's morale sits at zero), so an implementation reads immutable data and
/// a bool, allocates nothing and never logs.
/// </summary>
public interface ICultureMoraleService
{
    /// <summary>False when the culture never routs; true for vanilla behaviour.</summary>
    bool CanPanic(string? cultureId);

    /// <summary>The engine's base initial morale plus the culture's bravery.</summary>
    float InitialMorale(string? cultureId, float baseMorale);
}
