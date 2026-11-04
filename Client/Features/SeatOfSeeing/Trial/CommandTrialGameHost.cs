using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The engine half of the field trial's autostart (build-spec section 12): every <see cref="ICommandTrialGameHost"/> call is one TaleWorlds call.</summary>
internal sealed class CommandTrialGameHost : ICommandTrialGameHost
{
    private readonly CommandTrialOptions _options;
    private readonly IModLogger _logger;
    private readonly Stopwatch _wall = Stopwatch.StartNew();

    internal CommandTrialGameHost(CommandTrialOptions options, IModLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool AtMainMenu => GameStateManager.Current?.ActiveState is InitialState && Game.Current == null;

    public void HideAnyInquiry()
    {
        if (InformationManager.IsAnyInquiryActive())
            InformationManager.HideInquiry();
    }

    public void StartGame() => MBGameManager.StartNewGame(new CommandTrialGameManager(_options, _logger));

    public float? MissionSeconds => Mission.Current?.CurrentTime;

    public double WallSeconds => _wall.Elapsed.TotalSeconds;

    // MBGameManager.EndGame ends the mission and, with the manager ending, MissionState cleans every state, the game is destroyed and
    // the engine shows the main menu (FordsOfWarGameManager.ReturnToMainMenu). It is safe to call twice.
    public void ReturnToMainMenu()
    {
        if (MBGameManager.Current is CommandTrialGameManager)
            MBGameManager.EndGame();
    }

    public void Quit() => Utilities.QuitGame();
}
