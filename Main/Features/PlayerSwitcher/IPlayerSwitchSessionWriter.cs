using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Write side of the selection. Split from the read side so that the only components able to
/// change the selection are the picker UI and the patch that clears it.
/// </summary>
public interface IPlayerSwitchSessionWriter
{
    void Select(HeroPickRow row);

    /// <summary>Clears the selection. Called on every BodyGeneratorView construction.</summary>
    void Clear();

    void SetPreviewActive(bool active);

    /// <summary>Records what the handover did, for listeners that run after character creation ends.</summary>
    void RecordOutcome(SwitchOutcome outcome, SwitchPath path, string heroId);

    /// <summary>
    /// Wipes everything including the recorded outcome. Called when a NEW character creation
    /// begins, so a second campaign in one process cannot inherit the first one's result.
    /// </summary>
    void ResetForNewCreation();
}
