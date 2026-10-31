using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Hooks;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// Loads a Fords of War match (build-spec 6): a campaign-free <see cref="CustomGame"/> from the main menu, then the mission. The
/// base <c>OnLoadFinished</c> pushes the custom battle state and marks the game loaded, as <c>TaomShaderGameManager</c> relies on;
/// the launcher then opens the mission on top. The way back is <see cref="ReturnToMainMenu"/>: <c>MBGameManager.EndGame</c> ends the
/// mission and, with the manager ending, <c>MissionState</c> cleans every state, the game is destroyed and the engine shows the main
/// menu. Without it the end of the mission would pop to the custom battle screen underneath. A load that fails after it began
/// (the launcher opens no mission, or throws) returns the same way and then shows the start-failed notice of the setup prompt; the
/// notice is a global inquiry layer, so it outlives the game's teardown. TAOM's Custom Battle models apply to a game this manager
/// starts (build-spec section 4.1 model check).
/// </summary>
public sealed class FordsOfWarGameManager : CustomGameManager
{
    private readonly FordsMatchSetup _setup;
    private readonly FordsMissionLauncher _launcher;
    private readonly IFordsMenuAdapter _menu;
    private readonly IModLogger _logger;

    internal FordsOfWarGameManager(FordsMatchSetup setup, FordsMissionLauncher launcher, IFordsMenuAdapter menu, IModLogger logger)
    {
        _setup = setup;
        _launcher = launcher;
        _menu = menu;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        base.OnLoadFinished();
        try
        {
            if (_launcher.Launch(_setup))
                return;

            _logger.LogError("[FordsOfWar] the launcher opened no mission; returning to the main menu");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] starting the mission failed: {ex}");
        }

        ReturnToMainMenu(_logger);
        FordsSetupPrompt.ShowStartFailed(_menu, _logger);
    }

    /// <summary>
    /// Ends the running Fords game and shows the main menu. Does nothing when the current game is not a Fords of War game, and
    /// never throws. <c>EndGame</c> is safe to call twice: the second call finds the manager already ending.
    /// </summary>
    internal static void ReturnToMainMenu(IModLogger? logger)
    {
        try
        {
            if (Current is FordsOfWarGameManager)
                EndGame();
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"[FordsOfWar] returning to the main menu failed: {ex.Message}");
        }
    }
}
