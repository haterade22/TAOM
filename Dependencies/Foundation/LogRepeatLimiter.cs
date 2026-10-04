using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Lets a log line be written in full once and counts its repeats, so a patch that throws on every call
/// cannot fill diag.log with one line (maintainer decision D16). Nothing is dropped: the first occurrence
/// is the full line, <see cref="Repeated"/> and <see cref="TakeUnreported"/> hand back every repeat count for
/// the owner to report at a boundary of its own, and a line the table has no room for is written in full every
/// time, as it was before any limit existed. A write that did not land is not a write: the line stays owed until
/// one does, and the occurrence whose write failed is counted like a repeat. A count report that did not land is
/// owed the same way (<see cref="Unreport"/>). Thread-safe, because a finalizer runs on whichever thread called the
/// patched method.
/// </summary>
internal sealed class LogRepeatLimiter
{
    private sealed class Tracked
    {
        /// <summary>Occurrences counted instead of written, a failed write included.</summary>
        public long Repeats;

        /// <summary>
        /// The value of <see cref="Repeats"/> the last <see cref="TakeUnreported"/> handed out, or 0 once
        /// <see cref="Unreport"/> gave the line back.
        /// </summary>
        public long Reported;

        /// <summary>A write of this line has landed.</summary>
        public bool Written;

        /// <summary>Some thread is writing this line now, so a competing occurrence is counted, not written twice.</summary>
        public bool Writing;
    }

    private readonly Dictionary<string, Tracked> _lines = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly int _capacity;

    /// <param name="capacity">
    /// How many distinct lines are tracked. The table is bounded because a line's text can include an
    /// exception message, and a message that varied per call would otherwise grow it for the whole process.
    /// </param>
    public LogRepeatLimiter(int capacity) => _capacity = capacity;

    /// <summary>
    /// True when the caller should write <paramref name="line"/> in full: its first occurrence, an occurrence after
    /// a write of it failed, or any occurrence of a line the table has no room for. False for a repeat, which is
    /// counted instead. Every true for a line the table tracks must be followed by <see cref="WriteFinished"/>.
    /// </summary>
    public bool ShouldWrite(string line)
    {
        lock (_lock)
        {
            if (_lines.TryGetValue(line, out var tracked))
            {
                if (tracked.Written || tracked.Writing)
                {
                    tracked.Repeats++;
                    return false;
                }
                tracked.Writing = true;   // the earlier write failed, so this occurrence writes the line again
                return true;
            }
            if (_lines.Count < _capacity) _lines.Add(line, new Tracked { Writing = true });
            return true;
        }
    }

    /// <summary>
    /// Settles the write <see cref="ShouldWrite"/> asked for. When it did not land, the occurrence is counted and the
    /// line is owed again, so the next occurrence writes it. A line the table has no room for has nothing to settle.
    /// </summary>
    public void WriteFinished(string line, bool succeeded)
    {
        lock (_lock)
        {
            if (!_lines.TryGetValue(line, out var tracked)) return;
            tracked.Writing = false;
            if (succeeded) tracked.Written = true;
            else tracked.Repeats++;
        }
    }

    /// <summary>
    /// Every line that repeated, with how many occurrences were counted instead of written (the repeats after the
    /// one written in full, and any occurrence whose write failed), most repeated first (ties by text, so the order
    /// is stable). A line seen once and written is already in the log in full and is not listed. A line whose every
    /// write failed is listed, and the count line its owner writes for it is then the only copy of its text.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> Repeated()
    {
        lock (_lock)
        {
            return Snapshot(_lines.Where(entry => entry.Value.Repeats > 0));
        }
    }

    /// <summary>
    /// The lines whose count grew since the last call, with their running totals (not the growth), ordered as
    /// <see cref="Repeated"/> is. A boundary that reports as it goes calls this, so each report carries only news;
    /// <see cref="Repeated"/> still lists everything for the report at the end. A line whose report did not land goes
    /// back with <see cref="Unreport"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, long>> TakeUnreported()
    {
        lock (_lock)
        {
            var grown = _lines.Where(entry => entry.Value.Repeats > entry.Value.Reported).ToList();
            foreach (var entry in grown) entry.Value.Reported = entry.Value.Repeats;
            return Snapshot(grown);
        }
    }

    /// <summary>
    /// Gives back a line <see cref="TakeUnreported"/> handed out, because the owner's write of its count did not
    /// land: the next call lists it again with its total as it stands then, though it did not grow. No other line is
    /// touched, and a line the table does not hold has nothing to give back.
    /// </summary>
    public void Unreport(string line)
    {
        lock (_lock)
        {
            if (_lines.TryGetValue(line, out var tracked)) tracked.Reported = 0;
        }
    }

    private static IReadOnlyList<KeyValuePair<string, long>> Snapshot(IEnumerable<KeyValuePair<string, Tracked>> entries) =>
        entries.OrderByDescending(entry => entry.Value.Repeats)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new KeyValuePair<string, long>(entry.Key, entry.Value.Repeats))
            .ToList();
}
