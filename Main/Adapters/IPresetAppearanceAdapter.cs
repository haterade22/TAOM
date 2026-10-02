namespace TAOM.Adapters;

/// <summary>
/// Kysaro's hero presets (#704): copies a picked hero's or character template's look, gear, name and
/// skills onto the player's character during character creation. The picked object is the engine's
/// own <c>Hero</c> or <c>CharacterObject</c>; everything this adapter hands back is opaque to the
/// service that decides when to copy what.
/// </summary>
public interface IPresetAppearanceAdapter
{
    /// <summary>Resolves a pick once: a hero (copied with his name) or a character template (copied
    /// without one), with its gear drawn once, so the face generator's preview, the face-generator copy
    /// and the final copy all show the same kit. A pick with no gear of its own takes its culture's
    /// troop's. Null when <paramref name="picked"/> is neither.</summary>
    object? Resolve(object picked, bool isHero);

    /// <summary>The player's body, race, sex and battle and civilian gear now, to put back later.</summary>
    object? CaptureLook();

    /// <summary>Puts back a look from <see cref="CaptureLook"/>.</summary>
    void RestoreLook(object look);

    /// <summary>Body, race, sex and battle and civilian gear from a resolved pick.</summary>
    void ApplyLook(object resolved);

    /// <summary>A resolved hero's name (a template's pick keeps the player's) and every skill level.</summary>
    void ApplyIdentity(object resolved);

    /// <summary>The battle gear to dress the face generator's model in, as a fresh engine
    /// <c>Equipment</c> copy, or null when the pick has none.</summary>
    object? DisplayEquipment(object resolved);
}
