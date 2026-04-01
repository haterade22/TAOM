namespace TAOM.Features.FactionMap;

public interface ICultureStageProgressionService
{
    /// <summary>
    /// Invokes the character creation "next stage" logic: sets the character name
    /// for the given culture, then triggers the affirmative action on the view.
    /// </summary>
    /// <param name="viewInstance">The CultureStageView instance (accessed via reflection).</param>
    /// <param name="cultureId">StringId of the selected culture, used for name generation.</param>
    /// <param name="isFemale">Whether the main hero is female, for name generation.</param>
    void InvokeNextStage(object viewInstance, string cultureId, bool isFemale);

    /// <summary>
    /// Loads brush and sprite resources required by the faction map UI.
    /// Swallows exceptions and logs them rather than propagating.
    /// </summary>
    void LoadFactionMapResources();
}
