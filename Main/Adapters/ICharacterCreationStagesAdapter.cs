namespace TAOM.Adapters;

/// <summary>The engine's own character-creation stages, one value per stage class (v1.5.3).</summary>
public enum CharacterCreationStageKind
{
    Culture,
    FaceGenerator,
    Narrative,
    BannerEditor,
    ClanNaming,
    Review,
    Options,
}

/// <summary>
/// The stage list of one character creation, in engine terms: what it holds, and taking a stage out or
/// appending it back (#704: a faction-screen takeover). Which stages a takeover leaves out, and when, is
/// <c>FactionPickService</c>'s.
/// </summary>
public interface ICharacterCreationStagesAdapter
{
    /// <summary>How many stages the list holds, of any kind.</summary>
    int Count { get; }

    /// <summary>The engine's stage index. While a stage completes, the index of the stage that opens next.</summary>
    int CurrentIndex { get; }

    /// <summary>Stages <see cref="Remove"/> took out are waiting for <see cref="Append"/>: this character
    /// creation's list is short.</summary>
    bool HoldsRemoved { get; }

    /// <summary>Whether a stage of this kind is in the list.</summary>
    bool Has(CharacterCreationStageKind kind);

    /// <summary>Takes the first stage of this kind out of the list and keeps it for <see cref="Append"/>;
    /// false when the list has none.</summary>
    bool Remove(CharacterCreationStageKind kind);

    /// <summary>Appends the stage of this kind that <see cref="Remove"/> took out; false when none was.</summary>
    bool Append(CharacterCreationStageKind kind);
}
