using TAOM.Adapters;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Skips the backstory questions when the player has already chosen a lord to take over.
/// </summary>
public interface INarrativeCareerFastPathService
{
    /// <summary>
    /// Walks the narrative menu chain to the career menu when a lord is selected. Does nothing at
    /// all when none is, so an ordinary character creation is untouched.
    /// </summary>
    void SkipToCareerMenu(INarrativeStageAdapter stage);
}
