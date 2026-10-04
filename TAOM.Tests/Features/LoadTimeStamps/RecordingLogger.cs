using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// Collects every line with its level prefix ("INFO ", "DEBUG ", "WARN ", "ERROR "). <see cref="OnLog"/>
/// runs first with that prefixed line, so a test can throw from it (the line is then not collected)
/// or advance a clock.
/// </summary>
internal sealed class RecordingLogger : IModLogger
{
    public List<string> Lines { get; } = new();

    public Action<string>? OnLog { get; set; }

    public void LogInfo(string message) => Add("INFO " + message);

    public void LogDebug(string message) => Add("DEBUG " + message);

    public void LogWarning(string message) => Add("WARN " + message);

    public void LogError(string message) => Add("ERROR " + message);

    public string? LogFilePath => null;

    public void Dispose() { }

    private void Add(string line)
    {
        OnLog?.Invoke(line);
        Lines.Add(line);
    }
}
