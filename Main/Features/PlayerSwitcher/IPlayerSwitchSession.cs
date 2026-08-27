namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Read side of the character-creation-scoped selection. Handed to consumers that must observe
/// the selection but must never change it, notably the existing Patch9_RaceFilter.
/// </summary>
public interface IPlayerSwitchSession
{
    /// <summary>Empty when nothing is selected.</summary>
    string SelectedHeroId { get; }

    /// <summary>FaceGen race index of the selection, for the live preview.</summary>
    int SelectedRace { get; }

    /// <summary>
    /// True while the preview is driving the face generator. Patch9_RaceFilter early-returns on
    /// this, otherwise its culture race rebuild would snap a dwarf preview back to a human.
    /// </summary>
    bool IsPreviewActive { get; }

    bool HasSelection { get; }
}
