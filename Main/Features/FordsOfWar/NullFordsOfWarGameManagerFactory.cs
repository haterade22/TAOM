using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar;

/// <summary>Core's default (ADR-013): no game manager. TAOM.Client replaces it on a rendering client; here it only says why nothing starts.</summary>
internal sealed class NullFordsOfWarGameManagerFactory : IFordsOfWarGameManagerFactory
{
    private readonly IModLogger _logger;

    public NullFordsOfWarGameManagerFactory(IModLogger logger)
    {
        _logger = logger;
    }

    public MBGameManager? Create(FordsMatchSetup setup)
    {
        _logger.LogInfo("[FordsOfWar] no client game manager factory is registered (TAOM.Client is absent); a match cannot load");
        return null;
    }
}
