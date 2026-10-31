using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>Builds the Fords of War game manager (ADR-013); core's null factory builds none. Registered in <c>TaomClientComposition</c>.</summary>
internal sealed class FordsOfWarGameManagerFactory : IFordsOfWarGameManagerFactory
{
    private readonly FordsMissionLauncher _launcher;
    private readonly IFordsMenuAdapter _menu;
    private readonly IModLogger _logger;
    private readonly FordsRunReport _report;

    public FordsOfWarGameManagerFactory(FordsMissionLauncher launcher, IFordsMenuAdapter menu, IModLogger logger, FordsRunReport report)
    {
        _launcher = launcher;
        _menu = menu;
        _logger = logger;
        _report = report;
    }

    public MBGameManager? Create(FordsMatchSetup setup) => new FordsOfWarGameManager(setup, _launcher, _menu, _logger, _report);
}
