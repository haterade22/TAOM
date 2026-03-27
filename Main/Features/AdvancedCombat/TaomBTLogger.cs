using BehaviorTrees;
using TAOM.Core.Logging;

namespace TAOM.Features.AdvancedCombat;

public class TaomBTLogger : ILogger
{
    private static IModLogger _logger => IoC.Resolve<IModLogger>();

    public void LogMessage(string message)
    {
        _logger.LogError(message);
    }
}
