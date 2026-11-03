using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Spike;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// Loads The Long Defeat (#1079): a campaign-free <see cref="CustomGame"/> from the main menu, then the mission, in the shape of
/// <c>FordsOfWarGameManager</c>. A load that opens no mission goes back to the main menu.
/// </summary>
public sealed class LongDefeatGameManager : CustomGameManager
{
    private readonly LongDefeatSpikeOptions _options;
    private readonly LongDefeatSpikeLauncher _launcher;
    private readonly IModLogger _logger;

    internal LongDefeatGameManager(LongDefeatSpikeOptions options, LongDefeatSpikeLauncher launcher, IModLogger logger)
    {
        _options = options;
        _launcher = launcher;
        _logger = logger;
    }

    public override void OnLoadFinished()
    {
        base.OnLoadFinished();
        if (_launcher.Launch(_options))
            return;

        _logger.LogError("[LongDefeat] the launcher opened no mission; returning to the main menu");
        if (Current is LongDefeatGameManager)
            EndGame();
    }
}
