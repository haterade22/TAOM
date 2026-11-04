using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>Collects every line written, in order, at any level; <see cref="Errors"/> holds the error lines again.</summary>
internal sealed class ListLogger : IModLogger
{
    internal readonly List<string> Lines = new();
    internal readonly List<string> Errors = new();

    public string? LogFilePath => null;

    public void LogInfo(string message) => Lines.Add(message);

    public void LogDebug(string message)
    {
    }

    public void LogWarning(string message) => Lines.Add(message);

    public void LogError(string message)
    {
        Lines.Add(message);
        Errors.Add(message);
    }

    public void Dispose()
    {
    }
}
