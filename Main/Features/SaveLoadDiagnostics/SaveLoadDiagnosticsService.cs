using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics;

public sealed class SaveLoadDiagnosticsService : ISaveLoadDiagnosticsService
{
    private const string Tag = "[SaveLoad]";

    // Parallel load workers can fault per-object; a systematically corrupt graph must not
    // flood the log. Reset per attempt.
    private const int FaultCap = 20;
    private const int UnknownIdCap = 50;
    private const int InnerExceptionCap = 5;

    private readonly IModLogger _logger;
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private readonly ConcurrentDictionary<string, byte> _unknownIds = new ConcurrentDictionary<string, byte>();

    private int _seq;
    private int _faultCount;

    public SaveLoadDiagnosticsService(IModLogger logger)
    {
        _logger = logger;
    }

    public int FaultCount => Volatile.Read(ref _faultCount);

    public void BeginLoadAttempt(string saveName) => BeginAttempt(SaveLoadPhase.LoadRequested, saveName);

    public void BeginSaveAttempt(string saveName) => BeginAttempt(SaveLoadPhase.SaveBegin, saveName);

    public void LogPhase(SaveLoadPhase phase, string detail)
    {
        try { _logger.LogInfo(Stamp(phase, detail)); }
        catch { /* the diagnostic must never propagate */ }
    }

    public void LogFault(SaveLoadPhase phase, string detail, Exception? exception)
    {
        try
        {
            int count = Interlocked.Increment(ref _faultCount);
            if (count > FaultCap)
            {
                if (count == FaultCap + 1)
                    SafeWarn($"{Tag} fault cap ({FaultCap}) reached for this attempt — suppressing further fault lines");
                return;
            }

            var primary = Unwrap(exception);
            var chain = primary == null ? string.Empty : $" ex={FlattenChain(exception!)}";
            _logger.LogError(Stamp(phase, detail + chain));

            var stack = primary?.StackTrace;
            if (!string.IsNullOrEmpty(stack))
                _logger.LogError($"{Tag} stack={stack}");
        }
        catch { /* the diagnostic must never propagate */ }
    }

    public void MarkUnknownSaveId(string kind, string saveId)
    {
        try
        {
            if (_unknownIds.Count >= UnknownIdCap) return;
            if (!_unknownIds.TryAdd($"{kind}|{saveId}", 0)) return;
            _logger.LogWarning(Stamp(SaveLoadPhase.UnknownSaveId, $"kind={kind} saveId='{saveId}' — this build has no definition for a type in the save (definer/build mismatch)"));
        }
        catch { /* the diagnostic must never propagate */ }
    }

    private void BeginAttempt(SaveLoadPhase phase, string saveName)
    {
        try
        {
            Interlocked.Exchange(ref _seq, 0);
            Interlocked.Exchange(ref _faultCount, 0);
            _unknownIds.Clear();
            _stopwatch.Restart();
            _logger.LogInfo(Stamp(phase, $"name='{saveName}'"));
        }
        catch { /* the diagnostic must never propagate */ }
    }

    private string Stamp(SaveLoadPhase phase, string detail)
    {
        int seq = Interlocked.Increment(ref _seq);
        long ms = 0;
        try { ms = _stopwatch.ElapsedMilliseconds; } catch { /* clock best-effort */ }
        return $"{Tag} seq={seq} t=+{ms}ms phase={phase} {detail}".TrimEnd();
    }

    // TWParallel wraps worker faults in AggregateException; reflection init callbacks wrap
    // theirs in TargetInvocationException. Peel both so the log names the real cause.
    private static Exception? Unwrap(Exception? ex)
    {
        while (true)
        {
            switch (ex)
            {
                case TargetInvocationException tie when tie.InnerException != null:
                    ex = tie.InnerException;
                    continue;
                case AggregateException agg when agg.InnerExceptions.Count > 0:
                    ex = agg.InnerExceptions[0];
                    continue;
                default:
                    return ex;
            }
        }
    }

    private static string FlattenChain(Exception ex)
    {
        var sb = new StringBuilder();
        if (ex is AggregateException agg)
        {
            var flat = agg.Flatten();
            int n = 0;
            foreach (var inner in flat.InnerExceptions)
            {
                if (n++ >= InnerExceptionCap) { sb.Append(" | …"); break; }
                if (n > 1) sb.Append(" | ");
                Describe(sb, inner);
            }
        }
        else
        {
            Describe(sb, ex);
        }
        return sb.ToString();

        static void Describe(StringBuilder sb, Exception e)
        {
            var cause = e;
            while (cause is TargetInvocationException tie && tie.InnerException != null)
                cause = tie.InnerException;
            sb.Append(cause.GetType().Name).Append(": ").Append(cause.Message);
            for (var deeper = cause.InnerException; deeper != null; deeper = deeper.InnerException)
                sb.Append(" <- ").Append(deeper.GetType().Name).Append(": ").Append(deeper.Message);
        }
    }

    private void SafeWarn(string message)
    {
        try { _logger.LogWarning(message); }
        catch { /* the diagnostic must never propagate */ }
    }
}
