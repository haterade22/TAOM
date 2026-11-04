using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Hooks;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// Loads The Long Defeat (#1079): a campaign-free <see cref="CustomGame"/> from the main menu, then the mission, in the shape of
/// <c>FordsOfWarGameManager</c>. The base <c>OnLoadFinished</c> pushes the custom battle state, and the launcher opens the mission
/// on top. The way back is <see cref="ReturnToMainMenu"/>: <c>MBGameManager.EndGame</c> ends the mission and, with the manager
/// ending, the engine shows the main menu instead of the Custom Battle screen underneath. A load that opens no mission returns the
/// same way and tells the player.
/// </summary>
public sealed class LongDefeatGameManager : CustomGameManager
{
    private readonly Func<bool> _launch;
    private readonly ILongDefeatMenuAdapter _menu;
    private readonly IModLogger _logger;

    internal LongDefeatGameManager(LongDefeatSetup setup, LongDefeatMissionLauncher launcher, ILongDefeatMenuAdapter menu, IModLogger logger)
    {
        _launch = () => launcher.Launch(setup);
        _menu = menu;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        base.OnLoadFinished();
        try
        {
            if (_launch())
                return;

            _logger.LogError("[LongDefeat] the launcher opened no mission; returning to the main menu");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongDefeat] starting the mission failed: {ex}");
        }

        ReturnToMainMenu(_logger);
        LongDefeatSetupPrompt.ShowStartFailed(_menu, _logger);
    }

    /// <summary>Ends the running Long Defeat game and shows the main menu. Does nothing when the current game is another one, and never throws.</summary>
    internal static void ReturnToMainMenu(IModLogger? logger)
    {
        try
        {
            if (Current is LongDefeatGameManager)
                EndGame();
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"[LongDefeat] returning to the main menu failed: {ex.Message}");
        }
    }
}
