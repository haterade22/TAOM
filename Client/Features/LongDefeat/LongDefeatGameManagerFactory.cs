using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>Builds The Long Defeat's game manager (ADR-013); core's null factory builds none. Registered in <c>TaomClientComposition</c>.</summary>
internal sealed class LongDefeatGameManagerFactory : ILongDefeatGameManagerFactory
{
    private readonly LongDefeatMissionLauncher _launcher;
    private readonly ILongDefeatMenuAdapter _menu;
    private readonly IModLogger _logger;

    public LongDefeatGameManagerFactory(LongDefeatMissionLauncher launcher, ILongDefeatMenuAdapter menu, IModLogger logger)
    {
        _launcher = launcher;
        _menu = menu;
        _logger = logger;
    }

    public bool Start(LongDefeatSetup setup)
    {
        MBGameManager.StartNewGame(new LongDefeatGameManager(setup, _launcher, _menu, _logger));
        return true;
    }
}
