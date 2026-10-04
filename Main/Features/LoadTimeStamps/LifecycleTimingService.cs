using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// The [Lifecycle] stamps. Every CampaignEventDispatcher call of a new game, a loaded save and the
/// session start gets a dispatch line, whatever "Enable Load-Time Stamps" says. With the toggle on,
/// the call's campaign handlers are timed as well, for the length of that one call.
/// </summary>
public sealed class LifecycleTimingService
{
    private readonly ICampaignListenerAdapter _adapter;
    private readonly LoadStampDetailGate _gate;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;

    public LifecycleTimingService(ICampaignListenerAdapter adapter, LoadStampDetailGate gate, IStampClock clock, IModLogger logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static readonly LifecycleEvent[] NewGameEvents =
    {
        LifecycleEvent.OnNewGameCreated, LifecycleEvent.OnNewGameCreatedPartialFollowUp, LifecycleEvent.OnNewGameCreatedPartialFollowUpEnd,
    };

    private static readonly LifecycleEvent[] EarlyLoadedEvents = { LifecycleEvent.OnGameEarlyLoaded };
    private static readonly LifecycleEvent[] LoadedEvents = { LifecycleEvent.OnGameLoaded };
    private static readonly LifecycleEvent[] SessionEvents = { LifecycleEvent.OnSessionLaunched };
    private static readonly LifecycleEvent[] AfterSessionEvents = { LifecycleEvent.OnAfterSessionLaunched };

    private int _offLogged;
    private int _faultLogged;
    private int _restoreFaultLogged;
    private volatile string? _wrapFailure;

    /// <summary>The CampaignEvents events a dispatcher method invokes, in its order (v1.5.3 CampaignEvents.cs:2088-2116).</summary>
    internal static IReadOnlyList<LifecycleEvent> EventsOf(LifecycleDispatch dispatch) => dispatch switch
    {
        LifecycleDispatch.OnNewGameCreated => NewGameEvents,
        LifecycleDispatch.OnGameEarlyLoaded => EarlyLoadedEvents,
        LifecycleDispatch.OnGameLoaded => LoadedEvents,
        LifecycleDispatch.OnSessionStart => SessionEvents,
        LifecycleDispatch.OnAfterSessionStart => AfterSessionEvents,
        _ => Array.Empty<LifecycleEvent>(),
    };

    /// <summary>
    /// The dispatcher prefix's call: a scope with the start tick, or null when the stamp itself
    /// failed. With the toggle on, every listener of the dispatch's events is also swapped for a
    /// timing wrapper, unless per-handler timing is off this session (binding missing, or an earlier
    /// wrap failed). With the toggle off nothing is swapped and the adapter is not asked: the
    /// dispatch line is all the scope will write.
    /// </summary>
    public LifecycleDispatchScope? Begin(LifecycleDispatch dispatch)
    {
        LifecycleDispatchScope scope;
        bool detail;
        try
        {
            detail = _gate.Enabled;
            scope = new LifecycleDispatchScope(dispatch, _clock.Now);
        }
        catch (Exception ex)
        {
            LogFaultOnce(ex);
            return null;
        }

        if (detail) TimeListeners(scope);

        // The dispatch is timed from here, after the swap and any C4 line, on every path. A clock
        // fault keeps the first tick and the swap: a stamp fault, which turns nothing off.
        try
        {
            scope.Start = _clock.Now;
        }
        catch (Exception ex)
        {
            LogFaultOnce(ex);
        }

        return scope;
    }

    // Per-handler timing: swaps every listener of the dispatch's events for a timing wrapper. A
    // missing binding or a failed swap turns it off for the session (C4) and leaves the dispatch line.
    private void TimeListeners(LifecycleDispatchScope scope)
    {
        var problem = _adapter.BindingProblem ?? _wrapFailure;
        if (problem != null)
        {
            LogOffOnce(problem);
            return;
        }

        try
        {
            foreach (var lifecycleEvent in EventsOf(scope.Dispatch))
            {
                var record = new LifecycleDispatchScope.EventRecord(lifecycleEvent);
                // Listed before the wrap, so a wrap that throws midway is restored with the rest.
                scope.Events.Add(record);
                record.Listeners = _adapter.WrapListeners(lifecycleEvent);
            }

            scope.HasListeners = true;
        }
        catch (Exception ex)
        {
            RestoreAll(scope);
            _wrapFailure = Describe(ex);
            LogOffOnce(_wrapFailure);
        }
    }

    /// <summary>
    /// The dispatcher finalizer's call: logs each swapped event's handlers at or over the threshold
    /// and its total, then the dispatch line (the only line when nothing was swapped), and always
    /// puts every original listener back.
    /// </summary>
    public void End(LifecycleDispatchScope? scope, Exception? exception)
    {
        if (scope == null) return;
        try
        {
            try
            {
                WriteLines(scope, exception);
            }
            finally
            {
                RestoreAll(scope);
            }
        }
        catch (Exception ex)
        {
            LogFaultOnce(ex);
        }
    }

    private void WriteLines(LifecycleDispatchScope scope, Exception? exception)
    {
        // Read before any line is written: the dispatch time never includes the stamp's own logging.
        var end = _clock.Now;
        var frequency = _clock.Frequency;
        long listenersTicks = 0;
        foreach (var record in scope.Events)
        {
            var evt = record.Event.ToString();
            var listeners = record.Listeners;
            long eventTicks = 0, taomTicks = 0, maxTicks = 0;
            int taomListeners = 0, overThreshold = 0;
            string? maxHandler = null;
            foreach (var timing in listeners)
            {
                var ms = LoadTimeStampLines.ToMs(timing.Ticks, frequency);
                eventTicks += timing.Ticks;
                if (timing.IsTaom)
                {
                    taomListeners++;
                    taomTicks += timing.Ticks;
                }

                if (maxHandler == null || timing.Ticks > maxTicks)
                {
                    maxTicks = timing.Ticks;
                    maxHandler = timing.Handler;
                }

                if (ms >= LoadTimeStampLines.HandlerThresholdMs)
                {
                    overThreshold++;
                    _logger.LogInfo(LoadTimeStampLines.Handler(evt, timing.Handler, timing.Assembly, timing.Calls, ms,
                        LoadTimeStampLines.ToMs(timing.MaxTicks, frequency), timing.MaxArgument));
                }
            }

            listenersTicks += eventTicks;
            _logger.LogInfo(LoadTimeStampLines.EventTotal(evt, listeners.Count, taomListeners,
                LoadTimeStampLines.ToMs(eventTicks, frequency), LoadTimeStampLines.ToMs(taomTicks, frequency),
                LoadTimeStampLines.ToMs(eventTicks - taomTicks, frequency), overThreshold,
                LoadTimeStampLines.ToMs(maxTicks, frequency), maxHandler));
        }

        _logger.LogInfo(LoadTimeStampLines.Dispatch(
            scope.Dispatch.ToString(),
            LoadTimeStampLines.ToMs(end - scope.Start, frequency),
            scope.HasListeners ? LoadTimeStampLines.ToMs(listenersTicks, frequency) : null,
            exception == null ? "ok" : exception.GetType().Name));
    }

    // Every swapped event goes back, each on its own: one failed restore never strands the others.
    private void RestoreAll(LifecycleDispatchScope scope)
    {
        foreach (var record in scope.Events)
        {
            try
            {
                _adapter.RestoreListeners(record.Event);
            }
            catch (Exception ex)
            {
                LogRestoreFaultOnce(ex);
            }
        }

        scope.Events.Clear();
    }

    // One WARNING per process for per-handler timing switching itself off (binding missing, a wrap
    // failed): the reason and the consequence, never per dispatch.
    private void LogOffOnce(string problem) =>
        WarnOnce(ref _offLogged, LoadTimeStampLines.LifecycleOff(problem));

    // One WARNING per process for the stamp's own fault (a clock or logger failure), which turns
    // nothing off; its own latch, so it never hides a later switch-off line.
    private void LogFaultOnce(Exception exception) =>
        WarnOnce(ref _faultLogged, LoadTimeStampLines.LifecycleFault(exception));

    // One WARNING per process for a listener that could not be put back: its record keeps the timing
    // wrapper for the session (the adapter does not retry). Its own latch, beside the other two.
    private void LogRestoreFaultOnce(Exception exception) =>
        WarnOnce(ref _restoreFaultLogged, LoadTimeStampLines.LifecycleRestoreFault(exception));

    private void WarnOnce(ref int latch, string line)
    {
        if (System.Threading.Interlocked.Exchange(ref latch, 1) != 0) return;
        try
        {
            _logger.LogWarning(line);
        }
        catch
        {
            // A stamp must never break a load.
        }
    }

    private static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;
}
