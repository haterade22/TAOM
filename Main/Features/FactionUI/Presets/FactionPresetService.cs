using TAOM.Adapters;

namespace TAOM.Features.FactionUI.Presets;

/// <summary>
/// The hero picked on Kysaro's faction screen and when it is copied onto the player (#704). Mike's
/// decision (2026-10-01) keeps Kysaro's outcome: the pick's body, race, sex, gear and skills (and a
/// lord's name) end up on the player. Mike's Option A (2026-10-01) sets when: the look and gear are
/// copied when the face generator opens, so it shows the pick, and the name and skills only at the end
/// of character creation (handler priority 1060, after TAOM's 1050), where the look is copied again
/// over the stages since. Nothing invisible is written early, so un-picking leaves nothing behind;
/// the look the character had before the first pick is put back (<see cref="Clear"/>).
/// <para>
/// The selection is per character creation: it is dropped when a new one starts
/// (<see cref="ResetForNewCharacterCreation"/>) and once it has been applied at the end, so a second
/// campaign in the same process never inherits the first one's pick.
/// </para>
/// </summary>
public sealed class FactionPresetService
{
    private readonly IPresetAppearanceAdapter _appearance;

    private object? _pending;
    private object? _applied;
    private object? _lookBeforePick;

    public FactionPresetService(IPresetAppearanceAdapter appearance)
    {
        _appearance = appearance;
    }

    /// <summary>A pick is waiting for the face generator or has been copied onto the player; TAOM's
    /// culture race filter then leaves the face generator's race alone.</summary>
    public bool HasPick => _pending != null || _applied != null;

    /// <summary>A lord or leader from the faction screen's browse lists: copied with his name.</summary>
    public void SelectHero(object hero) => Select(hero, isHero: true);

    /// <summary>A named character card or a wanderer template: copied without a name.</summary>
    public void SelectTemplate(object character) => Select(character, isHero: false);

    /// <summary>"Custom Character", another faction, or the faction screen shown again: forgets the pick
    /// and puts back the look the character had before the first pick was copied.</summary>
    public void Clear()
    {
        _pending = null;
        _applied = null;
        if (_lookBeforePick == null)
            return;
        _appearance.RestoreLook(_lookBeforePick);
        _lookBeforePick = null;
    }

    /// <summary>A new character creation starts, or the game ended: forgets everything, putting nothing
    /// back (the character it belonged to is gone).</summary>
    public void ResetForNewCharacterCreation()
    {
        _pending = null;
        _applied = null;
        _lookBeforePick = null;
    }

    /// <summary>The face generator is being built: copies a pending pick's look and gear and returns the
    /// gear to dress its model in (an engine <c>Equipment</c>), or null to keep the face generator's own.</summary>
    public object? OnFaceGeneratorOpening()
    {
        if (_pending != null)
        {
            _lookBeforePick ??= _appearance.CaptureLook();
            _appearance.ApplyLook(_pending);
            _applied = _pending;
            _pending = null;
        }

        return _applied == null ? null : _appearance.DisplayEquipment(_applied);
    }

    /// <summary>Character creation is finishing: copies the pick's look over what the stages since have
    /// set, then its name and skills, and forgets it.</summary>
    public void OnCharacterCreationFinalize()
    {
        var pick = _applied ?? _pending;
        ResetForNewCharacterCreation();
        if (pick == null)
            return;
        _appearance.ApplyLook(pick);
        _appearance.ApplyIdentity(pick);
    }

    private void Select(object picked, bool isHero)
    {
        _pending = _appearance.Resolve(picked, isHero);
        _applied = null;
    }
}
