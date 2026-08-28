using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Character-creation-scoped selection state, held for the lifetime of the process and cleared
/// every time the face generator is constructed. Implements both faces of the split so a single
/// registered singleton serves readers and writers, while consumers only ever see the half they
/// are entitled to.
/// </summary>
public class PlayerSwitchSessionStore : IPlayerSwitchSession, IPlayerSwitchSessionWriter
{
    public HeroPickRow SelectedRow { get; private set; }

    public string SelectedHeroId => SelectedRow.HeroId ?? string.Empty;

    public int SelectedRace => SelectedRow.Race;

    public bool IsPreviewActive { get; private set; }

    public bool HasSelection => !SelectedRow.IsEmpty;

    public void Select(HeroPickRow row) => SelectedRow = row;

    public void Clear()
    {
        SelectedRow = default;

        // Ending the preview here matters as much as dropping the row. A stale IsPreviewActive
        // would keep Patch9_RaceFilter early-returning for the rest of character creation, so the
        // culture race filter would silently stop applying to a player building their own face.
        IsPreviewActive = false;
    }

    public void SetPreviewActive(bool active) => IsPreviewActive = active;

    public SwitchOutcome LastOutcome { get; private set; } = SwitchOutcome.NotAttempted;

    public SwitchPath LastPath { get; private set; } = SwitchPath.AssumeIdentity;

    public string LastSwitchedHeroId { get; private set; } = string.Empty;

    public void RecordOutcome(SwitchOutcome outcome, SwitchPath path, string heroId)
    {
        LastOutcome = outcome;
        LastPath = path;
        LastSwitchedHeroId = heroId ?? string.Empty;
    }

    public void ResetForNewCreation()
    {
        Clear();
        LastOutcome = SwitchOutcome.NotAttempted;
        LastPath = SwitchPath.AssumeIdentity;
        LastSwitchedHeroId = string.Empty;
    }
}
