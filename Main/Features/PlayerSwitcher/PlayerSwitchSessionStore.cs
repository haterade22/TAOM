namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Character-creation-scoped selection state, held for the lifetime of the process and cleared
/// every time the face generator is constructed. Implements both faces of the split so a single
/// registered singleton serves readers and writers, while consumers only ever see the half they
/// are entitled to.
/// </summary>
public class PlayerSwitchSessionStore : IPlayerSwitchSession, IPlayerSwitchSessionWriter
{
    public string SelectedHeroId { get; private set; } = string.Empty;

    public int SelectedRace { get; private set; }

    public bool IsPreviewActive { get; private set; }

    public bool HasSelection => !string.IsNullOrEmpty(SelectedHeroId);

    public void Select(string heroId, int race)
    {
        SelectedHeroId = heroId ?? string.Empty;
        SelectedRace = race;
    }

    public void Clear()
    {
        SelectedHeroId = string.Empty;
        SelectedRace = 0;

        // Ending the preview here matters as much as dropping the id. A stale IsPreviewActive
        // would keep Patch9_RaceFilter early-returning for the rest of character creation, so the
        // culture race filter would silently stop applying to a player building their own face.
        IsPreviewActive = false;
    }

    public void SetPreviewActive(bool active) => IsPreviewActive = active;
}
