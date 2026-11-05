using System;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Quests;

/// <summary>
/// The static entry the quest-log patches call (docs/features/shadowfax-screens.md). Its only state is the service the
/// module hands it. Every member answers "do nothing" (false, null, no-op) while unwired and never throws: a fast-open
/// patch must not break the Quest log.
/// </summary>
internal static class QuestLogDeferCalls
{
    internal const string Category = "Patch130_ShadowfaxQuests";

    internal static QuestLogDeferService? Service { get; private set; }

    internal static void Initialize(QuestLogDeferService? service) => Service = service;

    /// <summary>The QuestsVM constructor prefix: opens the service's scope and the [ScreenOpen] window. Null when it fails.</summary>
    internal static QuestOpenCall? BeginOpen()
    {
        try
        {
            var service = Service;
            service?.BeginOpen();
            return new QuestOpenCall(service, ScreenOpenTiming.Begin("Quests", "open"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The QuestsVM constructor finalizer: closes the scope and writes the open line. A null call (prefix stripped) does nothing.</summary>
    internal static void EndOpen(QuestOpenCall? call, Exception? exception)
    {
        if (call == null) return;
        try
        {
            ScreenOpenTiming.End(call.Window, exception, call.Service?.EndOpen());
        }
        catch
        {
            // The measurement must never break the screen.
        }
    }

    internal static long BeginItem()
    {
        try { return Service?.BeginItem() ?? 0; }
        catch { return 0; }
    }

    internal static void EndItem(long start)
    {
        try { Service?.EndItem(start); }
        catch { /* timing only */ }
    }

    internal static bool ShouldSkipPopulate(object item, bool hasJournalEntry, object? log)
    {
        try { return Service?.TryDefer(item, hasJournalEntry, log) ?? false; }
        catch { return false; }
    }

    internal static void EnsureBuilt(object? item)
    {
        try { Service?.EnsureBuilt(item); }
        catch { /* the service already contains its own failures */ }
    }

    internal static bool? UpdatedFor(object item)
    {
        try { return Service?.UpdatedFor(item); }
        catch { return null; }
    }

    internal static object? SortFallback(object item, bool last)
    {
        try { return Service?.SortFallback(item, last); }
        catch { return null; }
    }
}
