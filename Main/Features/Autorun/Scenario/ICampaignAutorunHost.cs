using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>Which screen the game shows, as the scenario driver needs to tell them apart.</summary>
internal enum AutorunScreen
{
    /// <summary>Anything the driver does not wait for: loading, an unknown state.</summary>
    Other,
    MainMenu,
    /// <summary>The campaign intro video (<c>VideoPlaybackState</c>).</summary>
    Video,
    CharacterCreation,
    /// <summary><c>MapState</c> is active and no game menu is open.</summary>
    Map,
    /// <summary><c>MapState</c> is active with a game menu open; only a wait menu lets time run.</summary>
    MapMenu,
    /// <summary><c>MissionState</c> is active, its scene load included.</summary>
    Mission,
}

/// <summary>
/// The engine seams the unattended campaign driver needs (#1075), narrow enough that a test fakes them. TAOM.Client implements it
/// (<c>CampaignAutorunHost</c>) and replaces core's <see cref="NullCampaignAutorunHost"/> (ADR-013), so a dedicated server can never be
/// armed. The driver ticks on wall time from the client's application tick, so every call below is made from the engine's main thread.
/// The engine facts behind each call are in docs/features/scenario-autorun.md "Engine facts".
/// The story members (#1368) open the dev story menu and pick its options on the path a person's click takes
/// (<c>MenuContext.InvokeConsequence</c> after <c>GameMenu.GetMenuOptionConditionsHold</c>), from the application tick: never from the
/// console and never inside the campaign tick. They skip the cheat-mode gate of the <c>taom.story_open</c> console command, which is
/// safe because only the arm file starts the driver.
/// </summary>
internal interface ICampaignAutorunHost
{
    /// <summary>The screen the game shows now.</summary>
    AutorunScreen Screen { get; }

    /// <summary>Hides an open inquiry without running either of its buttons. Its title ("" when it has none), or null when none was open.</summary>
    string? HideInquiry();

    /// <summary>A scene notification is active. The engine marks it active one layer tick before its scene exists.</summary>
    bool SceneNotificationActive { get; }

    /// <summary>Hides the active scene notification without running its buttons. Call it only for a notification that was active on
    /// the previous tick too: closing one whose scene does not exist yet throws (GauntletSceneNotification.CloseNotification).</summary>
    void HideSceneNotification();

    /// <summary>Closes the open Realms popup: in an unattended run, a story event window (War Chronicle or coronation), which pauses
    /// the campaign while it shows. Its button only closes it. True when one was open.</summary>
    bool CloseEventWindow();

    /// <summary>
    /// Starts a new sandbox campaign with the default start options, as the start options screen's Confirm does when nothing changed, and stops focus loss from pausing the game (in memory, remembering the old value). False when it could not start.
    /// </summary>
    bool StartNewCampaign();

    /// <summary>Ends the intro video as its own end does. False when no video plays.</summary>
    bool FinishVideo();

    /// <summary>
    /// At the culture stage: picks the culture and finishes character creation with no other choice made. False while the stage is not
    /// ready yet (the driver asks again next tick). Sets <paramref name="problem"/> when it can never succeed, for example a culture
    /// character creation does not offer (campaign objects do not exist at the main menu, so this is the first place the id is checked).
    /// </summary>
    bool TryFinishCreation(string cultureId, out string? problem);

    /// <summary>
    /// Once, on the map: turns autosave off for this campaign, makes every other party ignore the main party and holds it in place.
    /// Returns what it did, for the log.
    /// </summary>
    string PrepareMap();

    /// <summary>Puts time on unstoppable fast forward at this multiplier. True when the mode or the multiplier had to be set again.</summary>
    bool KeepFastForward(float speedUpMultiplier);

    /// <summary>The time mode is locked (a popup or panel holds it).</summary>
    bool TimeLocked { get; }

    /// <summary>Whole and part days since the campaign started; NaN when no campaign runs.</summary>
    double ElapsedDays { get; }

    /// <summary>Puts back the focus-loss setting <see cref="StartNewCampaign"/> changed. Does nothing when it changed nothing.</summary>
    void RestoreFocusSetting();

    /// <summary>Ends the running campaign and shows the main menu; does nothing when no campaign runs.</summary>
    void EndCampaign();

    /// <summary>
    /// Writes a small text file into the plan's out folder through a temp file and a move, so the launcher polling for the file never
    /// reads half of it. May throw; the driver logs the failure.
    /// </summary>
    void WriteFile(string name, string text);

    /// <summary>Asks the engine to shut down.</summary>
    void Quit();

    /// <summary>A story event window is showing, or one is queued and opens on a later frame (any Realms popup counts).</summary>
    bool EventWindowOpenOrWaiting { get; }

    /// <summary>
    /// Opens the dev story menu for this mission with the bot playing the lord, through the dev menu's own refusals, and forgets the
    /// last verdict. <paramref name="message"/> is the dev menu's answer, or why it was not reached. True when the story menu shows.
    /// </summary>
    bool OpenStoryMenu(string missionId, out string message);

    /// <summary>The map shows the dev story menu.</summary>
    bool StoryMenuShown { get; }

    /// <summary>The id of the game menu the map shows, or null when none is open.</summary>
    string? OpenMenuId { get; }

    /// <summary>Picks "Open the mission" as a click does. False when the story menu is not shown or the option is not offered.</summary>
    bool ChooseOpenMission();

    /// <summary>Picks "Back to the map" as a click does. False when the story menu is not shown or the option is not offered.</summary>
    bool ChooseBackToMap();

    /// <summary>The verdict the mission opened after the last <see cref="OpenStoryMenu"/> reported, or null until it reports one.</summary>
    StoryOutcome? StoryVerdict { get; }

    /// <summary>Runs one console command line ("name arg arg") as the console does and returns what it printed (#1434).</summary>
    string RunConsoleCommand(string commandLine);
}
