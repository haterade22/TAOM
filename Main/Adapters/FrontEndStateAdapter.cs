using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary><see cref="IFrontEndStateAdapter"/> over <see cref="GameStateManager"/> and Gauntlet's movie
/// handles.</summary>
public sealed class FrontEndStateAdapter : IFrontEndStateAdapter
{
    public bool IsMainMenuActive() => GameStateManager.Current?.ActiveState is InitialState;

    public bool IsInCharacterCreation() => GameStateManager.Current?.LastOrDefault<CharacterCreationState>() != null;

    // Every way a movie dies ends in IGauntletMovie.Release(), which sets IsReleased: the layer's own
    // finalize (GauntletLayer.ClearContext, v1.5.3 GauntletLayer.cs:48-53, reached from RemoveLayer and
    // from a screen pop) and GauntletLayer.ReleaseMovie (:140-148) alike. A resource refresh releases a
    // movie and puts a new one on the same identifier within one call (:60-77, :126-128), so a check
    // made from a later tick sees the live replacement.
    public bool IsMovieReleased(object movie) =>
        movie is not GauntletMovieIdentifier { Movie: { } loaded } || loaded.IsReleased;
}
