using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Features.CharacterCreation;

/// <summary>
/// Determines whether a character creation narrative stage should suppress the
/// mount actor because the culture's equipment roster has no horse.
/// </summary>
public interface INarrativeHorseGuardService
{
    /// <summary>
    /// Checks whether the equipment roster for the given culture/title/gender combination
    /// is missing a horse, and if so returns the no-horse character args to use instead.
    /// </summary>
    /// <returns>
    /// A <see cref="NarrativeHorseGuardArgs"/> when the roster exists and has no horse
    /// (the patch should short-circuit); <c>null</c> when the roster is not found or
    /// already contains a horse (let vanilla run).
    /// </returns>
    NarrativeHorseGuardArgs TryBuildNoHorseArgs(
        string cultureId,
        string titleType,
        bool isFemale,
        string characterId,
        int age);
}
