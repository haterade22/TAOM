using System;

namespace TAOM.Features.BattleLoadDiagnostics;

// Synthetic exception handed to ICrashReportService to produce a diagnostic bundle for a
// (crashless) battle-load stall. NOT thrown into the game loop — only passed to
// HandleException so the existing collector pipeline writes a ZIP under Logs/.
public sealed class BattleLoadStallException : Exception
{
    public BattleLoadStallException(string message) : base(message) { }
}
