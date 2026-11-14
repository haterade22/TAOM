using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace TAOM.Core.Diagnostics;

/// <summary>One fix that turned itself off: the site id and a short reason.</summary>
internal sealed class SelfDisable
{
    internal SelfDisable(string id, string reason)
    {
        Id = id;
        Reason = reason;
    }

    internal string Id { get; }
    internal string Reason { get; }
}

/// <summary>
/// Issue #1185: the record of fixes that turned themselves off at run time. Harmony counts a transpiler
/// that found no anchor as applied, and a latch trips long after any apply notice ran, so neither reaches
/// the patch failure notice. A site reports here on its failure branch; <see cref="SelfDisableNotice"/>
/// shows the entries on the main thread.
///
/// Threads: any. <see cref="Report"/> takes only the short internal lock of <c>ConcurrentDictionary.TryAdd</c>,
/// runs no other code while it holds it, writes no log and shows nothing, so it is safe from the AI thread, a
/// native callback or before IoC exists. Once per id for the life of the ledger.
/// </summary>
internal sealed class SelfDisableLedger
{
    private readonly ConcurrentDictionary<string, byte> _seen = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    private readonly ConcurrentQueue<SelfDisable> _pending = new ConcurrentQueue<SelfDisable>();
    private int _pendingCount;

    internal bool HasPending => Volatile.Read(ref _pendingCount) > 0;

    // Main thread only (SelfDisableNotice): the entries already logged whose chat line is not shown yet.
    internal List<SelfDisable> Unshown { get; } = new List<SelfDisable>();

    // Main thread only: the last chat line threw. The notice tries again when a new entry arrives, not every frame.
    internal bool ShowFailed { get; set; }

    // Any thread: no I/O, nothing shown. False for a null or empty id or an id already seen.
    internal bool Report(string id, string? reason)
    {
        if (string.IsNullOrEmpty(id) || !_seen.TryAdd(id, 0)) return false;
        _pending.Enqueue(new SelfDisable(id, reason ?? ""));
        Interlocked.Increment(ref _pendingCount);
        return true;
    }

    // Main thread (SelfDisableNotice): every pending entry in report order, then none.
    internal IReadOnlyList<SelfDisable> Take()
    {
        var taken = new List<SelfDisable>();
        while (_pending.TryDequeue(out var entry))
        {
            Interlocked.Decrement(ref _pendingCount);
            taken.Add(entry);
        }
        return taken;
    }
}

/// <summary>The static door every site calls: <c>SelfDisables.Report(id, reason)</c>.</summary>
internal static class SelfDisables
{
    private static volatile SelfDisableLedger _ledger = new SelfDisableLedger();

    // Tests swap a fresh ledger in and put the old one back.
    internal static SelfDisableLedger Ledger
    {
        get => _ledger;
        set => _ledger = value ?? new SelfDisableLedger();
    }

    internal static void Report(string id, string? reason)
    {
        try { _ledger.Report(id, reason); }
        catch { /* swallow-ok: a report must never break the latch or the transpiler that makes it */ }
    }
}
