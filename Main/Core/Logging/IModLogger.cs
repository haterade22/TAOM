using System;

namespace TAOM.Core.Logging;

public interface IModLogger : IDisposable
{
    void LogInfo(string message);
    void LogDebug(string message);
    void LogWarning(string message);
    void LogError(string message);
}
