using System;
using System.Collections.Generic;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// The always-on [LoadXml] stamp: one line per MBObjectManager.LoadXML call (its merged file and
/// XSLT counts, its time split into the merge and the object creation) and a summary per game
/// initialization. The call in flight is per thread; the summary's aggregate is under one lock.
/// Every public member swallows its own faults and warns once per process.
/// </summary>
public sealed class LoadXmlStampService
{
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;

    public LoadXmlStampService(IStampClock clock, IModLogger logger)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private readonly System.Threading.ThreadLocal<LoadXmlCall?> _inFlight = new();
    private readonly object _sync = new();
    private int _faulted;

    // The aggregate since the last summary, under _sync.
    private int _calls;
    private int _files;
    private int _xslt;
    private long _ticks;
    private long _mergeTicks;
    private long _objectTicks;
    private long _maxTicks;
    private string? _maxId;
    private int _failed;
    private string? _game;

    /// <summary>Starts a call on this thread; the previous call in flight (if any) becomes its parent.</summary>
    public LoadXmlCall? Begin(string? id, string? gameType)
    {
        try
        {
            var call = new LoadXmlCall(id, gameType, _clock.Now, _inFlight.Value);
            _inFlight.Value = call;
            return call;
        }
        catch (Exception ex)
        {
            Fault(ex);
            return null;
        }
    }

    /// <summary>Records the end of the first merge of the call in flight on this thread, with its counts.</summary>
    public void MergeFinished(IList<Tuple<string, string>>? toBeMerged, IList<string>? xsltList)
    {
        try
        {
            var call = _inFlight.Value;
            if (call == null || call.MergeEnd.HasValue) return;
            call.MergeEnd = _clock.Now;
            call.Files = CountFiles(toBeMerged);
            call.Xslt = CountXslt(xsltList);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Restores the parent call, logs the call's line and adds it to the summary.</summary>
    public void End(LoadXmlCall? call, Exception? exception)
    {
        if (call == null) return;
        try
        {
            _inFlight.Value = call.Parent;
            var now = _clock.Now;
            var frequency = _clock.Frequency;
            var ticks = now - call.Start;
            long? mergeTicks = call.MergeEnd.HasValue ? call.MergeEnd.Value - call.Start : null;
            long? objectTicks = call.MergeEnd.HasValue ? now - call.MergeEnd.Value : null;
            var id = call.Id ?? "null";

            // Counted before the line is written: a failed write never drops the call from the summary.
            lock (_sync)
            {
                if (_calls == 0 || ticks > _maxTicks)
                {
                    _maxTicks = ticks;
                    _maxId = id;
                }

                _calls++;
                _files += call.Files;
                _xslt += call.Xslt;
                _ticks += ticks;
                if (mergeTicks.HasValue) _mergeTicks += mergeTicks.Value;
                if (objectTicks.HasValue) _objectTicks += objectTicks.Value;
                if (exception != null) _failed++;
                if (!string.IsNullOrEmpty(call.GameType)) _game = call.GameType;
            }

            _logger.LogInfo(LoadTimeStampLines.LoadXml(
                id,
                call.Files,
                LoadTimeStampLines.ToMs(ticks, frequency),
                call.Xslt,
                mergeTicks.HasValue ? LoadTimeStampLines.ToMs(mergeTicks.Value, frequency) : null,
                objectTicks.HasValue ? LoadTimeStampLines.ToMs(objectTicks.Value, frequency) : null,
                exception == null ? "ok" : exception.GetType().Name));
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Logs every call since the last summary as one line, then starts a new aggregate.</summary>
    public void LogSummary()
    {
        try
        {
            var frequency = _clock.Frequency;
            string line;
            lock (_sync)
            {
                line = LoadTimeStampLines.LoadXmlSummary(
                    _game,
                    _calls,
                    _files,
                    _xslt,
                    LoadTimeStampLines.ToMs(_ticks, frequency),
                    LoadTimeStampLines.ToMs(_mergeTicks, frequency),
                    LoadTimeStampLines.ToMs(_objectTicks, frequency),
                    LoadTimeStampLines.ToMs(_maxTicks, frequency),
                    _maxId,
                    _failed);
                _calls = _files = _xslt = _failed = 0;
                _ticks = _mergeTicks = _objectTicks = _maxTicks = 0;
                _maxId = null;
                _game = null;
            }

            _logger.LogInfo(line);
        }
        catch (Exception ex)
        {
            Fault(ex);
        }
    }

    /// <summary>Entries the engine merges: those whose file path is neither null nor "".</summary>
    internal static int CountFiles(IList<Tuple<string, string>>? toBeMerged)
    {
        if (toBeMerged == null) return 0;
        var count = 0;
        foreach (var entry in toBeMerged)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Item1)) count++;
        }
        return count;
    }

    /// <summary>XSLTs the engine applies: entries at index 1 and later that are neither null nor "".</summary>
    internal static int CountXslt(IList<string>? xsltList)
    {
        if (xsltList == null) return 0;
        var count = 0;
        for (var i = 1; i < xsltList.Count; i++)
        {
            if (!string.IsNullOrEmpty(xsltList[i])) count++;
        }
        return count;
    }

    // One WARNING per process: the reason and the consequence, never per call.
    private void Fault(Exception exception)
    {
        if (System.Threading.Interlocked.Exchange(ref _faulted, 1) != 0) return;
        try
        {
            _logger.LogWarning(LoadTimeStampLines.LoadXmlFault(exception));
        }
        catch
        {
            // A stamp must never break a load.
        }
    }
}
