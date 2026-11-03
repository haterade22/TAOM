using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Spike;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat;

/// <summary>Core's default (ADR-013): no game manager. TAOM.Client replaces it on a rendering client.</summary>
internal sealed class NullLongDefeatGameManagerFactory : ILongDefeatGameManagerFactory
{
    private readonly IModLogger _logger;

    public NullLongDefeatGameManagerFactory(IModLogger logger)
    {
        _logger = logger;
    }

    public MBGameManager? Create(LongDefeatSpikeOptions options)
    {
        _logger.LogInfo("[LongDefeat] no client game manager factory is registered (TAOM.Client is absent); nothing loads");
        return null;
    }
}
