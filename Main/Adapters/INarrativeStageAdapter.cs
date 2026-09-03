namespace TAOM.Adapters;

/// <summary>
/// The narrative stage of character creation, reduced to the three moves needed to walk its menu
/// chain forward. Wraps the sealed CharacterCreationManager so the walk itself stays testable.
/// </summary>
/// <remarks>
/// Deliberately tiny. The engine exposes the whole chain publicly (GetSuitableNarrativeMenuOptions,
/// OnNarrativeMenuOptionSelected, TrySwitchToNextMenu), so nothing here needs reflection, and the
/// walk drives exactly the transition a real click drives.
/// </remarks>
public interface INarrativeStageAdapter
{
    /// <summary>StringId of the menu currently displayed, or empty when there is none.</summary>
    string CurrentMenuId { get; }

    /// <summary>
    /// Picks the first option the current menu offers. False when the menu offers none, which is
    /// the one state the walk must never advance past: TrySwitchToNextMenu indexes SelectedOptions
    /// by the current menu and throws KeyNotFoundException when nothing was selected for it.
    /// </summary>
    bool SelectFirstSuitableOption();

    /// <summary>Moves to the next menu in the chain. False at the end of the chain.</summary>
    bool TryAdvance();
}
