using BehaviorTrees;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace BehaviorTreeWrapper;

public class BannerlordLogger : ILogger
{
    public void LogMessage(string message)
    {
        InformationManager.DisplayMessage(new InformationMessage(message, new Color(1f, 0f, 0f, 1f)));
    }
}
