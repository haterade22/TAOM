namespace TAOM.Adapters;

/// <summary>
/// The recorded line a mission hero speaks when the field takes his spawn (#1569), kept apart from <see cref="ILongDefeatBattlefieldAdapter"/> so the director's fake stays small. The battlefield
/// adapter implements it, and the run host hands the director the same instance (<c>LongDefeatHeroLinePinTests</c>). Both members run on the main thread, from the director's tick. With no such
/// adapter the heroes are silent.
/// </summary>
internal interface ILongDefeatHeroVoiceAdapter
{
    /// <summary>
    /// Plays the OGG file at <paramref name="fullPath"/> as a 2D voice-over, after stopping a line that still plays. False when the engine made no sound, or the run is on a dedicated server, where none is made.
    /// </summary>
    bool PlayHeroLine(string fullPath);

    /// <summary>Stops the line that plays, if any. Safe to call with none.</summary>
    void StopHeroLine();
}
