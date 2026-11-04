namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The engine seams the field trial's autostart needs (build-spec section 12), narrow enough that a test fakes them; every member runs on the main thread.</summary>
internal interface ICommandTrialGameHost
{
    /// <summary>The initial menu is the active state and no game is running.</summary>
    bool AtMainMenu { get; }

    /// <summary>Hides an open inquiry (a boot notice nobody will click).</summary>
    void HideAnyInquiry();

    /// <summary>Starts the trial's campaign-free game from the main menu.</summary>
    void StartGame();

    /// <summary>A mission is open.</summary>
    bool MissionOpen { get; }

    /// <summary>Wall seconds since the host was built: the clock of every limit.</summary>
    double WallSeconds { get; }

    /// <summary>Ends the trial's game and shows the main menu; does nothing when another game runs.</summary>
    void ReturnToMainMenu();

    /// <summary>Asks the engine to shut down.</summary>
    void Quit();
}
