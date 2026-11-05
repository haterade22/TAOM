using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Quests;

/// <summary>
/// Quest log fast open (docs/features/shadowfax-screens.md): decides which finished quests skip building their stages
/// inside the QuestsVM constructor, builds them when one is selected, and keeps the updated dot and the date sort right
/// for the ones still unbuilt. Items are opaque keys (a <c>QuestItemVM</c> behind <see cref="IQuestJournalAdapter"/>), so
/// the service holds no TaleWorlds type. The open's scope, its snapshot of the toggle and of the adapter's bound state, and
/// the replay flag are per thread: the UI thread opens the screen, and nothing else may defer. The settings provider never
/// throws (its contract), so reads of it are not wrapped; only the adapter, which touches the engine, is.
/// </summary>
public sealed class QuestLogDeferService
{
    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IQuestJournalAdapter _adapter;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;
    private readonly ThreadLocal<OpenScope> _scope = new(() => new OpenScope());
    private readonly ConditionalWeakTable<object, Mark> _marks = new();
    private int _disarmed;

    public QuestLogDeferService(IShadowfaxScreensSettingsProvider settings, IQuestJournalAdapter adapter, IStampClock clock, IModLogger logger)
    {
        _settings = settings;
        _adapter = adapter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>True after a rebuild threw: no later open defers anything this process.</summary>
    public bool Disarmed => Volatile.Read(ref _disarmed) != 0;

    /// <summary>Binds the adapter and writes one line saying whether the fast open is installed. Returns the reason it is not, or null.</summary>
    public string? Install()
    {
        string? problem;
        try
        {
            problem = _adapter.Bind();
        }
        catch (Exception ex)
        {
            problem = ex.GetType().Name + ": " + ex.Message;
        }

        try
        {
            if (problem == null)
                _logger.LogInfo("[QuestLog] fast open installed; finished quests build when selected (fast open " + (_settings.QuestLogFastOpen ? "on" : "off") + ").");
            else
                _logger.LogWarning("[QuestLog] fast open unavailable (" + problem + "); vanilla builds the quest log.");
        }
        catch
        {
            // A log line must not break game start.
        }

        return problem;
    }

    /// <summary>Opens the scope on this thread. The outermost call snapshots the toggle and the bound state and zeroes the counts.</summary>
    public void BeginOpen()
    {
        var scope = _scope.Value!;
        if (scope.Depth > 0)
        {
            scope.Depth++;
            return;
        }

        // The adapter touches the engine: read it before Depth moves, so a throw cannot leave the scope open.
        bool bound;
        try
        {
            bound = _adapter.IsBound;
        }
        catch
        {
            bound = false;
        }

        scope.Depth = 1;
        scope.Reset();
        scope.Timing = _settings.LogScreenOpenTimes;
        scope.FastOpen = _settings.QuestLogFastOpen;
        scope.Bound = bound;
        scope.FastPath = scope.FastOpen && scope.Bound && !Disarmed;
    }

    /// <summary>Closes the scope. The outermost close returns the detail for the open line; a nested close or one without a begin returns null.</summary>
    public string? EndOpen()
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0)
        {
            scope.Depth = 0;
            return null;
        }

        if (--scope.Depth > 0) return null;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "fast open {0}; old={1}, deferred={2}, entries={3}, updated={4}, {5}, journal items {6:0.0} ms",
            scope.FastPath ? "on" : "off",
            scope.OldItems, scope.DeferredItems, scope.DeferredEntries, scope.Updated,
            scope.Bound ? "bound" : "unbound",
            LoadTimeStampLines.ToMs(scope.ItemTicks, _clock.Frequency));
    }

    /// <summary>Counts one journal item built in this open; returns its start tick, or 0 when the timer is off or the clock fails.</summary>
    public long BeginItem()
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0) return 0;
        scope.OldItems++;
        if (!scope.Timing) return 0;
        return Math.Max(_clock.Now, 1);
    }

    /// <summary>Adds the item's elapsed time to the open's total. A zero start (no prefix ran, or the timer is off) adds nothing.</summary>
    public void EndItem(long start)
    {
        var scope = _scope.Value!;
        if (start <= 0 || scope.Depth <= 0) return;
        scope.ItemTicks += _clock.Now - start;
    }

    /// <summary>
    /// True when PopulateQuestLog should be skipped for this item's entry; the item is then marked as deferred. Only inside an
    /// open whose snapshot had the toggle on and the adapter bound, for an item with a journal entry, outside a replay, and
    /// while armed. The first call for an item records its first journal entry on the mark and every call its last, because the
    /// journal constructor feeds <c>GetEntries()</c> in order; <see cref="SortFallback"/> reads them.
    /// </summary>
    public bool TryDefer(object item, bool hasJournalEntry, object? log = null)
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0 || scope.Replaying || !scope.FastOpen || !scope.Bound || !hasJournalEntry || Disarmed)
            return false;
        if (!_marks.TryGetValue(item, out var mark))
        {
            mark = new Mark { First = log };
            _marks.Add(item, mark);
            scope.DeferredItems++;
        }

        mark.Last = log;
        scope.DeferredEntries++;
        return true;
    }

    public bool IsDeferred(object item) => _marks.TryGetValue(item, out _);

    /// <summary>
    /// Builds a deferred item's stages once, whatever the toggle says now. A rebuild that throws is logged once, disarms
    /// deferral for the process and leaves the item marked (so sorting keeps reading its entries); the item is not retried.
    /// </summary>
    public void EnsureBuilt(object? item)
    {
        if (item == null || !_marks.TryGetValue(item, out var mark) || mark.Failed) return;
        var scope = _scope.Value!;
        if (scope.Replaying) return;
        scope.Replaying = true;
        try
        {
            _adapter.Rebuild(item);
            _marks.Remove(item);
        }
        catch (Exception ex)
        {
            mark.Failed = true;
            Disarm(ex);
        }
        finally
        {
            scope.Replaying = false;
        }
    }

    /// <summary>The updated dot for a deferred item (any journal entry unexamined), or null to leave vanilla's value.</summary>
    public bool? UpdatedFor(object item)
    {
        if (!IsDeferred(item)) return null;
        try
        {
            var updated = _adapter.AnyUnexamined(item);
            var scope = _scope.Value!;
            if (updated && scope.Depth > 0 && !scope.Replaying) scope.Updated++;
            return updated;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The journal entry vanilla's sort would read from a built item's stages: the first or last recorded on its mark. Null for any item that is not deferred.</summary>
    public object? SortFallback(object item, bool last)
    {
        return _marks.TryGetValue(item, out var mark) ? (last ? mark.Last : mark.First) : null;
    }

    private void Disarm(Exception ex)
    {
        if (Interlocked.Exchange(ref _disarmed, 1) != 0) return;
        try
        {
            _logger.LogWarning("[QuestLog] building a finished quest on demand threw " + ex.GetType().Name + ": " + ex.Message
                + "; fast open is off until the game restarts.");
        }
        catch
        {
            // A log line must not break the screen.
        }
    }

    private sealed class Mark
    {
        public bool Failed;
        public object? First;
        public object? Last;
    }

    private sealed class OpenScope
    {
        public int Depth;
        public bool Timing;
        public bool FastOpen;
        public bool Bound;
        public bool FastPath;
        public bool Replaying;
        public int OldItems;
        public int DeferredItems;
        public int DeferredEntries;
        public int Updated;
        public long ItemTicks;

        public void Reset()
        {
            OldItems = 0;
            DeferredItems = 0;
            DeferredEntries = 0;
            Updated = 0;
            ItemTicks = 0;
            Replaying = false;
        }
    }
}
