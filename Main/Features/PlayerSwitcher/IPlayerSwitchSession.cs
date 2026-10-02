using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Read side of the character-creation-scoped selection. Handed to consumers that must observe
/// the selection but must never change it, notably the existing Patch9_RaceFilter.
/// </summary>
public interface IPlayerSwitchSession
{
    /// <summary>
    /// The chosen lord, or an empty row when nothing is selected. The whole row is held rather
    /// than just an id so the handover can be planned at finalize time without re-querying a
    /// campaign whose culture selection may since have moved on.
    /// </summary>
    HeroPickRow SelectedRow { get; }

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

    /// <summary>
    /// What the handover did, recorded at finalize so a later listener can react to it. Survives
    /// <see cref="IPlayerSwitchSessionWriter.Clear"/>, because the selection is consumed at
    /// finalize but the outcome is still needed by OnCharacterCreationIsOverEvent, which fires
    /// afterwards.
    /// </summary>
    SwitchOutcome LastOutcome { get; }

    /// <summary>Which path the handover took. Only meaningful when <see cref="LastOutcome"/> took effect
    /// (<see cref="SwitchOutcomeExtensions.TookEffect"/>).</summary>
    SwitchPath LastPath { get; }

    /// <summary>The hero the player became, or empty.</summary>
    string LastSwitchedHeroId { get; }

    /// <summary>
    /// The gold that hero held when the handover finished, or -1 when no handover took effect. Read
    /// back after character creation, because the engine assigns the player 1,000 gold once every
    /// handler has run (FinalizeCharacterCreationState) and a taken-over lord keeps their own treasury.
    /// </summary>
    int LastHeroGold { get; }

    /// <summary>
    /// The last handover took effect on the takeover path: the player is now an existing lord with their
    /// own clan and treasury. False for an adopted wanderer, who joins the clan the player made.
    /// </summary>
    bool LordTakenOver { get; }
}
