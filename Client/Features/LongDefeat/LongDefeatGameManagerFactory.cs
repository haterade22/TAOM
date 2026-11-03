using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Spike;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>Builds The Long Defeat's game manager (ADR-013); core's null factory builds none. Registered in <c>TaomClientComposition</c>.</summary>
internal sealed class LongDefeatGameManagerFactory : ILongDefeatGameManagerFactory
{
    private readonly LongDefeatSpikeLauncher _launcher;
    private readonly IModLogger _logger;

    public LongDefeatGameManagerFactory(LongDefeatSpikeLauncher launcher, IModLogger logger)
    {
        _launcher = launcher;
        _logger = logger;
    }

    public MBGameManager? Create(LongDefeatSpikeOptions options) => new LongDefeatGameManager(options, _launcher, _logger);
}
