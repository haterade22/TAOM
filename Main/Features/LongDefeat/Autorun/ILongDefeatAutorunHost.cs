namespace TAOM.Features.LongDefeat.Autorun;

/// <summary>
/// The engine seams the autorun driver needs, narrow enough that a test fakes them. TAOM.Client implements it
/// (<c>LongDefeatAutorunHost</c>) and replaces core's <see cref="NullLongDefeatAutorunHost"/> (ADR-013). The driver ticks on wall time
/// from the client's application tick, so every call below is made from the engine's main thread.
/// </summary>
internal interface ILongDefeatAutorunHost
{
    /// <summary>The initial menu is the active state and no game is running.</summary>
    bool AtMainMenu { get; }

    /// <summary>Hides an open inquiry (a boot notice nobody will click). True when one was open.</summary>
    bool HideAnyInquiry();

    /// <summary>Starts the run as the setup prompt does. False when it could not start.</summary>
    bool Start(LongDefeatSetup setup);

    /// <summary>Ends the running Long Defeat game and shows the main menu; does nothing when no such game runs.</summary>
    void ReturnToMainMenu();

    /// <summary>
    /// Writes a small text file into the plan's out folder through a temp file and a move, so the launcher polling for the file never
    /// reads half of it. May throw; <see cref="LongDefeatAutorunDriver"/> logs the failure.
    /// </summary>
    void WriteFile(string name, string text);

    /// <summary>Saves a picture of the screen under this name in the plan's out folder. The engine writes 24-bit BMP data whatever the name says.</summary>
    void TakeScreenshot(string name);

    /// <summary>Asks the engine to shut down.</summary>
    void Quit();
}
